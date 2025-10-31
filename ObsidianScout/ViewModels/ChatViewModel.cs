using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObsidianScout.Models;
using ObsidianScout.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;
using Microsoft.Maui.ApplicationModel;

namespace ObsidianScout.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ISettingsService _settingsService;

    private readonly ObservableCollection<ChatMessage> _messages = new();
    public ObservableCollection<ChatMessage> Messages => _messages;

    private System.Timers.Timer? _pollTimer;

    private DateTime? _lastMessageTimestamp;
    public DateTime? LastMessageTimestamp
    {
        get => _lastMessageTimestamp;
        set => SetProperty(ref _lastMessageTimestamp, value);
    }

    private readonly ObservableCollection<ChatMember> _members = new();
    public ObservableCollection<ChatMember> Members => _members;

    private readonly ObservableCollection<ChatGroup> _groups = new();
    public ObservableCollection<ChatGroup> Groups => _groups;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _isSending;
    public bool IsSending
    {
        get => _isSending;
        set
        {
            if (SetProperty(ref _isSending, value))
            {
                OnPropertyChanged(nameof(IsSendEnabled));
            }
        }
    }

    private string _chatType = "dm";
    public string ChatType
    {
        get => _chatType;
        set
        {
            if (SetProperty(ref _chatType, value))
            {
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(IsSendEnabled));
                OnPropertyChanged(nameof(IsAllianceChat));

                // When switching to group chat, clear direct recipient
                if (_chatType == "group")
                {
                    SelectedMember = null;
                    // Do not force ChatGroup here; SelectedGroup will set ChatGroup when selected
                }
                else
                {
                    ChatGroup = null;
                }

                // reload messages for new chat type
                try { _ = LoadMessagesAsync(); } catch { }

                // start/stop polling appropriately
                if (_chatType == "dm")
                {
                    if (SelectedMember != null) StartPollingMessages(); else StopPollingMessages();
                }
                else if (_chatType == "group")
                {
                    // only poll for group chats when a group is selected
                    if (!string.IsNullOrEmpty(ChatGroup)) StartPollingMessages(); else StopPollingMessages();
                }
                else
                {
                    // other chat types (e.g., alliance) - poll by default
                    StartPollingMessages();
                }
            }
        }
    }

    // Helper property for UI to toggle alliance vs direct chat
    public bool IsAllianceChat
    {
        get => ChatType == "group"; // Treat the toggle as Group chat
        set
        {
            // setting this will update ChatType (which has additional side-effects)
            ChatType = value ? "group" : "dm";
            OnPropertyChanged(nameof(IsAllianceChat));

            // If switching to group chat, ensure groups are loaded
            if (value)
            {
                _ = LoadGroupsAsync();
            }
        }
    }

    private string? _chatUser;
    public string? ChatUser
    {
        get => _chatUser;
        set
        {
            if (SetProperty(ref _chatUser, value))
            {
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(IsSendEnabled));
            }
        }
    }

    private string? _chatGroup;
    public string? ChatGroup
    {
        get => _chatGroup;
        set
        {
            if (SetProperty(ref _chatGroup, value))
            {
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(IsSendEnabled));

                // When switching group, reload messages and (for simplicity) start polling
                try { _ = LoadMessagesAsync(); } catch { }
                if (!string.IsNullOrEmpty(_chatGroup)) StartPollingMessages();
            }
        }
    }

    private ChatMember? _selectedMember;
    public ChatMember? SelectedMember
    {
        get => _selectedMember;
        set
        {
            if (SetProperty(ref _selectedMember, value))
            {
                // Keep ChatUser in sync for backwards compatibility where code reads ChatUser string
                ChatUser = value?.Id.ToString();
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(IsSendEnabled));

                // reload messages immediately
                try { _ = LoadMessagesAsync(); } catch { }

                // start polling for new messages
                if (value != null)
                {
                    StartPollingMessages();
                }
                else
                {
                    StopPollingMessages();
                }
            }
        }
    }

    private ChatGroup? _selectedGroup;
    public ChatGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetProperty(ref _selectedGroup, value))
            {
                // Keep string ChatGroup in sync for API calls - use group name per API docs
                ChatGroup = value?.Name ?? value?.Title;
                // reload messages for selected group
                try { _ = LoadMessagesAsync(); } catch { }
            }
        }
    }

    private string _messageText = string.Empty;
    public string MessageText
    {
        get => _messageText;
        set
        {
            if (SetProperty(ref _messageText, value))
            {
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(IsSendEnabled));
            }
        }
    }

    // Computed: true when there's text and a selected recipient/group
    public bool CanSend => !string.IsNullOrWhiteSpace(MessageText) && (SelectedMember != null || !string.IsNullOrEmpty(ChatUser) || !string.IsNullOrEmpty(ChatGroup));

    // Button enabled state also considers sending in progress
    public bool IsSendEnabled => CanSend && !IsSending;

    private string? _currentUsername; // cached current user's username

    private async Task<string?> GetCurrentUsernameAsync()
    {
        if (!string.IsNullOrEmpty(_currentUsername)) return _currentUsername;
        try
        {
            _currentUsername = await _settingsService.GetUsernameAsync();
        }
        catch { }
        return _currentUsername;
    }

    public ChatViewModel(IApiService apiService, ICacheService cacheService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _settingsService = settingsService;
    }

    private void EnsureMessageReactionsInitialized(ChatMessage m)
    {
        if (m == null) return;
        if (m.Reactions == null)
        {
            m.Reactions = new ObservableCollection<ReactionSummary>();
        }
    }

    public void StartPollingMessages(int intervalMs =5000)
    {
        StopPollingMessages();
        _pollTimer = new System.Timers.Timer(intervalMs);
        _pollTimer.Elapsed += async (s, e) => await PollMessagesAsync();
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    public void StopPollingMessages()
    {
        try
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            _pollTimer = null;
        }
        catch { }
    }

    // Helper: robust comparison between server-sent sender string and current username
    private static bool AreSameUser(string? sender, string? currentUser)
    {
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(currentUser))
            return false;

        var a = sender.Trim().ToLowerInvariant();
        var b = currentUser.Trim().ToLowerInvariant();

        if (a == b) return true;

        // If either contains the other (handles cases like "Display Name (username)" or "username (Full Name)")
        if (a.Contains(b) || b.Contains(a)) return true;

        // If both are numeric ids, compare numerically
        if (long.TryParse(a, out var an) && long.TryParse(b, out var bn) && an == bn) return true;

        // Strip non-alphanumeric and compare
        var aNorm = new string(a.Where(char.IsLetterOrDigit).ToArray());
        var bNorm = new string(b.Where(char.IsLetterOrDigit).ToArray());
        if (!string.IsNullOrEmpty(aNorm) && aNorm == bNorm) return true;

        return false;
    }

    // Refresh IsFromCurrentUser for all loaded messages using cached or freshly read username
    private async Task RefreshOwnershipFlagsAsync()
    {
        try
        {
            var current = await GetCurrentUsernameAsync();
            if (string.IsNullOrEmpty(current)) return;

            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var m in _messages)
                {
                    m.IsFromCurrentUser = AreSameUser(m.Sender, current);
                }
            });
        }
        catch { }
    }

    private async Task PollMessagesAsync()
    {
        try
        {
            // only poll if a recipient is selected OR we're in a group chat with a selected group
            if (_chatType == "dm" && SelectedMember == null) return;
            if (_chatType == "group" && string.IsNullOrEmpty(ChatGroup)) return;

            ChatMessagesResponse? resp = null;

            if (_chatType == "group")
            {
                // Prepare group name variants like LoadMessagesAsync_Internal
                var groupVariants = new List<string?>();
                groupVariants.Add(ChatGroup);
                if (!string.IsNullOrEmpty(ChatGroup))
                {
                    var sanitized = ChatGroup.Replace('/', '_').Replace(' ', '_');
                    if (!groupVariants.Contains(sanitized)) groupVariants.Add(sanitized);

                    var lower = ChatGroup.ToLowerInvariant();
                    if (!groupVariants.Contains(lower)) groupVariants.Add(lower);

                    var urlEncoded = Uri.EscapeDataString(ChatGroup);
                    if (!groupVariants.Contains(urlEncoded)) groupVariants.Add(urlEncoded);

                    var sanitizedLower = sanitized.ToLowerInvariant();
                    if (!groupVariants.Contains(sanitizedLower)) groupVariants.Add(sanitizedLower);
                }

                var tryTypes = new[] { string.Empty, "group", "team", "alliance" };
                foreach (var t in tryTypes)
                {
                    foreach (var g in groupVariants)
                    {
                        try
                        {
                            var typeArg = string.IsNullOrEmpty(t) ? string.Empty : t;
                            // For group fetches do not pass a user parameter
                            var tryResp = await _apiService.GetChatMessagesAsync(typeArg, null, g);
                            if (tryResp != null && tryResp.Success && tryResp.Messages != null && tryResp.Messages.Count >0)
                            {
                                resp = tryResp;
                                // Persist the working group variant so future polls use it
                                if (g != null && g != ChatGroup)
                                {
                                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => ChatGroup = g);
                                }
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"PollMessages attempt failed for type='{t}' group='{g}': {ex.Message}");
                        }
                    }
                    if (resp != null) break;
                }
            }
            else
            {
                // For direct messages ensure we pass the other user's id and no group
                resp = await _apiService.GetChatMessagesAsync("dm", SelectedMember?.Id.ToString(), null);
            }

            if (resp != null && resp.Success && resp.Messages != null)
            {
                // messages returned newest-first per API; we want to display oldest-first
                var sorted = resp.Messages.OrderBy(m => m.Timestamp).ToList();
                // detect new messages by timestamp
                var newest = sorted.LastOrDefault();

                if (newest != null && (LastMessageTimestamp == null || newest.Timestamp > LastMessageTimestamp))
                {
                    // update UI on main thread for new/changed messages
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        _messages.Clear();
                        var currentUser = await GetCurrentUsernameAsync();
                        foreach (var m in sorted)
                        {
                            EnsureMessageReactionsInitialized(m);
                            m.IsFromCurrentUser = AreSameUser(m.Sender, currentUser);
                            _messages.Add(m);
                        }
                        LastMessageTimestamp = newest.Timestamp;
                    });
                }
                else
                {
                    // No new messages; still refresh reactions (and edited flag) for existing messages
                    // Build a lookup of server-side messages by Id for quick access
                    var serverById = sorted.ToDictionary(m => m.Id);

                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        var toRemove = new List<ChatMessage>();
                        var currentUser = await GetCurrentUsernameAsync();

                        // Update or mark for removal
                        foreach (var local in _messages.ToList())
                        {
                            if (!serverById.TryGetValue(local.Id, out var serverMsg))
                            {
                                // message no longer on server -> was deleted
                                toRemove.Add(local);
                                continue;
                            }

                            // Sync text/content if changed
                            if (!string.Equals(local.Text, serverMsg.Text, System.StringComparison.Ordinal))
                            {
                                local.Text = serverMsg.Text;
                            }

                            // Sync edited metadata
                            local.Edited = serverMsg.Edited;
                            local.EditedTimestamp = serverMsg.EditedTimestamp;

                            // Sync reactions
                            if (serverMsg.Reactions == null)
                                serverMsg.Reactions = new ObservableCollection<ReactionSummary>();

                            // Simple replace to trigger UI update
                            local.Reactions = new ObservableCollection<ReactionSummary>(serverMsg.Reactions.Select(r => new ReactionSummary { Emoji = r.Emoji, Count = r.Count }));

                            // Refresh ownership flag in case username became available/changed
                            local.IsFromCurrentUser = AreSameUser(local.Sender, currentUser);
                        }

                        // Remove deleted messages from local collection
                        foreach (var rem in toRemove)
                        {
                            if (_messages.Contains(rem)) _messages.Remove(rem);
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PollMessagesAsync failed: {ex.Message}");
        }
    }

    // Ensure polling stops when ViewModel is disposed (optional)
    public void Dispose()
    {
        StopPollingMessages();
    }

    // Ensure LoadMessagesAsync sets LastMessageTimestamp
    private async Task LoadMessagesAsync_Internal()
    {
        ChatMessagesResponse? resp = null;

        try
        {
            if (ChatType == "dm")
            {
                // Direct messages: require a selected member
                if (SelectedMember == null)
                {
                    // clear messages and exit
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => Messages.Clear());
                    LastMessageTimestamp = null;
                    return;
                }

                resp = await _apiService.GetChatMessagesAsync("dm", SelectedMember.Id.ToString(), null);
            }
            else if (ChatType == "group")
            {
                // Group messages: require selected group
                if (string.IsNullOrEmpty(ChatGroup))
                {
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => Messages.Clear());
                    LastMessageTimestamp = null;
                    return;
                }

                // Ask server for messages using group parameter; let ApiService omit 'type' so server can infer
                resp = await _apiService.GetChatMessagesAsync(type: string.Empty, user: null, group: ChatGroup);
            }
            else
            {
                // fallback to requesting by type only
                resp = await _apiService.GetChatMessagesAsync(ChatType, ChatUser, ChatGroup);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadMessagesAsync_Internal failed: {ex.Message}");
        }

        // Populate UI on main thread
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
        {
            Messages.Clear();
            if (resp != null && resp.Success && resp.Messages != null)
            {
                var sorted = resp.Messages.OrderBy(m => m.Timestamp).ToList();
                var currentUser = await GetCurrentUsernameAsync();
                foreach (var m in sorted)
                {
                    EnsureMessageReactionsInitialized(m);
                    m.IsFromCurrentUser = AreSameUser(m.Sender, currentUser);
                    Messages.Add(m);
                }
                LastMessageTimestamp = sorted.LastOrDefault()?.Timestamp;
            }
        });

        // If username may have been unavailable earlier, attempt a refresh to ensure flags are correct
        await RefreshOwnershipFlagsAsync();
    }

    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading messages...";
            await LoadMessagesAsync_Internal();
            StatusMessage = "";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMembersAsync()
    {
        try
        {
            StatusMessage = "Loading members...";
            System.Diagnostics.Debug.WriteLine("Chat: Fetching members for current user's scouting teams...");

            // Determine team numbers the current user is associated with
            var teamNumbers = new HashSet<int>();

            // Get current username so we can exclude it from the members list
            var currentUsername = await GetCurrentUsernameAsync();

            //1) Primary team from settings (the user's own team)
            try
            {
                var primaryTeam = await _settingsService.GetTeamNumberAsync();
                if (primaryTeam.HasValue)
                {
                    teamNumbers.Add(primaryTeam.Value);
                    System.Diagnostics.Debug.WriteLine($"Chat: Primary team from settings: {primaryTeam.Value}");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat: failed to read primary team from settings: {ex.Message}");
            }

            //2) Teams the user has scouted (from cached scouting entries where ScoutName matches current username)
            try
            {
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    var cachedScouting = await _cacheService.GetCachedScoutingDataAsync();
                    if (cachedScouting != null && cachedScouting.Count >0)
                    {
                        var scoutedTeams = cachedScouting
                            .Where(s => !string.IsNullOrEmpty(s.ScoutName) && s.ScoutName.Equals(currentUsername, System.StringComparison.OrdinalIgnoreCase))
                            .Select(s => s.TeamNumber)
                            .Distinct();

                        foreach (var t in scoutedTeams)
                        {
                            if (t >0) teamNumbers.Add(t);
                        }

                        System.Diagnostics.Debug.WriteLine($"Chat: Found {scoutedTeams.Count()} teams scouted by '{currentUsername}' in cache");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chat: failed to read scouted teams from cache: {ex.Message}");
            }

            Members.Clear();

            if (teamNumbers.Count ==0)
            {
                System.Diagnostics.Debug.WriteLine("Chat: No team numbers found for current user - falling back to API without team filter");
                // Try API without team filter to at least get some members
                var respAll = await _apiService.GetChatMembersAsync(scope: "team");
                if (respAll != null && respAll.Success && respAll.Members != null)
                {
                    foreach (var m in respAll.Members)
                    {
                        // Skip current user
                        if (!string.IsNullOrEmpty(currentUsername) && !string.IsNullOrEmpty(m.Username) && m.Username.Equals(currentUsername, System.StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Ensure DisplayName is populated for UI
                        if (string.IsNullOrWhiteSpace(m.DisplayName))
                        {
                            m.DisplayName = !string.IsNullOrWhiteSpace(m.Username) ? m.Username : m.Id.ToString();
                        }
                        if (!Members.Any(x => x.Id == m.Id)) Members.Add(m);
                    }

                    StatusMessage = $"Loaded {Members.Count} members";
                    System.Diagnostics.Debug.WriteLine($"Chat: Loaded {Members.Count} members (no user teams)");
                    return;
                }

                StatusMessage = respAll?.Error ?? "No members available";
                return;
            }

            // Query API for each team and aggregate unique members
            foreach (var tn in teamNumbers)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Chat: Requesting members for team {tn}");
                    var resp = await _apiService.GetChatMembersForTeamAsync(tn);
                    if (resp != null && resp.Success && resp.Members != null && resp.Members.Count >0)
                    {
                        foreach (var m in resp.Members)
                        {
                            // Skip current user
                            if (!string.IsNullOrEmpty(currentUsername) && !string.IsNullOrEmpty(m.Username) && m.Username.Equals(currentUsername, System.StringComparison.OrdinalIgnoreCase))
                                continue;

                            // Ensure DisplayName is populated for UI
                            if (string.IsNullOrWhiteSpace(m.DisplayName))
                            {
                                m.DisplayName = !string.IsNullOrWhiteSpace(m.Username) ? m.Username : m.Id.ToString();
                            }
                            if (!Members.Any(x => x.Id == m.Id))
                            {
                                Members.Add(m);
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"Chat: Added {resp.Members.Count} members from team {tn} (total now {Members.Count})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Chat: No members returned for team {tn} (Error: {resp?.Error})");
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Chat: Error fetching members for team {tn}: {ex.Message}");
                }
            }

            // Auto-select a default member if none selected (skip current user)
            if (SelectedMember == null && Members.Count >0)
            {
                var firstNonCurrent = Members.FirstOrDefault(m => string.IsNullOrEmpty(currentUsername) || string.IsNullOrEmpty(m.Username) || !m.Username.Equals(currentUsername, System.StringComparison.OrdinalIgnoreCase));
                if (firstNonCurrent != null)
                {
                    SelectedMember = firstNonCurrent;
                    System.Diagnostics.Debug.WriteLine($"Chat: Auto-selected member Id={SelectedMember.Id}, DisplayName='{SelectedMember.DisplayName}'");
                }
            }

            if (Members.Count >0)
            {
                System.Diagnostics.Debug.WriteLine("Chat: Final member list:");
                foreach (var mm in Members)
                {
                    System.Diagnostics.Debug.WriteLine($" - Id={mm.Id}, Username='{mm.Username}', DisplayName='{mm.DisplayName}', Team={mm.TeamNumber}");
                }
                StatusMessage = $"Loaded {Members.Count} members";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Chat: No members found after querying API/cache");
                StatusMessage = "No members found for your teams";
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadMembersAsync exception: {ex.Message}");
            StatusMessage = $"Error loading members: {ex.Message}";
        }
        finally
        {
            await Task.Delay(900);
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (IsSending) return;
        if (!CanSend)
        {
            StatusMessage = "Select a recipient and enter a message";
            await Task.Delay(1200);
            StatusMessage = string.Empty;
            return;
        }

        try
        {
            IsSending = true;
            StatusMessage = "Sending...";

            var req = new ChatSendRequest
            {
                Body = MessageText?.Trim() ?? string.Empty
            };

            // Prefer SelectedMember if present
            if (SelectedMember != null)
            {
                req.RecipientId = SelectedMember.Id;
            }
            else if (!string.IsNullOrEmpty(ChatUser) && int.TryParse(ChatUser, out var rid))
            {
                req.RecipientId = rid;
            }

            // If sending as group, set the 'group' field per API docs
            if (!string.IsNullOrEmpty(ChatGroup) && ChatType == "group")
            {
                req.Group = ChatGroup;
            }
            else if (!string.IsNullOrEmpty(ChatType) && ChatType != "dm")
            {
                // Fallback: use conversation_type for non-dm types (e.g., alliance)
                req.ConversationType = ChatType;
            }

            var resp = await _apiService.SendChatAsync(req);
            if (resp.Success && resp.Message != null)
            {
                // Ensure reactions collection exists
                EnsureMessageReactionsInitialized(resp.Message);

                // Mark message as from current user so UI shows delete immediately
                var currentUserForSent = await GetCurrentUsernameAsync();
                resp.Message.IsFromCurrentUser = AreSameUser(resp.Message.Sender, currentUserForSent);

                Messages.Add(resp.Message);
                MessageText = string.Empty;
                StatusMessage = "Message sent";
                LastMessageTimestamp = resp.Message.Timestamp;

                // Refresh messages to ensure history is up to date (fire-and-forget)
                try
                {
                    _ = LoadMessagesAsync();
                }
                catch { }
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to send message";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Send error: {ex.Message}";
        }
        finally
        {
            IsSending = false;
            await Task.Delay(800);
            StatusMessage = string.Empty;
        }
    }

    private ChatMessage? _selectedMessage;
    public ChatMessage? SelectedMessageForAction
    {
        get => _selectedMessage;
        set => SetProperty(ref _selectedMessage, value);
    }

    [RelayCommand]
    private async Task EditMessageAsync(ChatMessage message)
    {
        if (message == null)
        {
            StatusMessage = "No message selected to edit";
            await Task.Delay(1000);
            StatusMessage = string.Empty;
            return;
        }

        try
        {
            StatusMessage = "Editing message...";

            var req = new ChatEditRequest
            {
                MessageId = message.Id,
                Text = message.Text ?? string.Empty
            };

            var resp = await _apiService.EditChatMessageAsync(req);
            if (resp.Success)
            {
                // Mark locally as edited and set timestamp
                message.Edited = true;
                message.EditedTimestamp = DateTime.UtcNow;

                // Inform UI
                StatusMessage = "Message edited";

                // Optionally refresh messages to ensure server-canonical data
                try { _ = LoadMessagesAsync(); } catch { }
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to edit message";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Edit error: {ex.Message}";
        }
        finally
        {
            await Task.Delay(900);
            StatusMessage = string.Empty;
        }
    }

    // New: edit with explicit text provided by UI prompt
    public async Task EditMessageTextAsync(ChatMessage message, string newText)
    {
        if (message == null) return;
        if (string.IsNullOrWhiteSpace(newText)) return;

        try
        {
            StatusMessage = "Editing message...";

            var req = new ChatEditRequest
            {
                MessageId = message.Id,
                Text = newText.Trim()
            };

            var resp = await _apiService.EditChatMessageAsync(req);
            if (resp.Success)
            {
                // Update local model on main thread
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    message.Text = req.Text;
                    message.Edited = true;
                    message.EditedTimestamp = DateTime.UtcNow;
                });

                StatusMessage = "Message edited";

                // Optionally refresh messages
                try { _ = LoadMessagesAsync(); } catch { }
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to edit message";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Edit error: {ex.Message}";
        }
        finally
        {
            await Task.Delay(900);
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(ChatMessage message)
    {
        if (message == null)
        {
            StatusMessage = "No message selected to delete";
            await Task.Delay(1000);
            StatusMessage = string.Empty;
            return;
        }

        try
        {
            StatusMessage = "Deleting message...";

            var req = new ChatDeleteRequest { MessageId = message.Id };
            var resp = await _apiService.DeleteChatMessageAsync(req);
            if (resp.Success)
            {
                // Remove locally
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Messages.Contains(message)) Messages.Remove(message);
                });

                StatusMessage = "Message deleted";

                try { _ = LoadMessagesAsync(); } catch { }
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to delete message";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Delete error: {ex.Message}";
        }
        finally
        {
            await Task.Delay(900);
            StatusMessage = string.Empty;
        }
    }

    // New: react to a message (toggle). Calls API and updates local message.Reactions
    public async Task ReactToMessageAsync(ChatMessage message, string emoji)
    {
        if (message == null) return;
        if (string.IsNullOrWhiteSpace(emoji)) return;

        try
        {
            StatusMessage = "Updating reaction...";

            var req = new ChatReactRequest
            {
                MessageId = message.Id,
                Emoji = emoji
            };

            var resp = await _apiService.ReactToChatMessageAsync(req);
            if (resp.Success && resp.Reactions != null)
            {
                // Update local message reactions on main thread
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Replace with ObservableCollection so UI receives change notifications
                    message.Reactions = new ObservableCollection<ReactionSummary>(resp.Reactions.Select(r => new ReactionSummary { Emoji = r.Emoji, Count = r.Count }));
                    // Ensure UI knows about property change
                    OnPropertyChanged(nameof(Messages));
                });

                StatusMessage = "Reaction updated";
            }
            else
            {
                StatusMessage = resp.Error ?? "Failed to update reaction";
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"React error: {ex.Message}";
        }
        finally
        {
            await Task.Delay(900);
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ReactMessageAsync(ChatMessage message)
    {
        if (message == null) return;
        // Default quick reaction when invoked from swipe menu
        await ReactToMessageAsync(message, "👍");
    }

    // Public wrappers for code-behind usage
    public Task LoadMessagesAsyncPublic() => LoadMessagesAsync();
    public Task LoadMembersAsyncPublic() => LoadMembersAsync();
    public Task SendMessageAsyncPublic() => SendMessageAsync();
    public Task DeleteMessageAsyncPublic(ChatMessage message) => DeleteMessageAsync(message);
    public Task LoadGroupsAsyncPublic() => LoadGroupsAsync();
    public Task<ChatCreateGroupResponse> CreateGroupAsyncPublic(string title, List<string>? membersUsernames = null) => CreateGroupAsync(title, membersUsernames);

    // Expose current username for view code that may need it
    public Task<string?> GetCurrentUsernamePublic() => GetCurrentUsernameAsync();

    [RelayCommand]
    private async Task LoadGroupsAsync()
    {
        try
        {
            StatusMessage = "Loading groups...";
            var resp = await _apiService.GetChatGroupsAsync();
            Groups.Clear();
            if (resp != null && resp.Success && resp.Groups != null)
            {
                foreach (var g in resp.Groups)
                {
                    Groups.Add(g);
                }

                // Auto-select a default group when in group chat mode
                if (ChatType == "group" && SelectedGroup == null && Groups.Count >0)
                {
                    // Prefer a group where the current user is a member
                    var preferred = Groups.FirstOrDefault(x => x.IsMember) ?? Groups[0];
                    SelectedGroup = preferred;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadGroupsAsync failed: {ex.Message}");
        }
        finally
        {
            StatusMessage = string.Empty;
        }
    }

    public async Task<ChatCreateGroupResponse> CreateGroupAsync(string title, List<string>? membersUsernames = null)
    {
        try
        {
            var req = new ChatCreateGroupRequest { Group = title ?? string.Empty, Members = membersUsernames };
            var resp = await _apiService.CreateChatGroupAsync(req);
            if (resp != null && resp.Success && resp.Group != null)
            {
                // Add to local collection and select
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    Groups.Add(resp.Group);
                    SelectedGroup = resp.Group;
                });
            }
            return resp ?? new ChatCreateGroupResponse { Success = false, Error = "Invalid response" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateGroupAsync failed: {ex.Message}");
            return new ChatCreateGroupResponse { Success = false, Error = ex.Message };
        }
    }
}
