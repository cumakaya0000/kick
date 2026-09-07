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
    }
}
