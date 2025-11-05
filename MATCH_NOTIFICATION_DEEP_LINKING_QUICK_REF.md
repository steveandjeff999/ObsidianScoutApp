# Match Notification Deep Linking - Quick Reference

## ?? What It Does
Tapping a **match reminder** or **match strategy** notification now opens the **Match Prediction page** with the event and match **pre-selected** and prediction **auto-run**.

## ?? Quick Test
1. Subscribe to a match notification
2. Wait for notification (or trigger manually)
3. Tap notification
4. **Result**: Match Prediction page opens with:
- Event selected
   - Match selected
   - Prediction already calculated
   - Status: "?? Opened from notification - Match X"

## ?? Files Changed

| File | Changes |
|------|---------|
| `MainActivity.cs` | Added match notification detection & navigation URI building |
| `AppShell.xaml.cs` | Registered MatchPredictionPage route |
| `MatchPredictionViewModel.cs` | Added QueryProperty support + HandleDeepLinkAsync() |

## ?? Navigation Flow

```
Notification Tap
    ?
MainActivity detects type="match"
    ?
Builds: "MatchPredictionPage?eventId=123&matchNumber=42"
    ?
AppShell navigates after login
    ?
ViewModel receives parameters
    ?
Auto-selects event & match
    ?
Auto-runs prediction
    ?
User sees results! ?
```

## ?? Code Snippets

### MainActivity - Match Detection
```csharp
else if (type == "match" && !string.IsNullOrEmpty(eventId))
{
    navUri = $"MatchPredictionPage?eventId={eventId}";
    if (!string.IsNullOrEmpty(eventCode))
        navUri += $"&eventCode={System.Uri.EscapeDataString(eventCode)}";
    if (!string.IsNullOrEmpty(matchNumber))
        navUri += $"&matchNumber={matchNumber}";
}
```

### ViewModel - Deep Link Handling
```csharp
[QueryProperty(nameof(EventId), "eventId")]
[QueryProperty(nameof(MatchNumber), "matchNumber")]
public partial class MatchPredictionViewModel : ObservableObject
{
  public string? EventId { get; set; } // Triggers HandleDeepLinkAsync()
    
    private async Task HandleDeepLinkAsync()
    {
        // Find event, select it, wait for matches, 
    // find match, select it, run prediction
    }
}
```

### AppShell - Route Registration
```csharp
Routing.RegisterRoute("MatchPredictionPage", typeof(MatchPredictionPage));
```

## ? Chat Notifications Still Work
- Chat deep linking unchanged
- Uses separate route: `"Chat?sourceType=dm&sourceId=username"`
- **No conflicts or breaking changes**

## ?? Supported Notification Types

| Type | Deep Link | Auto-Predict |
|------|-----------|--------------|
| Match Reminder | ? Yes | ? Yes |
| Match Strategy | ? Yes | ? Yes |
| Chat Message | ? Yes (to Chat) | N/A |

## ?? Error Handling

```
Event not found ? Show error message
Match not found ? Show status, let user select manually
Not logged in ? Redirect to login, then navigate
```

## ?? Test Scenarios

1. **App closed** ? Tap notification ? Opens app ? Login ? Navigate ? Predict
2. **App background** ? Tap notification ? Resume ? Navigate ? Predict
3. **App foreground** ? Tap notification ? Navigate ? Predict
4. **Invalid event** ? Navigate ? Show error message
5. **Invalid match** ? Navigate ? Show event, manual match selection

## ?? User Benefits

? **Zero Manual Steps**: Just tap notification
? **Instant Results**: Prediction runs automatically
? **Context Preserved**: Right event & match selected
? **Status Indicator**: Shows "Opened from notification"

## ?? Status

**? COMPLETE** - All features implemented and tested
- Match notifications deep link to Match Prediction page
- Auto-select event and match
- Auto-run prediction
- Chat notifications still work perfectly

---

**Need more info?** See `MATCH_NOTIFICATION_DEEP_LINKING_COMPLETE.md` for full documentation.
