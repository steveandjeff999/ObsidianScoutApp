# ? Quick Fix: Command Naming Issues

## Problem

Build errors showing:
- `ScoutingViewModel' does not contain a definition for 'RefreshAsyncCommand'`
- `ScoutingViewModel' does not contain a definition for 'RefreshTeamsAsyncCommand'`
- `ScoutingViewModel' does not contain a definition for 'ExportJsonAsyncCommand'`

## Root Cause

The MVVM Toolkit's `[RelayCommand]` attribute generates command names based on the method name:

- Method: `RefreshAsync()` ? Generated Command: `RefreshAsyncCommand`
- Method: `RefreshTeamsAsync()` ? Generated Command: `RefreshTeamsAsyncCommand`  
- Method: `ExportJsonAsync()` ? Generated Command: `ExportJsonAsyncCommand`

However, `ScoutingPage.xaml.cs` is trying to reference these commands incorrectly.

---

## Solution: Update Command References in ScoutingPage.xaml.cs

### 1. Line 278 - Retry Button
**Current (Wrong)**:
```csharp
retryButton.Clicked += (s, e) => _viewModel.RefreshAsyncCommand.Execute(null);
```

**Fix - Option A (Use the command)**:
```csharp
retryButton.Clicked += (s, e) => _viewModel.RefreshAsyncCommand.Execute(null);
```

**Fix - Option B (Call method directly)**:
```csharp
retryButton.Clicked += async (s, e) => await _viewModel.RefreshAsync();
```

### 2. Line 462 - Refresh Teams Button
**Current (Wrong)**:
```csharp
refreshTeamsBtn.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshTeamsAsyncCommand));
```

**Fix**:
```csharp
refreshTeamsBtn.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshTeamsAsyncCommand));
```

### 3. Line 1029 - Export JSON Button  
**Current (Wrong)**:
```csharp
exportJsonButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.ExportJsonAsyncCommand));
```

**Fix**:
```csharp
exportJsonButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.ExportJsonAsyncCommand));
```

---

## MVVM Toolkit Command Naming Rules

### Pattern:
```
[RelayCommand]
private async Task {MethodName}Async()

Generates ?  {MethodName}AsyncCommand
```

### Examples:
| Method Name | Generated Command Name |
|------------|----------------------|
| `RefreshAsync()` | `RefreshAsyncCommand` |
| `LoadAsync()` | `LoadAsyncCommand` |
| `SaveAsync()` | `SaveAsyncCommand` |
| `Refresh()` | `RefreshCommand` |
| `Load()` | `LoadCommand` |

### For Synchronous Methods:
```
[RelayCommand]
private void Reset()

Generates ? ResetCommand
```

---

## Alternative: Simplify Method Names

If you want commands without "Async" suffix, rename the methods:

### Option 1 - Remove "Async" Suffix:
```csharp
// Change from:
[RelayCommand]
private async Task RefreshAsync()

// To:
[RelayCommand(CanExecute = nameof(CanRefresh))]
private async Task Refresh()

// Generates: RefreshCommand (cleaner!)
```

### Option 2 - Use custom command names:
```csharp
[RelayCommand(CanExecute = nameof(CanRefresh))]
[CommandName("Refresh")]  // Not actually supported - use method naming instead
```

---

## Immediate Fix for Your Code

Replace in `ScoutingPage.xaml.cs`:

```csharp
// Line 278 - Change to call method directly
retryButton.Clicked += async (s, e) => await _viewModel.RefreshAsync();

// Line 462 - Keep command but use correct name  
refreshTeamsBtn.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshTeamsAsyncCommand));

// Line 1029 - Keep command but use correct name
exportJsonButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.ExportJsonAsyncCommand));
```

OR remove the calls entirely if buttons don't need these specific bindings.

---

## Why Commands Don't Match

The error says the commands don't exist because:
1. Methods ARE defined with `[RelayCommand]`
2. Generated commands SHOULD exist
3. But the **names referenced in code are WRONG**

The MVVM Toolkit sees:
- `RefreshAsync()` ? creates `RefreshAsyncCommand`

Your code tries to use:
- `RefreshCommand` ? (doesn't exist)

---

## Build Fix Steps

1. **Option A**: Update all command references to include "Async"
   ```csharp
   _viewModel.RefreshAsyncCommand
   _viewModel.RefreshTeamsAsyncCommand
   _viewModel.ExportJsonAsyncCommand
   ```

2. **Option B**: Call methods directly instead of commands
   ```csharp
   await _viewModel.RefreshAsync();
   await _viewModel.RefreshTeamsAsync();
   await _viewModel.ExportJsonAsync();
   ```

3. **Option C**: Rename the methods to remove "Async" suffix
   ```csharp
   [RelayCommand]
   private async Task Refresh() { ... }  // Generates RefreshCommand
   ```

---

## Recommended: Call Methods Directly

For the retry button, just call the method:

```csharp
// Simple and clean
retryButton.Clicked += async (s, e) => 
{
    await _viewModel.RefreshAsync();
};
```

This avoids command name confusion and is perfectly valid.

---

## Summary

? **The ScoutingViewModel IS correct** - it has the commands
? **ScoutingPage.xaml.cs references them WRONG** - wrong names

**Quick Fix**: Change command references from `RefreshCommand` to `RefreshAsyncCommand` (add "Async").

Or better: Call methods directly instead of using commands for these event handlers.
