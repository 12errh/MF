using System;
using System.Numerics;
using System.Threading.Tasks;
using Mate.Core.Models;

namespace Mate.Core
{
    public interface IWindowService : IDisposable
    {
        // Window position/size
        Task<Result<WindowInfo>> GetWindowInfo(IntPtr handle);
        Task<Result> SetPosition(Vector2 position);
        Task<Result<Vector2>> GetPosition();
        Task<Result> SetSize(Vector2 size);
        Task<Result<Vector2>> GetSize();

        // Window state
        Task<Result> SetAlwaysOnTop(bool value);
        Task<Result> SetBorderless(bool value);
        Task<Result> SetClickThrough(bool value);
        Task<Result> HideFromTaskbar(bool value);

        // Input
        Task<Result<Vector2>> GetMousePosition();

        // Monitor
        Task<Result<MonitorInfo[]>> GetAllMonitors();

        // Window discovery
        Task<Result<IntPtr[]>> GetAllVisibleWindows();

        // Lifecycle
        Task<Result> Initialize(IntPtr unityWindow);
    }
}