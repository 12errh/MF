using System;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;

namespace Mate.System
{
    /// <summary>
    /// System tray + notification service. State is tracked locally; visual
    /// presentation is delegated to the platform layer via IEventBus so the
    /// service stays testable without AppIndicator/DBus native calls.
    /// </summary>
    public class SystemTrayService : ISystemService, IDisposable
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private bool _isVisible;

        public bool IsSupported => true; // AppIndicator on Linux

        public SystemTrayService(IConfiguration config, IEventBus eventBus)
        {
            _config = config;
            _eventBus = eventBus;
        }

        public Task<Result> ShowTrayIcon(string iconPath, string tooltip)
        {
            if (_isVisible)
                return Task.FromResult(Result.Ok());

            // Platform layer (TrayIndicator/AppIndicator) consumes this event.
            _isVisible = true;
            _eventBus.Publish(new TrayIconShownEvent(iconPath, tooltip));
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> HideTrayIcon()
        {
            _isVisible = false;
            _eventBus.Publish(new TrayIconHiddenEvent());
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> ShowNotification(string title, string message)
        {
            if (string.IsNullOrEmpty(title)) title = "Mate Framework";
            if (string.IsNullOrEmpty(message)) return Task.FromResult(Result.Fail("Notification message is empty"));

            // Platform layer (DBusNotificationHelper) consumes this event.
            _eventBus.Publish(new NotificationShownEvent(title, message));
            return Task.FromResult(Result.Ok());
        }

        public void Dispose()
        {
            if (_isVisible)
            {
                _isVisible = false;
                _eventBus.Publish(new TrayIconHiddenEvent());
            }
        }
    }

    public record TrayIconShownEvent(string IconPath, string Tooltip);
    public record TrayIconHiddenEvent();
    public record NotificationShownEvent(string Title, string Message);
}