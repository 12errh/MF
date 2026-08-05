using System;
using System.IO;
using System.Threading;

namespace Mate.Core
{
    /// <summary>
    /// Watches a project directory for config/asset changes and publishes
    /// <see cref="ConfigReloadedEvent"/> on the event bus after a debounce.
    /// Code files (.cs) are deliberately ignored — hot reload covers
    /// config and assets only, never code (ADR-013).
    /// </summary>
    public class HotReloadHandler : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly Timer _debounceTimer;
        private readonly int _debounceMs;
        private readonly object _sync = new object();

        private FileSystemWatcher _watcher;
        private DateTime _lastReloadTime;
        private bool _disposed;

        private static readonly string[] WatchedExtensions =
            { ".toml", ".json", ".vrm", ".wav", ".mp3", ".anim" };

        public DateTime LastReloadTime => _lastReloadTime;

        public HotReloadHandler(string projectDir, IEventBus eventBus, int debounceMs = 500)
        {
            _eventBus = eventBus;
            _debounceMs = debounceMs;
            _lastReloadTime = DateTime.MinValue;

            _watcher = null;
            _debounceTimer = new Timer(DebounceCallback, null, Timeout.Infinite, Timeout.Infinite);

            if (!Directory.Exists(projectDir))
                return;

            _watcher = new FileSystemWatcher(projectDir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>Whether the given file extension should trigger a reload.</summary>
        public bool ShouldWatch(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return false;
            var ext = extension.ToLowerInvariant();
            return Array.IndexOf(WatchedExtensions, ext) >= 0;
        }

        /// <summary>
        /// Immediately record the reload time and publish the reload event.
        /// Exposed as a seam so tests can verify the event contract without
        /// filesystem timing races.
        /// </summary>
        public void TriggerReload(string source)
        {
            if (_disposed)
                return;
            _lastReloadTime = DateTime.UtcNow;
            _eventBus.Publish(new ConfigReloadedEvent(source));
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                var ext = Path.GetExtension(e.Name);
                if (!ShouldWatch(ext))
                    return;

                _debounceTimer.Change(_debounceMs, Timeout.Infinite);
            }
        }

        private void DebounceCallback(object state)
        {
            TriggerReload("settings");
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;

                _debounceTimer.Dispose();
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
            }
        }
    }

    /// <summary>Published when a watched config/asset file changes.</summary>
    public record ConfigReloadedEvent(string Source);
}
