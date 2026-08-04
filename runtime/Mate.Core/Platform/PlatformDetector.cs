using System;
using Mate.Core;

namespace Mate.Platform
{
    /// <summary>
    /// Detects desktop environment and session type from XDG env vars.
    /// Port of EarlyEnvSet.cs with platform capabilities.
    /// </summary>
    public class PlatformDetector
    {
        public string DetectDesktopEnvironment()
        {
            return Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "unknown";
        }

        public string DetectSessionType()
        {
            return Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown";
        }

        public bool IsHyprland()
        {
            return !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"));
        }

        public bool IsX11()
        {
            return DetectSessionType() == "x11";
        }

        public bool IsWayland()
        {
            return DetectSessionType() == "wayland";
        }

        public IPlatformCapabilities GetCapabilities()
        {
            var sessionType = DetectSessionType();
            bool isWayland = sessionType == "wayland";
            bool isHyprland = IsHyprland();

            return new LinuxCapabilities
            {
                BackendName = isHyprland ? "Hyprland" : "X11",
                BackendVersion = "1.0",
                SupportsTransparency = !isWayland || isHyprland,
                SupportsClickThrough = !isWayland || isHyprland,
                SupportsAlwaysOnTop = true,
                SupportsWindowEnumeration = !isWayland || isHyprland,
                SupportsMonitorEnumeration = true,
                SupportsScreenCapture = false, // PipeWire needed, defer to v2
                SupportsWindowSitting = !isWayland || isHyprland,
                SupportsDesktopSitting = sessionType == "x11",
                SupportsHideFromTaskbar = !isWayland || isHyprland,
            };
        }
    }

    internal class LinuxCapabilities : IPlatformCapabilities
    {
        public bool SupportsTransparency { get; set; }
        public bool SupportsClickThrough { get; set; }
        public bool SupportsAlwaysOnTop { get; set; }
        public bool SupportsWindowEnumeration { get; set; }
        public bool SupportsMonitorEnumeration { get; set; }
        public bool SupportsScreenCapture { get; set; }
        public bool SupportsWindowSitting { get; set; }
        public bool SupportsDesktopSitting { get; set; }
        public bool SupportsHideFromTaskbar { get; set; }
        public string BackendName { get; set; }
        public string BackendVersion { get; set; }
    }
}