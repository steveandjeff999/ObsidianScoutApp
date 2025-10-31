using ObsidianScout.ViewModels;
using ObsidianScout.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Collections;
using System.Windows.Input;

namespace ObsidianScout.Views;

public partial class ChatPage : ContentPage
{
 private bool _membersLoaded = false;
 private ChatViewModel? _vm;

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
 };
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
 MainThread.BeginInvokeOnMainThread(() => ScrollToBottom());
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
}