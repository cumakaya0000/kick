using System;
using Microsoft.Extensions.Logging;
using KickAutoRecorder.Core.Interfaces;

namespace KickAutoRecorder.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void ShowNotification(string title, string message, string iconType = "Info")
    {
        _logger.LogInformation("NOTIFICATION [{Icon}]: {Title} - {Message}", iconType, title, message);

        try
        {
            // Windows NotifyIcon / System Toast notification via Windows API
            System.Threading.Tasks.Task.Run(() =>
            {
                using var notifyIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Information,
                    Visible = true,
                    BalloonTipTitle = title,
                    BalloonTipText = message,
                    BalloonTipIcon = iconType switch
                    {
                        "Error" => System.Windows.Forms.ToolTipIcon.Error,
                        "Warning" => System.Windows.Forms.ToolTipIcon.Warning,
                        _ => System.Windows.Forms.ToolTipIcon.Info
                    }
                };

                notifyIcon.ShowBalloonTip(4000);
                System.Threading.Thread.Sleep(4500);
                notifyIcon.Visible = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display Windows notification Toast.");
        }
    }
}
