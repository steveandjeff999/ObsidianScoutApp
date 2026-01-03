using System;
using System.Linq;
using Microsoft.Maui.Controls;
using ObsidianScout.ViewModels;
using ObsidianScout.Converters;
using ObsidianScout.Models;
using System.Text.Json;
using System.IO;
using ObsidianScout.Services;

namespace ObsidianScout.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel? _vm;

    public HistoryPage()
    {
        InitializeComponent();
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            if (services != null && services.GetService(typeof(HistoryViewModel)) is HistoryViewModel resolved)
            {
                _vm = resolved;
            }
        }
        catch
        {
            // ignore
        }

        BindingContext = _vm;
        // end constructor
    }

    public async void OnUploadEntryClicked(object sender, EventArgs e)
    {
        try
        {
                if (sender is Button btn && btn.CommandParameter is ScoutingEntry se)
            {
                    // If entry has already been uploaded, prevent local editing
                    try
                    {
                        if (se.IsUploaded)
                        {
                            await DisplayAlert("Read-only", "This entry has been uploaded. To edit it, update the record on the server.", "OK");
                            return;
                        }
                        if (se.IsPending)
                        {
                            await DisplayAlert("Pending", "This entry is pending upload and cannot be edited or re-uploaded.", "OK");
                            return;
                        }
                    }
                    catch { }
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null && services.GetService(typeof(IApiService)) is IApiService api && services.GetService(typeof(ICacheService)) is ICacheService cache)
                {
                    // Build submission
                    var submission = new ScoutingSubmission
                    {
                        TeamId = se.TeamId,
                        MatchId = se.MatchId,
                        Data = se.Data ?? new Dictionary<string, object?>(),
                        OfflineId = se.OfflineId
                    };

                    try
                    {
                        // mark uploading and refresh UI
                        se.UploadInProgress = true;
                        if (BindingContext is HistoryViewModel vm1)
                        {
                            var existing1 = vm1.AllScouting.FirstOrDefault(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                            if (existing1 != null)
                            {
                                var i1 = vm1.AllScouting.IndexOf(existing1);
                                vm1.AllScouting[i1] = se;
                            }
                        }

                        var res = await api.SubmitScoutingDataAsync(submission);
                        if (res.Success)
                        {
                            // remove from pending cache
                            await cache.RemovePendingScoutingAsync(x => x.OfflineId == se.OfflineId || x.Id == se.Id);
                                // clear uploading flag and local-change marker
                                se.UploadInProgress = false;
                                se.HasLocalChanges = false;
                            // update id if server returned one
                            if (res.ScoutingId > 0) se.Id = res.ScoutingId;
                            // clear offline id now that server has it
                            se.OfflineId = string.Empty;
                            // update cached list
                            try
                            {
                                var cached = await cache.GetCachedScoutingDataAsync();
                                if (cached == null) cached = new List<ScoutingEntry>();
                                var idx = cached.FindIndex(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                                if (idx >= 0) cached[idx] = se; else cached.Add(se);
                                await cache.CacheScoutingDataAsync(cached);
                            }
                            catch { }

                            // update UI
                            if (BindingContext is HistoryViewModel vm)
                            {
                                var existing = vm.AllScouting.FirstOrDefault(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                                if (existing != null)
                                {
                                    var i = vm.AllScouting.IndexOf(existing);
                                    vm.AllScouting[i] = se;
                                }
                                else vm.AllScouting.Insert(0, se);
                            }
                        }
                        else
                        {
                            // upload failed - surface server error details
                            se.UploadInProgress = false;
                            if (BindingContext is HistoryViewModel vm2)
                            {
                                var existing2 = vm2.AllScouting.FirstOrDefault(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                                if (existing2 != null)
                                {
                                    var i2 = vm2.AllScouting.IndexOf(existing2);
                                    vm2.AllScouting[i2] = se;
                                }
                            }

                            var details = new System.Text.StringBuilder();
                            if (!string.IsNullOrEmpty(res.Message)) details.AppendLine(res.Message);
                            if (!string.IsNullOrEmpty((res as dynamic).Error)) details.AppendLine((string)((res as dynamic).Error ?? string.Empty));
                            try { if (!string.IsNullOrEmpty((res as dynamic).ErrorCode)) details.AppendLine((string)((res as dynamic).ErrorCode ?? string.Empty)); } catch { }
                            var msg = details.Length > 0 ? details.ToString().Trim() : "Upload failed";
                            await DisplayAlert("Upload Failed", msg, "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[History] UploadEntry failed: {ex.Message}");
                        await DisplayAlert("Error", "Upload failed.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] OnUploadEntryClicked error: {ex.Message}");
        }
    }

    public async void OnUploadPitEntryClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.CommandParameter is PitScoutingEntry pe)
            {
                // If pit entry was uploaded or pending, prevent uploading/editing here
                try
                {
                    if (pe.Id > 0)
                    {
                        await DisplayAlert("Read-only", "This pit entry has been uploaded. To edit it, update the record on the server.", "OK");
                        return;
                    }
                    if (pe.IsPending)
                    {
                        await DisplayAlert("Pending", "This pit entry is pending upload and cannot be edited locally.", "OK");
                        return;
                    }
                }
                catch { }
                var services = Application.Current?.Handler?.MauiContext?.Services;
                if (services != null && services.GetService(typeof(IApiService)) is IApiService api && services.GetService(typeof(ICacheService)) is ICacheService cache)
                {
                    var submission = new PitScoutingSubmission
                    {
                        TeamId = pe.TeamId,
                        Data = pe.Data ?? new Dictionary<string, object?>(),
                        Images = pe.Images
                    };

                    try
                    {
                        // set uploading
                        pe.UploadInProgress = true;
                        if (BindingContext is HistoryViewModel pvm1)
                        {
                            var existing1 = pvm1.AllPit.FirstOrDefault(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                            if (existing1 != null) pvm1.AllPit[pvm1.AllPit.IndexOf(existing1)] = pe;
                        }

                        var res = await api.SubmitPitScoutingDataAsync(submission);
                        if (res.Success)
                        {
                            await cache.RemovePendingPitAsync(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                            pe.HasLocalChanges = false;
                            pe.UploadInProgress = false;
                            if (BindingContext is HistoryViewModel vm)
                            {
                                var existing = vm.AllPit.FirstOrDefault(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                                if (existing != null)
                                {
                                    var i = vm.AllPit.IndexOf(existing);
                                    vm.AllPit[i] = pe;
                                }
                                else vm.AllPit.Insert(0, pe);
                            }
                        }
                        else
                        {
                            pe.UploadInProgress = false;
                            if (BindingContext is HistoryViewModel pvm2)
                            {
                                var existing2 = pvm2.AllPit.FirstOrDefault(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                                if (existing2 != null) pvm2.AllPit[pvm2.AllPit.IndexOf(existing2)] = pe;
                            }

                            var details = new System.Text.StringBuilder();
                            if (!string.IsNullOrEmpty((res as dynamic).Message)) details.AppendLine((string)((res as dynamic).Message ?? string.Empty));
                            if (!string.IsNullOrEmpty((res as dynamic).Error)) details.AppendLine((string)((res as dynamic).Error ?? string.Empty));
                            try { if (!string.IsNullOrEmpty((res as dynamic).ErrorCode)) details.AppendLine((string)((res as dynamic).ErrorCode ?? string.Empty)); } catch { }
                            var msg = details.Length > 0 ? details.ToString().Trim() : "Upload failed";
                            await DisplayAlert("Upload Failed", msg, "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[History] UploadPitEntry failed: {ex.Message}");
                        await DisplayAlert("Error", "Upload failed.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] OnUploadPitEntryClicked error: {ex.Message}");
        }
    }
    public HistoryPage(HistoryViewModel vm) : this()
    {
        if (vm != null)
        {
            _vm = vm;
            BindingContext = _vm;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm != null) await _vm.LoadAsync();
    }

    public async void OnViewEntryClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.CommandParameter is ScoutingEntry se)
            {
                // Build friendly name map (best-effort)
                Dictionary<string, string> idToName = new(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null && services.GetService(typeof(ICacheService)) is ICacheService cache)
                    {
                        var cfg = await cache.GetCachedGameConfigAsync();
                        if (cfg != null)
                        {
                            void AddElements(IEnumerable<ScoringElement>? elems)
                            {
                                if (elems == null) return;
                                foreach (var el in elems)
                                {
                                    if (string.IsNullOrEmpty(el.Id)) continue;
                                    if (!idToName.ContainsKey(el.Id)) idToName[el.Id] = string.IsNullOrEmpty(el.Name) ? el.Id : el.Name;
                                }
                            }

                            AddElements(cfg.AutoPeriod?.ScoringElements);
                            AddElements(cfg.TeleopPeriod?.ScoringElements);
                            AddElements(cfg.EndgamePeriod?.ScoringElements);
                            if (cfg.PostMatch != null)
                            {
                                foreach (var r in cfg.PostMatch.RatingElements) if (!string.IsNullOrEmpty(r.Id) && !idToName.ContainsKey(r.Id)) idToName[r.Id] = r.Name;
                                foreach (var t in cfg.PostMatch.TextElements) if (!string.IsNullOrEmpty(t.Id) && !idToName.ContainsKey(t.Id)) idToName[t.Id] = t.Name;
                            }
                        }
                    }
                }
                catch { }

                var detail = new StackLayout { Padding = 12, Spacing = 8 };

                // Team and Match pickers (like scouting form)
                var teamLabel = new Label { Text = "Team", FontAttributes = FontAttributes.Bold, FontSize = 14 };
                var teamPicker = new Picker { Title = "Select Team", WidthRequest = 220 };
                var matchLabel = new Label { Text = "Match", FontAttributes = FontAttributes.Bold, FontSize = 14 };
                var matchPicker = new Picker { Title = "Select Match", WidthRequest = 220 };

                // Populate pickers from API/cache
                var teamsList = new List<Team>();
                var matchesList = new List<Match>();
                try
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null)
                    {
                        if (services.GetService(typeof(IApiService)) is IApiService api)
                        {
                            try
                            {
                                var tres = await api.GetTeamsAsync(limit: 500);
                                if (tres != null && tres.Success && tres.Teams != null) teamsList = tres.Teams;
                            }
                            catch { }

                            try
                            {
                                // request matches for the same event if available
                                var evt = se.EventId;
                                var mres = await api.GetMatchesAsync(evt);
                                if (mres != null && mres.Matches != null && mres.Matches.Count > 0) matchesList = mres.Matches;
                            }
                            catch { }
                        }

                        if ((teamsList == null || teamsList.Count == 0) && services.GetService(typeof(ICacheService)) is ICacheService cache)
                        {
                            try { var cached = await cache.GetCachedTeamsAsync(); if (cached != null) teamsList = cached; } catch { }
                        }

                        if ((matchesList == null || matchesList.Count == 0) && services.GetService(typeof(ICacheService)) is ICacheService cache2)
                        {
                            try { var cachedMatches = await cache2.GetCachedMatchesAsync(); if (cachedMatches != null) matchesList = cachedMatches; } catch { }
                        }
                    }
                }
                catch { }

                teamPicker.ItemsSource = teamsList;
                teamPicker.ItemDisplayBinding = new Binding(".", converter: new FuncConverter<Team, string>(t => t != null ? $"{t.TeamNumber} - {t.TeamName}" : string.Empty));
                matchPicker.ItemsSource = matchesList;
                matchPicker.ItemDisplayBinding = new Binding(".", converter: new FuncConverter<Match, string>(m => m != null ? $"{m.MatchType} {m.MatchNumber}" : string.Empty));

                // If entry already uploaded or pending, make selectors read-only
                try { teamPicker.IsEnabled = !(se.IsUploaded || se.IsPending); matchPicker.IsEnabled = !(se.IsUploaded || se.IsPending); } catch { }

                // Prepare page variable so handlers can update the title at runtime
                var page = new ContentPage { Title = $"Edit {se.TeamNumber}/{se.MatchNumber}", Content = new ScrollView { Content = detail } };

                // Controls map will be populated after parsing JSON; declare here so handlers can update controls in real-time
                Dictionary<string, View>? controls = null;

                // Immediately apply changes when user picks a new team/match so the model and title reflect selection
                teamPicker.SelectedIndexChanged += (s, ea) =>
                {
                    try
                    {
                        if (teamPicker.SelectedItem is Team selTeam)
                        {
                            se.TeamNumber = selTeam.TeamNumber;
                            se.TeamId = selTeam.Id;
                            // update title to reflect change
                            try { page.Title = $"Edit {se.TeamNumber}/{se.MatchNumber}"; } catch { }
                            // ensure data dictionary reflects selection
                            try
                            {
                                if (se.Data == null) se.Data = new Dictionary<string, object>();
                                se.Data["team_id"] = se.TeamId;
                                se.Data["team_number"] = se.TeamNumber;
                            }
                            catch { }
                            // update any JSON-generated controls for team_id/team_number
                            try
                            {
                                if (controls != null)
                                {
                                    if (controls.TryGetValue("team_id", out var c1))
                                    {
                                        if (c1 is Entry e1) e1.Text = se.TeamId.ToString();
                                        else if (c1 is Editor ed1) ed1.Text = se.TeamId.ToString();
                                        else if (c1 is Label l1) l1.Text = se.TeamId.ToString();
                                    }
                                    if (controls.TryGetValue("team_number", out var c2))
                                    {
                                        if (c2 is Entry e2) e2.Text = se.TeamNumber.ToString();
                                        else if (c2 is Editor ed2) ed2.Text = se.TeamNumber.ToString();
                                        else if (c2 is Label l2) l2.Text = se.TeamNumber.ToString();
                                    }
                                }
                            }
                            catch { }
                            // reflect change in the UI collection if possible
                            try
                            {
                                if (BindingContext is HistoryViewModel vm)
                                {
                                    var existing = vm.AllScouting.FirstOrDefault(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                                    if (existing != null)
                                    {
                                        var idx = vm.AllScouting.IndexOf(existing);
                                        vm.AllScouting[idx] = se;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                };

                matchPicker.SelectedIndexChanged += (s, ea) =>
                {
                    try
                    {
                        if (matchPicker.SelectedItem is Match selMatch)
                        {
                            se.MatchNumber = selMatch.MatchNumber;
                            se.MatchId = selMatch.Id;
                            try { page.Title = $"Edit {se.TeamNumber}/{se.MatchNumber}"; } catch { }
                            // reflect change in the UI collection if possible
                            try
                            {
                                if (BindingContext is HistoryViewModel vm)
                                {
                                    var existing = vm.AllScouting.FirstOrDefault(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                                    if (existing != null)
                                    {
                                        var idx = vm.AllScouting.IndexOf(existing);
                                        vm.AllScouting[idx] = se;
                                    }
                                }
                            }
                            catch { }
                            // update any JSON-generated controls for match_id/match_number
                            try
                            {
                                if (controls != null)
                                {
                                    if (controls.TryGetValue("match_id", out var c1))
                                    {
                                        if (c1 is Entry e1) e1.Text = se.MatchId.ToString();
                                        else if (c1 is Editor ed1) ed1.Text = se.MatchId.ToString();
                                        else if (c1 is Label l1) l1.Text = se.MatchId.ToString();
                                    }
                                    if (controls.TryGetValue("match_number", out var c2))
                                    {
                                        if (c2 is Entry e2) e2.Text = se.MatchNumber.ToString();
                                        else if (c2 is Editor ed2) ed2.Text = se.MatchNumber.ToString();
                                        else if (c2 is Label l2) l2.Text = se.MatchNumber.ToString();
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                };

                // Select initial values
                if (teamsList != null && teamsList.Count > 0)
                    teamPicker.SelectedItem = teamsList.FirstOrDefault(t => t.Id == se.TeamId || t.TeamNumber == se.TeamNumber);
                if (matchesList != null && matchesList.Count > 0)
                    matchPicker.SelectedItem = matchesList.FirstOrDefault(m => m.Id == se.MatchId || m.MatchNumber == se.MatchNumber);

                var headerRow = new HorizontalStackLayout { Spacing = 12 };
                headerRow.Children.Add(teamLabel);
                headerRow.Children.Add(teamPicker);
                headerRow.Children.Add(matchLabel);
                headerRow.Children.Add(matchPicker);
                detail.Children.Add(headerRow);

                detail.Children.Add(new Label { Text = $"Scout: {se.ScoutName}", FontSize = 14 });

                // Parse JSON preview to build editable controls
                controls = new Dictionary<string, View>();
                try
                {
                    var json = se.Preview ?? string.Empty;
                    JsonDocument doc;
                    try { doc = JsonDocument.Parse(json); }
                    catch
                    {
                        // If preview isn't valid JSON, show raw text in editor
                        var ed = new Editor { Text = json, AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 200 };
                        detail.Children.Add(new Label { Text = "Data" });
                        detail.Children.Add(ed);
                        controls["__raw"] = ed;
                        doc = null;
                    }

                    if (doc != null)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var key = prop.Name;
                            var display = idToName.ContainsKey(key) ? idToName[key] : key;
                            // choose control based on value kind
                            switch (prop.Value.ValueKind)
                            {
                                case JsonValueKind.True:
                                case JsonValueKind.False:
                                {
                                    var sw = new Switch { IsToggled = prop.Value.GetBoolean() };
                                    detail.Children.Add(new Label { Text = display });
                                    detail.Children.Add(sw);
                                    // disable if uploaded or pending
                                    try { sw.IsEnabled = !(se.IsUploaded || se.IsPending); } catch { }
                                    controls[key] = sw;
                                    break;
                                }
                                case JsonValueKind.Number:
                                {
                                    var entry = new Entry { Text = prop.Value.GetRawText(), Keyboard = Keyboard.Numeric };
                                    detail.Children.Add(new Label { Text = display });
                                    detail.Children.Add(entry);
                                    try { entry.IsEnabled = !(se.IsUploaded || se.IsPending); } catch { }
                                    controls[key] = entry;
                                    break;
                                }
                                case JsonValueKind.String:
                                {
                                    var s = prop.Value.GetString() ?? string.Empty;
                                    if (s.Contains("\n") || s.Length > 120)
                                    {
                                        var ed = new Editor { Text = s, AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 120 };
                                        detail.Children.Add(new Label { Text = display });
                                        detail.Children.Add(ed);
                                        try { ed.IsEnabled = !(se.IsUploaded || se.IsPending); } catch { }
                                        controls[key] = ed;
                                    }
                                    else
                                    {
                                        var entry = new Entry { Text = s };
                                        detail.Children.Add(new Label { Text = display });
                                        detail.Children.Add(entry);
                                        try { entry.IsEnabled = !(se.IsUploaded || se.IsPending); } catch { }
                                        controls[key] = entry;
                                    }
                                    break;
                                }
                                case JsonValueKind.Object:
                                case JsonValueKind.Array:
                                default:
                                {
                                    var text = prop.Value.ToString();
                                    var ed = new Editor { Text = text, AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 160 };
                                    detail.Children.Add(new Label { Text = display });
                                    detail.Children.Add(ed);
                                    try { ed.IsEnabled = !(se.IsUploaded || se.IsPending); } catch { }
                                    controls[key] = ed;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }

                // Save / Cancel buttons
                var buttons = new HorizontalStackLayout { Spacing = 12 };
                var saveBtn = new Button { Text = "Save", Style = (Style)Application.Current?.Resources["PrimaryButton"] ?? null };
                var cancelBtn = new Button { Text = "Cancel", Style = (Style)Application.Current?.Resources["OutlineButton"] ?? null };
                buttons.Add(saveBtn);
                buttons.Add(cancelBtn);
                detail.Children.Add(buttons);

                // If entry is uploaded or pending, disable Save and indicate server-side update or pending
                try
                {
                    if (se.IsUploaded || se.IsPending)
                    {
                        saveBtn.IsEnabled = false;
                        if (se.IsUploaded)
                        {
                            saveBtn.Text = "Update on server";
                            saveBtn.BackgroundColor = Colors.LightGray;
                            saveBtn.Clicked += async (s, e) => await DisplayAlert("Read-only", "This entry has been uploaded. To edit it, update the record on the server.", "OK");
                        }
                        else
                        {
                            saveBtn.Text = "Pending (read-only)";
                            saveBtn.BackgroundColor = Colors.LightGray;
                            saveBtn.Clicked += async (s, e) => await DisplayAlert("Read-only", "This entry is pending upload and cannot be edited locally.", "OK");
                        }
                    }
                }
                catch { }

                // page already created above

                cancelBtn.Clicked += async (s, ea) => { await Navigation.PopModalAsync(); };

                saveBtn.Clicked += async (s, ea) =>
                {
                    try
                    {
                        // Update team/match from pickers
                        try
                        {
                            if (teamPicker.SelectedItem is Team selTeam)
                            {
                                se.TeamNumber = selTeam.TeamNumber;
                                se.TeamId = selTeam.Id;
                            }
                        }
                        catch { }
                        try
                        {
                            if (matchPicker.SelectedItem is Match selMatch)
                            {
                                se.MatchNumber = selMatch.MatchNumber;
                                se.MatchId = selMatch.Id;
                            }
                        }
                        catch { }

                        // Update se.Data from controls
                        foreach (var kv in controls)
                        {
                            var key = kv.Key;
                            var view = kv.Value;
                            object? newVal = null;
                            if (view is Switch sw) newVal = sw.IsToggled;
                            else if (view is Entry ent) newVal = ent.Text ?? string.Empty;
                            else if (view is Editor ed) newVal = ed.Text ?? string.Empty;
                            else newVal = null;

                            if (key == "__raw")
                            {
                                // replace entire data dictionary with parsed JSON if possible
                                try
                                {
                                    var parsed = JsonDocument.Parse((string?)newVal ?? string.Empty);
                                    var dict = new Dictionary<string, object>();
                                    foreach (var p in parsed.RootElement.EnumerateObject()) dict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? (object?)p.Value.GetString()! : (object?)p.Value.ToString();
                                    se.Data = dict.ToDictionary(x => x.Key, x => x.Value ?? new object());
                                }
                                catch
                                {
                                    // store raw string
                                    se.Data = new Dictionary<string, object?> { { "raw", newVal ?? string.Empty } } as Dictionary<string, object>;
                                }
                            }
                            else
                            {
                                if (se.Data == null) se.Data = new Dictionary<string, object>();
                                se.Data[key] = newVal ?? string.Empty;
                            }
                        }

                        // Mark as locally changed so UI shows modified state
                        se.HasLocalChanges = true;

                        // Persist updates to pending cache and cached data
                        try
                        {
                            var services = Application.Current?.Handler?.MauiContext?.Services;
                            if (services != null && services.GetService(typeof(ICacheService)) is ICacheService cache)
                            {
                                // Ensure we have an offline id to track local edits
                                if (string.IsNullOrEmpty(se.OfflineId)) se.OfflineId = Guid.NewGuid().ToString();

                                // update pending list (remove old then add updated)
                                await cache.RemovePendingScoutingAsync(x =>
                                    (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) ||
                                    (se.Id > 0 && x.Id == se.Id) ||
                                    (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId)
                                );
                                await cache.AddPendingScoutingAsync(se);

                                // also update cached scouting data if present
                                try
                                {
                                    var cached = await cache.GetCachedScoutingDataAsync();
                                    if (cached != null && cached.Count > 0)
                                    {
                                        var idx = cached.FindIndex(x =>
                                            (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) ||
                                            (se.Id > 0 && x.Id == se.Id) ||
                                            (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId)
                                        );
                                        if (idx >= 0)
                                        {
                                            cached[idx] = se;
                                        }
                                        else
                                        {
                                            // not found in cached list, add or replace server entry if present
                                            cached.Add(se);
                                        }
                                        await cache.CacheScoutingDataAsync(cached);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // Update UI collection
                        if (BindingContext is HistoryViewModel vm)
                        {
                            var existing = vm.AllScouting.FirstOrDefault(x => (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) || (se.Id > 0 && x.Id == se.Id) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                            if (existing != null)
                            {
                                var idx = vm.AllScouting.IndexOf(existing);
                                vm.AllScouting[idx] = se;
                            }
                            else
                            {
                                vm.AllScouting.Insert(0, se);
                            }
                        }

                        await Navigation.PopModalAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[History] Save edit failed: {ex.Message}");
                        await page.DisplayAlert("Error", "Failed to save changes.", "OK");
                    }
                };

                // Add a Close toolbar item as well
                page.ToolbarItems.Add(new ToolbarItem("Close", null, async () => await Navigation.PopModalAsync()));
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] Failed to open entry view: {ex.Message}");
        }
    }

    public async void OnViewPitEntryClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.CommandParameter is PitScoutingEntry pe)
            {
                // Build simple JSON-derived editor like match editing does
                var detail = new StackLayout { Padding = 12, Spacing = 8 };

                // Team selector (try to populate teams)
                var teamLabel = new Label { Text = "Team", FontAttributes = FontAttributes.Bold, FontSize = 14 };
                var teamPicker = new Picker { Title = "Select Team", WidthRequest = 220 };
                var teamsList = new List<Team>();
                try
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null && services.GetService(typeof(IApiService)) is IApiService api)
                    {
                        var tres = await api.GetTeamsAsync(limit: 500);
                        if (tres != null && tres.Success && tres.Teams != null) teamsList = tres.Teams;
                    }
                    if ((teamsList == null || teamsList.Count == 0) && services != null && services.GetService(typeof(ICacheService)) is ICacheService cache)
                    {
                        var cached = await cache.GetCachedTeamsAsync(); if (cached != null) teamsList = cached;
                    }
                }
                catch { }

                teamPicker.ItemsSource = teamsList;
                teamPicker.ItemDisplayBinding = new Binding(".", converter: new FuncConverter<Team, string>(t => t != null ? $"{t.TeamNumber} - {t.TeamName}" : string.Empty));
                try { teamPicker.SelectedItem = teamsList.FirstOrDefault(t => t.Id == pe.TeamId || t.TeamNumber == pe.TeamNumber); } catch { }

                var headerRow = new HorizontalStackLayout { Spacing = 12 };
                headerRow.Children.Add(teamLabel);
                headerRow.Children.Add(teamPicker);
                detail.Children.Add(headerRow);

                // Parse pe.Data into controls
                var controls = new Dictionary<string, View>();
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(pe.Data ?? new Dictionary<string, object>());
                    JsonDocument doc;
                    try { doc = JsonDocument.Parse(json); }
                    catch
                    {
                        var ed = new Editor { Text = json, AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 200 };
                        detail.Add(new Label { Text = "Data" });
                        detail.Add(ed);
                        controls["__raw"] = ed;
                        doc = null;
                    }

                    if (doc != null)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var key = prop.Name;
                            var display = key;
                            switch (prop.Value.ValueKind)
                            {
                                case JsonValueKind.True:
                                case JsonValueKind.False:
                                {
                                    var sw = new Switch { IsToggled = prop.Value.GetBoolean() };
                                    detail.Add(new Label { Text = display });
                                    detail.Add(sw);
                                    try { sw.IsEnabled = false; } catch { }
                                    controls[key] = sw;
                                    break;
                                }
                                case JsonValueKind.Number:
                                {
                                    var entry = new Entry { Text = prop.Value.GetRawText(), Keyboard = Keyboard.Numeric };
                                    detail.Add(new Label { Text = display });
                                    detail.Add(entry);
                                    try { entry.IsEnabled = false; } catch { }
                                    controls[key] = entry;
                                    break;
                                }
                                case JsonValueKind.String:
                                {
                                    var s = prop.Value.GetString() ?? string.Empty;
                                    if (s.Contains("\n") || s.Length > 120)
                                    {
                                        var ed = new Editor { Text = s, AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 120 };
                                        detail.Add(new Label { Text = display });
                                        detail.Add(ed);
                                        try { ed.IsEnabled = false; } catch { }
                                        controls[key] = ed;
                                    }
                                    else
                                    {
                                        var entry = new Entry { Text = s };
                                        detail.Add(new Label { Text = display });
                                        detail.Add(entry);
                                        try { entry.IsEnabled = false; } catch { }
                                        controls[key] = entry;
                                    }
                                    break;
                                }
                                case JsonValueKind.Object:
                                case JsonValueKind.Array:
                                default:
                                {
                                    var text = prop.Value.ToString();
                                    var ed = new Editor { Text = text, AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 160 };
                                    detail.Add(new Label { Text = display });
                                    detail.Add(ed);
                                    try { ed.IsEnabled = false; } catch { }
                                    controls[key] = ed;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }

                // Save/Cancel
                var buttons = new HorizontalStackLayout { Spacing = 12 };
                var saveBtn = new Button { Text = "Save", Style = (Style)Application.Current?.Resources["PrimaryButton"] ?? null };
                var cancelBtn = new Button { Text = "Cancel", Style = (Style)Application.Current?.Resources["OutlineButton"] ?? null };
                buttons.Add(saveBtn);
                buttons.Add(cancelBtn);
                detail.Add(buttons);

                // If pit entry is uploaded, disable Save and indicate server-side update
                try
                {
                    if (pe.Id > 0)
                    {
                        saveBtn.IsEnabled = false;
                        saveBtn.Text = "Update on server";
                        saveBtn.BackgroundColor = Colors.LightGray;
                        saveBtn.Clicked += async (s, e) => await DisplayAlert("Read-only", "This pit entry has been uploaded. To edit it, update the record on the server.", "OK");
                    }
                }
                catch { }

                var page = new ContentPage { Title = $"Edit Pit {pe.TeamNumber}", Content = new ScrollView { Content = detail } };
                cancelBtn.Clicked += async (s, ea) => { await Navigation.PopModalAsync(); };

                saveBtn.Clicked += async (s, ea) =>
                {
                    try
                    {
                        // Update team if changed
                        try
                        {
                            if (teamPicker.SelectedItem is Team selTeam)
                            {
                                pe.TeamNumber = selTeam.TeamNumber;
                                pe.TeamId = selTeam.Id;
                            }
                        }
                        catch { }

                        // Gather control values
                        var newData = new Dictionary<string, object>();
                        foreach (var kv in controls)
                        {
                            var key = kv.Key;
                            var view = kv.Value;
                            object? newVal = null;
                            if (view is Switch sw) newVal = sw.IsToggled;
                            else if (view is Entry ent) newVal = ent.Text ?? string.Empty;
                            else if (view is Editor ed) newVal = ed.Text ?? string.Empty;
                            else newVal = null;

                            if (key == "__raw")
                            {
                                try
                                {
                                    var parsed = JsonDocument.Parse((string?)newVal ?? string.Empty);
                                    var dict = new Dictionary<string, object>();
                                    foreach (var p in parsed.RootElement.EnumerateObject()) dict[p.Name] = p.Value.ValueKind == JsonValueKind.String ? (object?)p.Value.GetString()! : (object?)p.Value.ToString();
                                    pe.Data = dict.ToDictionary(x => x.Key, x => x.Value ?? new object());
                                }
                                catch { }
                            }
                            else
                            {
                                newData[key] = newVal ?? string.Empty;
                            }
                        }

                        if (pe.Data == null) pe.Data = new Dictionary<string, object>();
                        foreach (var kv in newData) pe.Data[kv.Key] = kv.Value ?? string.Empty;

                        // persist to cache
                        try
                        {
                            var services = Application.Current?.Handler?.MauiContext?.Services;
                            if (services != null && services.GetService(typeof(ICacheService)) is ICacheService cache)
                            {
                                var cached = await cache.GetCachedPitScoutingDataAsync() ?? new List<PitScoutingEntry>();
                                var idx = cached.FindIndex(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                                if (idx >= 0) cached[idx] = pe; else cached.Insert(0, pe);
                                await cache.CachePitScoutingDataAsync(cached);
                                try { await cache.RemovePendingPitAsync(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId)); } catch { }
                            }
                        }
                        catch { }

                        // update UI
                        try
                        {
                            if (BindingContext is HistoryViewModel vm)
                            {
                                var existing = vm.AllPit.FirstOrDefault(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                                if (existing != null)
                                {
                                    var i = vm.AllPit.IndexOf(existing);
                                    vm.AllPit[i] = pe;
                                }
                                else vm.AllPit.Insert(0, pe);
                            }
                        }
                        catch { }

                        await Navigation.PopModalAsync();
                    }
                    catch { }
                };

                page.ToolbarItems.Add(new ToolbarItem("Close", null, async () => await Navigation.PopModalAsync()));
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] OnViewPitEntryClicked error: {ex.Message}");
        }
    }

    public async void OnDeleteEntryClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.CommandParameter is ScoutingEntry se)
            {
                var confirm = await DisplayAlert("Delete", "Delete this local scouting entry?", "Delete", "Cancel");
                if (!confirm) return;

                try
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null && services.GetService(typeof(ICacheService)) is ICacheService cache)
                    {
                        // Prefer to delete by OfflineId when available (unique for local entries).
                        if (!string.IsNullOrEmpty(se.OfflineId))
                        {
                            await cache.RemovePendingScoutingAsync(x => x.OfflineId == se.OfflineId);
                        }
                        else if (se.Id > 0)
                        {
                            await cache.RemovePendingScoutingAsync(x => x.Id == se.Id);
                        }
                        else
                        {
                            // Fallback: match by timestamp + team + match to avoid removing all entries with default ids
                            var ts = se.Timestamp;
                            var team = se.TeamId;
                            var match = se.MatchId;
                            await cache.RemovePendingScoutingAsync(x => x.Timestamp == ts && x.TeamId == team && x.MatchId == match);
                        }

                        // Also remove from cached scouting data (so deleted items don't reappear after restart)
                        try
                        {
                            var cached = await cache.GetCachedScoutingDataAsync();
                            if (cached != null && cached.Count > 0)
                            {
                                var before = cached.Count;
                                cached = cached.Where(x => !(
                                    (!string.IsNullOrEmpty(se.OfflineId) && x.OfflineId == se.OfflineId) ||
                                    (se.Id > 0 && x.Id == se.Id) ||
                                    (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId)
                                )).ToList();

                                if (cached.Count != before)
                                {
                                    await cache.CacheScoutingDataAsync(cached);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[History] Failed to remove from cached scouting: {ex.Message}");
                        }

                        // Persist deletion marker to AppData so deletions survive restart
                        try
                        {
                            var appData = FileSystem.AppDataDirectory;
                            // offline id markers
                            if (!string.IsNullOrEmpty(se.OfflineId))
                            {
                                var offlinePath = Path.Combine(appData, "deleted_scouting_offline.json");
                                List<string> offs = new();
                                if (File.Exists(offlinePath))
                                {
                                    try { offs = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(offlinePath)) ?? new List<string>(); } catch { offs = new List<string>(); }
                                }
                                if (!offs.Contains(se.OfflineId))
                                {
                                    offs.Add(se.OfflineId);
                                    try { await File.WriteAllTextAsync(offlinePath, JsonSerializer.Serialize(offs)); } catch { }
                                }
                            }

                            // numeric id markers
                            if (se.Id > 0)
                            {
                                var idsPath = Path.Combine(appData, "deleted_scouting_ids.json");
                                List<int> ids = new();
                                if (File.Exists(idsPath))
                                {
                                    try { ids = JsonSerializer.Deserialize<List<int>>(await File.ReadAllTextAsync(idsPath)) ?? new List<int>(); } catch { ids = new List<int>(); }
                                }
                                if (!ids.Contains(se.Id))
                                {
                                    ids.Add(se.Id);
                                    try { await File.WriteAllTextAsync(idsPath, JsonSerializer.Serialize(ids)); } catch { }
                                }
                            }
                        }
                        catch { }

                        // Also remove any exported JSON files that match this entry so it doesn't reappear from exports
                        try
                        {
                            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            var exportsFolder = Path.Combine(documentsPath, "ObsidianScout", "Exports");
                            if (Directory.Exists(exportsFolder))
                            {
                                foreach (var file in Directory.GetFiles(exportsFolder, "*.json"))
                                {
                                    try
                                    {
                                        var json = await File.ReadAllTextAsync(file);
                                        var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                                        if (doc == null) continue;

                                        var teamNumber = doc.ContainsKey("team_number") && int.TryParse(doc["team_number"]?.ToString(), out var tn) ? tn : 0;
                                        var matchNumber = doc.ContainsKey("match_number") && int.TryParse(doc["match_number"]?.ToString(), out var mn) ? mn : 0;
                                        DateTime timestamp = File.GetCreationTimeUtc(file);
                                        if (doc.ContainsKey("generated_at") && DateTime.TryParse(doc["generated_at"]?.ToString(), out var parsed)) timestamp = parsed;

                                        if (teamNumber == se.TeamNumber && matchNumber == se.MatchNumber && timestamp.ToLocalTime() == se.Timestamp)
                                        {
                                            try { File.Delete(file); }
                                            catch { }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }

                        // Remove from UI list immediately
                        if (BindingContext is HistoryViewModel vm)
                        {
                            // Remove matching item from the observable collection if present
                            var item = vm.AllScouting.FirstOrDefault(x => x.OfflineId == se.OfflineId || (x.Id == se.Id && se.Id > 0) || (x.Timestamp == se.Timestamp && x.TeamId == se.TeamId && x.MatchId == se.MatchId));
                            if (item != null)
                            {
                                vm.AllScouting.Remove(item);
                            }
                            else
                            {
                                // Fallback: reload from cache
                                await vm.LoadAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[History] Failed to delete entry: {ex.Message}");
                    await DisplayAlert("Error", "Failed to delete entry.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] OnDeleteEntryClicked error: {ex.Message}");
        }
    }

    public async void OnDeletePitEntryClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.CommandParameter is PitScoutingEntry pe)
            {
                var confirm = await DisplayAlert("Delete", "Delete this local pit scouting entry?", "Delete", "Cancel");
                if (!confirm) return;

                try
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    if (services != null && services.GetService(typeof(ICacheService)) is ICacheService cache)
                    {
                        if (pe.Id > 0)
                        {
                            await cache.RemovePendingPitAsync(x => x.Id == pe.Id);
                        }
                        else if (!string.IsNullOrEmpty(pe.TeamName))
                        {
                            // fallback: try to match by team number + timestamp
                            var ts = pe.Timestamp;
                            var team = pe.TeamId;
                            await cache.RemovePendingPitAsync(x => x.Timestamp == ts && x.TeamId == team);
                        }
                        if (BindingContext is HistoryViewModel vm)
                        {
                            var item = vm.AllPit.FirstOrDefault(x => x.Id == pe.Id || (x.Timestamp == pe.Timestamp && x.TeamId == pe.TeamId));
                            if (item != null)
                            {
                                vm.AllPit.Remove(item);
                            }
                            else
                            {
                                await vm.LoadAsync();
                            }
                        }
                        // Persist deletion marker for pit entries so they don't reappear after restart
                        try
                        {
                            if (pe.Id > 0)
                            {
                                var appData = FileSystem.AppDataDirectory;
                                var pitIdsPath = Path.Combine(appData, "deleted_pit_ids.json");
                                List<int> ids = new();
                                if (File.Exists(pitIdsPath))
                                {
                                    try { ids = JsonSerializer.Deserialize<List<int>>(await File.ReadAllTextAsync(pitIdsPath)) ?? new List<int>(); } catch { ids = new List<int>(); }
                                }
                                if (!ids.Contains(pe.Id))
                                {
                                    ids.Add(pe.Id);
                                    try { await File.WriteAllTextAsync(pitIdsPath, JsonSerializer.Serialize(ids)); } catch { }
                                }
                            }
                        }
                        catch { }
                        
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[History] Failed to delete pit entry: {ex.Message}");
                    await DisplayAlert("Error", "Failed to delete pit entry.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[History] OnDeletePitEntryClicked error: {ex.Message}");
        }
    }
}
