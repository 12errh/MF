using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace Mate.Platform
{
    /// <summary>
    /// Synchronous seam over the native window backend. The default
    /// implementation is the ported X11 windowing code; tests inject a fake.
    /// Mirrors the framework's adapter pattern (IPulseAudio/IVrmLoader).
    /// </summary>
    public interface IWindowBackend : IDisposable
    {
        /// <summary>Open the display and locate the Unity window (by PID).</summary>
        bool Initialize(IntPtr unityWindow);

        bool GetWindowPosition(out Vector2Int position);
        bool SetWindowPosition(Vector2Int position);
        bool GetWindowSize(out Vector2Int size);
        bool SetWindowSize(Vector2Int size);

        bool SetAlwaysOnTop(bool value);
        bool SetBorderless(bool value);
        bool SetClickThrough(bool value);
        bool HideFromTaskbar(bool value);
        bool SetWindowType(int type);

        bool GetMousePosition(out Vector2Int position);

        /// <summary>Set the window title (used to name the window after the project).</summary>
        bool SetWindowTitle(string title);

        List<MonitorInfoData> GetAllMonitors();
        List<IntPtr> GetAllVisibleWindows();
        WindowInfoData GetWindowInfo(IntPtr handle);
    }

    /// <summary>Backend-neutral monitor rectangle.</summary>
    public struct MonitorInfoData
    {
        public int Index;
        public string Name;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    /// <summary>Backend-neutral window info.</summary>
    public struct WindowInfoData
    {
        public IntPtr Handle;
        public Vector2Int Position;
        public Vector2Int Size;
        public string ClassName;

        public WindowInfoData(IntPtr handle, Vector2Int position, Vector2Int size, string className)
        {
            Handle = handle;
            Position = position;
            Size = size;
            ClassName = className;
        }
    }
}
