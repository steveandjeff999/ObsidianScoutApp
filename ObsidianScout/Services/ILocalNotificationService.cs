namespace ObsidianScout.Services;

public interface ILocalNotificationService
{
 Task ShowAsync(string title, string body, int id =0);
}
