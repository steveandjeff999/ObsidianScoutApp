namespace ObsidianScout.Services;

public interface ILocalNotificationService
{
 Task ShowAsync(string title, string body, int id =0);
 
 // NEW: Show notification with deep link data
 Task ShowWithDataAsync(string title, string message, int id, Dictionary<string, string> data);
}
