using ObsidianScout.ViewModels;
using ObsidianScout.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Collections;
using System.Windows.Input;

namespace ObsidianScout.Views;

// Add QueryProperty attributes to handle deep link parameters
[QueryProperty(nameof(SourceType), "sourceType")]
[QueryProperty(nameof(SourceId), "sourceId")]
public partial class ChatPage : ContentPage
{
 private bool _membersLoaded = false;
 private ChatViewModel? _vm;

 // Properties for query parameters from deep link
 private string? _sourceType;
 private string? _sourceId;

 public string? SourceType
 {
  get => _sourceType;
     set
        {
   _sourceType = value;
 System.Diagnostics.Debug.WriteLine($"[ChatPage] SourceType set to: {value}");
      HandleDeepLink();
 }
    }

    public string? SourceId
 {
   get => _sourceId;
  set
      {
     _sourceId = value;
   System.Diagnostics.Debug.WriteLine($"[ChatPage] SourceId set to: {value}");
            HandleDeepLink();
}
 }

 public ICommand ReactSwipeCommand { get; }

 // Allow viewModel to be optional so page works when DI isn't resolving it
 public ChatPage(ChatViewModel? viewModel = null)
 {
 InitializeComponent();

 ReactSwipeCommand = new Command<object>(async (param) => await OnReactSwipeCommand(param));

 ChatViewModel vm = viewModel;
 if (vm == null)
 {
 var services = Application.Current?.Handler?.MauiContext?.Services;
 if (services != null)
 {
 var api = services.GetService<IApiService>();
 var cache = services.GetService<ICacheService>();
 var settings = services.GetService<ISettingsService>();
 // If ApiService not registered for some reason, throw a helpful debug message
 if (api == null)
 {
 System.Diagnostics.Debug.WriteLine("ChatPage: IApiService not registered in DI");
 }
 if (cache == null)
 {
 System.Diagnostics.Debug.WriteLine("ChatPage: ICacheService not registered in DI");
 }
 if (settings == null)
 {
 System.Diagnostics.Debug.WriteLine("ChatPage: ISettingsService not registered in DI");
 }
 vm = new ChatViewModel(
 api ?? throw new System.InvalidOperationException("IApiService required"),
 cache ?? throw new System.InvalidOperationException("ICacheService required"),
 settings ?? throw new System.InvalidOperationException("ISettingsService required")
 );
 }
 else
 {
 // As last resort create minimal ChatViewModel with default services (will likely fail earlier)
 throw new System.InvalidOperationException("Unable to resolve services to create ChatViewModel");
 }
 }

 BindingContext = vm;
 _vm = vm;
 vm.LoadMessagesCommand.Execute(null);

 // subscribe to message collection changes to auto-scroll
 _vm.Messages.CollectionChanged += Messages_CollectionChanged;

 // subscribe to last message timestamp changes for additional scroll trigger
 _vm.PropertyChanged += Vm_PropertyChanged;

 // Use Appearing event to load members once
 this.Appearing += async (s, e) =>
 {
 if (!_membersLoaded && BindingContext is ChatViewModel cvm)
 {
 _membersLoaded = true;
 try
 {
 await cvm.LoadMembersCommand.ExecuteAsync(null);
 }
 catch (System.Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"ChatPage Appearing: failed to load members: {ex.Message}");
 }

 // Also attempt to load groups so group picker has data
 try
 {
 await cvm.LoadGroupsCommand.ExecuteAsync(null);
 }
 catch (System.Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"ChatPage Appearing: failed to load groups: {ex.Message}");
 }
 }

 // Ensure polling is started when page appears (5s interval)
 try
 {
 _vm?.StartPollingMessages(5000);
 }
 catch (System.Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"ChatPage Appearing: failed to start polling: {ex.Message}");
 }
 
 // Mark messages as read when page appears and messages exist
 await Task.Delay(1000); // Give messages time to load
 MarkMessagesAsRead();
 };

 // Stop polling when page disappears to avoid background network activity
 this.Disappearing += (s, e) =>
 {
 try
 {
 _vm?.StopPollingMessages();
 }
 catch (System.Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"ChatPage Disappearing: failed to stop polling: {ex.Message}");
 }
 
 // Mark as read when leaving page
 MarkMessagesAsRead();
 };
 }

    // New method to mark messages as read
    private void MarkMessagesAsRead()
    {
        try
        {
      if (_vm == null || _vm.Messages == null || _vm.Messages.Count == 0)
        return;
    
            // Get the last message ID
    var lastMessage = _vm.Messages.LastOrDefault();
  if (lastMessage == null || string.IsNullOrEmpty(lastMessage.Id))
      return;
       
   // Determine conversation ID based on chat type
          string? conversationId = null;
      
            if (_vm.ChatType == "dm" && _vm.SelectedMember != null)
 {
     conversationId = $"dm_{_vm.SelectedMember.Username}";
     }
            else if (_vm.ChatType == "group" && _vm.SelectedGroup != null)
       {
        conversationId = $"group_{_vm.SelectedGroup.Name}";
          }
  
          if (string.IsNullOrEmpty(conversationId))
            return;
    
         // Call API to mark as read (fire and forget)
         _ = MarkAsReadAsync(conversationId, lastMessage.Id);
        }
        catch (Exception ex)
        {
         System.Diagnostics.Debug.WriteLine($"[ChatPage] MarkMessagesAsRead error: {ex.Message}");
}
    }
    
    private async Task MarkAsReadAsync(string conversationId, string lastMessageId)
    {
        try
    {
        System.Diagnostics.Debug.WriteLine($"[ChatPage] Marking messages as read: {conversationId}, last: {lastMessageId}");
  
   // Get API service
var services = Application.Current?.Handler?.MauiContext?.Services;
            if (services == null) return;
          
        var apiService = services.GetService<IApiService>();
            if (apiService == null) return;
            
            // Call API
     var result = await apiService.MarkChatMessagesAsReadAsync(conversationId, lastMessageId);
            
          if (result.Success)
   {
            System.Diagnostics.Debug.WriteLine($"[ChatPage] ✓ Messages marked as read successfully");
            }
  else
            {
       System.Diagnostics.Debug.WriteLine($"[ChatPage] ✗ Failed to mark as read: {result.Error}");
     }
        }
        catch (Exception ex)
 {
         System.Diagnostics.Debug.WriteLine($"[ChatPage] MarkAsReadAsync error: {ex.Message}");
    }
    }

    private void HandleDeepLink()
    {
  // Only handle when both parameters are set
 if (string.IsNullOrEmpty(_sourceType) || string.IsNullOrEmpty(_sourceId) || _vm == null)
         return;

  try
     {
  System.Diagnostics.Debug.WriteLine($"[ChatPage] Handling deep link: {_sourceType}/{_sourceId}");

    MainThread.BeginInvokeOnMainThread(async () =>
   {
 try
    {
    // Give UI time to initialize
    await Task.Delay(500);

    if (_sourceType == "dm")
   {
       // Open DM with specific user
     System.Diagnostics.Debug.WriteLine($"[ChatPage] Opening DM with user: {_sourceId}");
  
          // Find the user in members list
  var member = _vm.Members.FirstOrDefault(m => 
    string.Equals(m.Username, _sourceId, StringComparison.OrdinalIgnoreCase));

  if (member != null)
    {
       _vm.SelectedMember = member;
  _vm.ChatType = "dm";
      await _vm.LoadMessagesCommand.ExecuteAsync(null);
    System.Diagnostics.Debug.WriteLine($"[ChatPage] ✓ Opened DM with {_sourceId}");
    }
            else
     {
  System.Diagnostics.Debug.WriteLine($"[ChatPage] User {_sourceId} not found in members list");
      // Try reloading members in case they weren't loaded yet
      await _vm.LoadMembersCommand.ExecuteAsync(null);
   await Task.Delay(200);
      
       // Try again
   member = _vm.Members.FirstOrDefault(m => 
         string.Equals(m.Username, _sourceId, StringComparison.OrdinalIgnoreCase));
  
          if (member != null)
          {
      _vm.SelectedMember = member;
            _vm.ChatType = "dm";
  await _vm.LoadMessagesCommand.ExecuteAsync(null);
 System.Diagnostics.Debug.WriteLine($"[ChatPage] ✓ Opened DM with {_sourceId} after reload");
   }
  else
  {
    System.Diagnostics.Debug.WriteLine($"[ChatPage] User {_sourceId} still not found after reload");
       }
   }
       }
   else if (_sourceType == "group")
  {
     // Open specific group
        System.Diagnostics.Debug.WriteLine($"[ChatPage] Opening group: {_sourceId}");
 
 // Find the group in groups list
   var group = _vm.Groups.FirstOrDefault(g => 
       string.Equals(g.Name, _sourceId, StringComparison.OrdinalIgnoreCase));

        if (group != null)
     {
      _vm.SelectedGroup = group;
_vm.ChatType = "group";
      await _vm.LoadMessagesCommand.ExecuteAsync(null);
 System.Diagnostics.Debug.WriteLine($"[ChatPage] ✓ Opened group {_sourceId}");
            }
      else
 {
  System.Diagnostics.Debug.WriteLine($"[ChatPage] Group {_sourceId} not found in groups list");
  // Try reloading groups
  await _vm.LoadGroupsCommand.ExecuteAsync(null);
     await Task.Delay(200);
       
   // Try again
         group = _vm.Groups.FirstOrDefault(g => 
            string.Equals(g.Name, _sourceId, StringComparison.OrdinalIgnoreCase));
         
 if (group != null)
 {
        _vm.SelectedGroup = group;
      _vm.ChatType = "group";
      await _vm.LoadMessagesCommand.ExecuteAsync(null);
            System.Diagnostics.Debug.WriteLine($"[ChatPage] ✓ Opened group {_sourceId} after reload");
    }
  else
       {
        System.Diagnostics.Debug.WriteLine($"[ChatPage] Group {_sourceId} still not found after reload");
          }
     }
       }

// Clear parameters after handling
             _sourceType = null;
    _sourceId = null;
      }
  catch (Exception ex)
     {
    System.Diagnostics.Debug.WriteLine($"[ChatPage] Error in deep link handler: {ex.Message}");
       }
    });
 }
    catch (Exception ex)
     {
  System.Diagnostics.Debug.WriteLine($"[ChatPage] HandleDeepLink error: {ex.Message}");
        }
    }

 private async Task OnReactSwipeCommand(object? parameter)
 {
 try
 {
 if (parameter is ObsidianScout.Models.ChatMessage msg && BindingContext is ChatViewModel vm)
 {
 var emoji = await DisplayActionSheet("React with", "Cancel", null, "👍", "❤️", "😂", "🎉", "😮", "😢");
 if (!string.IsNullOrEmpty(emoji) && emoji != "Cancel")
 {
 await vm.ReactToMessageAsync(msg, emoji);
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"OnReactSwipeCommand failed: {ex.Message}");
 }
 }

 private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
 {
 if (e.PropertyName == nameof(ChatViewModel.LastMessageTimestamp))
 {
 MainThread.BeginInvokeOnMainThread(() => ScrollToBottom());
 }
 }

 private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
 {
 // When items added, scroll to bottom
 if (e.Action == NotifyCollectionChangedAction.Add)
 {
 MainThread.BeginInvokeOnMainThread(() =>
 {
 ScrollToBottom();
 // Mark as read when new messages arrive
 MarkMessagesAsRead();
 });
 }
 }

 private void ScrollToBottom()
 {
 try
 {
 if (MessagesList == null) return;
 var items = MessagesList.ItemsSource as IList;
 if (items == null || items.Count ==0) return;
 var last = items[items.Count -1];
 MessagesList.ScrollTo(last, position: ScrollToPosition.End, animate: true);
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"ScrollToBottom failed: {ex.Message}");
 }
 }

 // Handler for message tap/long-press to show Edit/Delete options
 public async void OnMessageTapped(object sender, EventArgs e)
 {
 try
 {
 if (sender is VisualElement ve && ve.BindingContext is ObsidianScout.Models.ChatMessage msg && BindingContext is ChatViewModel vm)
 {
 // Build action list based on ownership
 var actions = new List<string>();
 if (msg.IsFromCurrentUser)
 {
 actions.Add("Edit");
 }
 actions.Add("React");
 if (msg.IsFromCurrentUser)
 {
 actions.Add("Delete");
 }

 // Show platform-appropriate action sheet
 var choice = await DisplayActionSheet("Message actions", "Cancel", null, actions.ToArray());
 if (choice == "Edit")
 {
 // Double-check ownership before allowing edit
 if (!msg.IsFromCurrentUser)
 {
 await DisplayAlert("Not allowed", "You can only edit your own messages.", "OK");
 return;
 }

 // Prompt user for new text
 var newText = await DisplayPromptAsync("Edit message", "Update message text:", "Save", "Cancel", placeholder: "Message", maxLength:4000, initialValue: msg.Text);
 if (newText != null)
 {
 // If user didn't change text, do nothing
 if (!string.Equals(newText.Trim(), msg.Text?.Trim() ?? string.Empty, System.StringComparison.Ordinal))
 {
 // Use new helper to send edit and update local model
 await vm.EditMessageTextAsync(msg, newText);
 }
 }
 }
 else if (choice == "Delete")
 {
 // Double-check ownership before allowing delete
 if (!msg.IsFromCurrentUser)
 {
 await DisplayAlert("Not allowed", "You can only delete your own messages.", "OK");
 return;
 }

 if (vm.DeleteMessageCommand.CanExecute(msg))
 {
 await vm.DeleteMessageCommand.ExecuteAsync(msg);
 }
 }
 else if (choice == "React")
 {
 // show simple emoji choices
 var emoji = await DisplayActionSheet("React with", "Cancel", null, "👍", "❤️", "😂", "🎉", "😮", "😢");
 if (!string.IsNullOrEmpty(emoji) && emoji != "Cancel")
 {
 // Call VM to react
 await vm.ReactToMessageAsync(msg, emoji);
 }
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"OnMessageTapped failed: {ex.Message}");
 }
 }

 // Overload matching TappedEventArgs so XAML loader can find it
 public async void OnMessageTapped(object sender, TappedEventArgs e)
 {
 // Delegate to the main handler
 OnMessageTapped(sender, (EventArgs)e);
 }

 // Handler for SwipeItem "React" invoked - use EventArgs signature so XAML can bind
 public async void OnMessageReactInvoked(object sender, EventArgs e)
 {
 try
 {
 // The SwipeItem's BindingContext will be the ChatMessage
 if (sender is SwipeItem si && si.BindingContext is ObsidianScout.Models.ChatMessage msg && BindingContext is ChatViewModel vm)
 {
 var emoji = await DisplayActionSheet("React with", "Cancel", null, "👍", "❤️", "😂", "🎉", "😮", "😢");
 if (!string.IsNullOrEmpty(emoji) && emoji != "Cancel")
 {
 await vm.ReactToMessageAsync(msg, emoji);
 }
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"OnMessageReactInvoked failed: {ex.Message}");
 }
 }

 private void OnEntryCompleted(object sender, EventArgs e)
 {
 // Execute the SendMessageCommand when the entry is completed (Enter pressed)
 if (BindingContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
 {
 vm.SendMessageCommand.Execute(null);
 }
 }

 // New group creation handler
 public async void OnCreateGroupClicked(object sender, EventArgs e)
 {
 try
 {
 if (BindingContext is not ChatViewModel vm)
 return;

 // Prompt for group name
 var title = await DisplayPromptAsync("Create group", "Enter group name:", "Create", "Cancel", placeholder: "group_name", maxLength:100);
 if (string.IsNullOrWhiteSpace(title))
 return;

 // Ask if user wants to include all team members or only themselves
 var choice = await DisplayActionSheet("Add members", "Cancel", null, "All team members", "Only me");
 if (choice == "Cancel" || string.IsNullOrEmpty(choice))
 return;

 List<string> members = new();

 if (choice == "All team members")
 {
 // Use usernames from ViewModel.Members; ensure current user included
 try
 {
 foreach (var m in vm.Members)
 {
 if (!string.IsNullOrWhiteSpace(m.Username) && !members.Contains(m.Username))
 members.Add(m.Username);
 }
 }
 catch { }

 // If no members found, fall back to only current user
 if (members.Count ==0)
 {
 var cur = await vm.GetCurrentUsernamePublic();
 if (!string.IsNullOrWhiteSpace(cur)) members.Add(cur);
 }
 }
 else
 {
 var cur = await vm.GetCurrentUsernamePublic();
 if (!string.IsNullOrWhiteSpace(cur)) members.Add(cur);
 }

 // Call ViewModel to create group
 var resp = await vm.CreateGroupAsyncPublic(title.Trim(), members);
 if (resp != null && resp.Success)
 {
 await DisplayAlert("Group created", $"Group '{title}' created successfully.", "OK");
 // Refresh groups list
 await vm.LoadGroupsAsyncPublic();
 }
 else
 {
 var err = resp?.Error ?? "Unknown error";
 await DisplayAlert("Create failed", $"Failed to create group: {err}", "OK");
 }
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"OnCreateGroupClicked failed: {ex.Message}");
 await DisplayAlert("Error", "Failed to create group", "OK");
 }
 }
}