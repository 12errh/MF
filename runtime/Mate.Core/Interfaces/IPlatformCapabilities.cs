namespace Mate.Core
{
    public interface IPlatformCapabilities
    {
        bool SupportsTransparency { get; }
        bool SupportsClickThrough { get; }
        bool SupportsAlwaysOnTop { get; }
        bool SupportsWindowEnumeration { get; }
        bool SupportsMonitorEnumeration { get; }
        bool SupportsScreenCapture { get; }
        bool SupportsWindowSitting { get; }
        bool SupportsDesktopSitting { get; }
        bool SupportsHideFromTaskbar { get; }
        string BackendName { get; }
        string BackendVersion { get; }
    }
}