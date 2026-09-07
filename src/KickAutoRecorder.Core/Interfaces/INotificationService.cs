namespace KickAutoRecorder.Core.Interfaces;

public interface INotificationService
{
    void ShowNotification(string title, string message, string iconType = "Info");
}
