using System;
using System.Numerics;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Platform;

namespace Mate.Window
{
    /// <summary>
    /// IWindowService backed by a native window backend (X11 by default).
    /// Reads the mate.toml [window] section through IConfiguration and applies
    /// it to the Unity window on Initialize. Testable with a fake IWindowBackend.
    /// </summary>
    public class WindowService : IWindowService
    {
        private readonly IWindowBackend _backend;
        private readonly IConfiguration _config;
        private bool _initialized;

        public WindowService(IWindowBackend backend, IConfiguration config)
        {
            _backend = backend;
            _config = config;
        }

        public async Task<Result> Initialize(IntPtr unityWindow)
        {
            await Task.CompletedTask;
            if (!_backend.Initialize(unityWindow))
                return Result.Fail("Failed to open the display or locate the Unity window");

            _initialized = true;
            ApplyWindowConfig();
            return Result.Ok();
        }

        /// <summary>Apply mate.toml [window] settings to the native window.</summary>
        private void ApplyWindowConfig()
        {
            if (_config.GetBool("alwaysOnTop", true)) _backend.SetAlwaysOnTop(true);
            if (_config.GetBool("transparent", true))
            {
                _backend.SetBorderless(true);
                if (_config.GetBool("clickThrough", false))
                    _backend.SetClickThrough(true);
            }
            if (_config.GetBool("hideFromTaskbar", false)) _backend.HideFromTaskbar(true);

            var windowType = _config.GetString("windowType", "normal");
            _backend.SetWindowType(windowType switch
            {
                "dock" => 1,
                "desktop" => 2,
                _ => 0,
            });

            var position = _config.GetString("initialPosition", "center");
            if (position != "center" && position.Contains(','))
            {
                var parts = position.Split(',');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0].Trim(), out var x) &&
                    int.TryParse(parts[1].Trim(), out var y))
                {
                    _backend.SetWindowPosition(new UnityEngine.Vector2Int(x, y));
                }
            }
        }

        public async Task<Result<WindowInfo>> GetWindowInfo(IntPtr handle)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result<WindowInfo>.Fail("Window service not initialized");
            var info = _backend.GetWindowInfo(handle);
            return Result<WindowInfo>.Ok(new WindowInfo(
                info.Handle,
                new Vector2(info.Position.x, info.Position.y),
                new Vector2(info.Size.x, info.Size.y),
                info.ClassName));
        }

        public async Task<Result> SetPosition(Vector2 position)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result.Fail("Window service not initialized");
            return _backend.SetWindowPosition(new UnityEngine.Vector2Int((int)position.X, (int)position.Y))
                ? Result.Ok()
                : Result.Fail("SetWindowPosition failed");
        }

        public async Task<Result<Vector2>> GetPosition()
        {
            await Task.CompletedTask;
            if (!_initialized) return Result<Vector2>.Fail("Window service not initialized");
            if (!_backend.GetWindowPosition(out var pos)) return Result<Vector2>.Fail("GetWindowPosition failed");
            return Result<Vector2>.Ok(new Vector2(pos.x, pos.y));
        }

        public async Task<Result> SetSize(Vector2 size)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result.Fail("Window service not initialized");
            return _backend.SetWindowSize(new UnityEngine.Vector2Int((int)size.X, (int)size.Y))
                ? Result.Ok()
                : Result.Fail("SetWindowSize failed");
        }

        public async Task<Result<Vector2>> GetSize()
        {
            await Task.CompletedTask;
            if (!_initialized) return Result<Vector2>.Fail("Window service not initialized");
            if (!_backend.GetWindowSize(out var size)) return Result<Vector2>.Fail("GetWindowSize failed");
            return Result<Vector2>.Ok(new Vector2(size.x, size.y));
        }

        public async Task<Result> SetAlwaysOnTop(bool value)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result.Fail("Window service not initialized");
            return _backend.SetAlwaysOnTop(value) ? Result.Ok() : Result.Fail("SetAlwaysOnTop failed");
        }

        public async Task<Result> SetBorderless(bool value)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result.Fail("Window service not initialized");
            return _backend.SetBorderless(value) ? Result.Ok() : Result.Fail("SetBorderless failed");
        }

        public async Task<Result> SetClickThrough(bool value)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result.Fail("Window service not initialized");
            return _backend.SetClickThrough(value) ? Result.Ok() : Result.Fail("SetClickThrough failed");
        }

        public async Task<Result> HideFromTaskbar(bool value)
        {
            await Task.CompletedTask;
            if (!_initialized) return Result.Fail("Window service not initialized");
            return _backend.HideFromTaskbar(value) ? Result.Ok() : Result.Fail("HideFromTaskbar failed");
        }

        public async Task<Result<Vector2>> GetMousePosition()
        {
            await Task.CompletedTask;
            if (!_initialized) return Result<Vector2>.Fail("Window service not initialized");
            if (!_backend.GetMousePosition(out var pos)) return Result<Vector2>.Fail("GetMousePosition failed");
            return Result<Vector2>.Ok(new Vector2(pos.x, pos.y));
        }

        public async Task<Result<MonitorInfo[]>> GetAllMonitors()
        {
            await Task.CompletedTask;
            if (!_initialized) return Result<MonitorInfo[]>.Fail("Window service not initialized");
            var monitors = _backend.GetAllMonitors();
            var result = new MonitorInfo[monitors.Count];
            for (var i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                result[i] = new MonitorInfo(m.Index, m.Name,
                    new Rectangle(m.X, m.Y, m.Width, m.Height));
            }
            return Result<MonitorInfo[]>.Ok(result);
        }

        public async Task<Result<IntPtr[]>> GetAllVisibleWindows()
        {
            await Task.CompletedTask;
            if (!_initialized) return Result<IntPtr[]>.Fail("Window service not initialized");
            return Result<IntPtr[]>.Ok(_backend.GetAllVisibleWindows().ToArray());
        }

        public void Dispose() => _backend.Dispose();
    }
}