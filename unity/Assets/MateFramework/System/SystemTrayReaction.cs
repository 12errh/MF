using System;
using Mate.Core;
using Mate.Interfaces;

namespace Mate.System
{
    /// <summary>
    /// Consumes tray/notification events published by SystemTrayService and
    /// forwards them to the native layer (INativeTray). Registered as a
    /// singleton so MateContext.Dispose runs its IDisposable.Dispose.
    /// </summary>
    public class SystemTrayReaction : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly INativeTray _native;
        private readonly SubscriptionToken _shownToken;
        private readonly SubscriptionToken _hiddenToken;
        private readonly SubscriptionToken _notifyToken;

        public SystemTrayReaction(IEventBus eventBus, INativeTray native)
        {
            _eventBus = eventBus;
            _native = native;
            _shownToken = eventBus.Subscribe<TrayIconShownEvent>(OnShown);
            _hiddenToken = eventBus.Subscribe<TrayIconHiddenEvent>(OnHidden);
            _notifyToken = eventBus.Subscribe<NotificationShownEvent>(OnNotify);
        }

        private void OnShown(TrayIconShownEvent evt) => _native.ShowIcon(evt.IconPath, evt.Tooltip);

        private void OnHidden(TrayIconHiddenEvent evt) => _native.HideIcon();

        private void OnNotify(NotificationShownEvent evt) => _native.Notify(evt.Title, evt.Message);

        public void Dispose()
        {
            _eventBus.Unsubscribe(_shownToken);
            _eventBus.Unsubscribe(_hiddenToken);
            _eventBus.Unsubscribe(_notifyToken);
        }
    }
}