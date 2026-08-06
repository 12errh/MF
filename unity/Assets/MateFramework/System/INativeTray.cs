namespace Mate.System
{
    /// <summary>
    /// Abstraction over the native tray icon + desktop notification layer so the
    /// event-driven consumer is testable without AppIndicator/DBus calls.
    /// </summary>
    public interface INativeTray
    {
        void ShowIcon(string iconPath, string tooltip);
        void HideIcon();
        void Notify(string title, string message);
    }
}