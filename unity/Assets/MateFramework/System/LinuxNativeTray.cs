using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Mate.System
{
    /// <summary>
    /// Linux native tray + notification implementation.
    /// Tray uses libayatana-appindicator3 (AppIndicator); notifications shell out
    /// to notify-send (no extra native library). Wrapped in #if !UNITY_EDITOR so
    /// EditMode tests never touch native calls.
    /// </summary>
    public class LinuxNativeTray : INativeTray
    {
        private const string LibraryName = "libayatana-appindicator3.so.1";

        private IntPtr _indicator = IntPtr.Zero;

        [StructLayout(LayoutKind.Sequential)]
        private struct AppIndicatorCategory { public int Value; public AppIndicatorCategory(int v) => Value = v; }

        private static readonly AppIndicatorCategory ApplicationStatus = new(0);

        [DllImport(LibraryName)]
        private static extern IntPtr app_indicator_new(string id, string icon_name, AppIndicatorCategory category);

        [DllImport(LibraryName)]
        private static extern void app_indicator_set_status(IntPtr indicator, int status);

        [DllImport(LibraryName)]
        private static extern void app_indicator_set_icon_full(IntPtr indicator, string icon_name, string description);

        [DllImport(LibraryName)]
        private static extern void app_indicator_set_icon_theme_path(IntPtr indicator, string theme_path);

        public void ShowIcon(string iconPath, string tooltip)
        {
#if !UNITY_EDITOR
            if (_indicator != IntPtr.Zero)
                return;

            _indicator = app_indicator_new("mate-framework", "applications-system", ApplicationStatus);
            if (_indicator == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError("[LinuxNativeTray] Failed to create AppIndicator");
                return;
            }

            app_indicator_set_status(_indicator, 1); // Active

            // app_indicator_set_icon_full takes a themed icon name, not a
            // filesystem path. When a custom icon file is provided, register its
            // directory as the icon theme path and pass the file name (without
            // extension) as the icon name.
            if (!string.IsNullOrEmpty(iconPath))
            {
                var full = Path.GetFullPath(iconPath);
                var dir = Path.GetDirectoryName(full);
                var name = Path.GetFileNameWithoutExtension(full);
                if (!string.IsNullOrEmpty(dir))
                    app_indicator_set_icon_theme_path(_indicator, dir);
                app_indicator_set_icon_full(_indicator, name, tooltip ?? "Mate");
            }
#else
            // No-op in the editor / headless tests.
#endif
        }

        public void HideIcon()
        {
#if !UNITY_EDITOR
            if (_indicator != IntPtr.Zero)
            {
                app_indicator_set_status(_indicator, 0); // Passive (hidden)
                _indicator = IntPtr.Zero;
            }
#endif
        }

        public void Notify(string title, string message)
        {
#if !UNITY_EDITOR
            try
            {
                var psi = new ProcessStartInfo("notify-send")
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                psi.ArgumentList.Add(title ?? "Mate");
                psi.ArgumentList.Add(message ?? string.Empty);
                var proc = Process.Start(psi);
                proc?.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"notify-send failed: {ex.Message}");
            }
#endif
        }
    }
}