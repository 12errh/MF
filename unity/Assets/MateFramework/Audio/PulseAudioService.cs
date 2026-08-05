using System;
using System.Collections.Generic;
using System.Linq;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;

namespace Mate.Audio
{
    /// <summary>
    /// Audio monitoring service. Allowed apps and threshold come from IConfiguration;
    /// native PulseAudio interaction is delegated to an injected IPulseAudio.
    /// </summary>
    public class PulseAudioService : IAudioService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private readonly IPulseAudio _pulseAudio;
        private readonly HashSet<string> _allowedApps;
        private readonly HashSet<int> _monitoredNodes = new();

        public bool IsMonitoring => _monitoredNodes.Count > 0;
        public float Threshold => _config.GetFloat("soundThreshold", 0.2f);

        public event Action<int, float> OnPeakLevelChanged;

        public PulseAudioService(IConfiguration config, IEventBus eventBus, IPulseAudio pulseAudio = null)
        {
            _config = config;
            _eventBus = eventBus;
            _pulseAudio = pulseAudio ?? new PulseAudioAdapter(null);

            var raw = config.GetString("allowedApps", "spotify");
            _allowedApps = new HashSet<string>(
                raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool IsAllowedApp(string appName) => _allowedApps.Contains(appName);

        public float GetPeakLevel(int nodeId)
        {
            if (!_monitoredNodes.Contains(nodeId))
                return 0f;
            return _pulseAudio.GetPeakLevel((uint)nodeId);
        }

        public int[] GetPlayingAudioPrograms()
        {
            return _pulseAudio.GetPlayingPrograms()
                .Select(p => p.ProcessId)
                .ToArray();
        }

        public bool IsAppPlaying(string appName)
        {
            if (!IsAllowedApp(appName)) return false;

            return _pulseAudio.GetPlayingPrograms()
                .Any(p => string.Equals(p.Name, appName, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(p.ProcessName, appName, StringComparison.OrdinalIgnoreCase));
        }

        public void StartMonitoring(int nodeId)
        {
            _monitoredNodes.Add(nodeId);
        }

        public void StopMonitoring(int nodeId)
        {
            _monitoredNodes.Remove(nodeId);
        }

        public void Poll()
        {
            // Iterate a snapshot so a subscriber mutating monitoring state during
            // delivery cannot invalidate the enumeration.
            foreach (var nodeId in _monitoredNodes.ToArray())
            {
                float level = _pulseAudio.GetPeakLevel((uint)nodeId);
                _eventBus.Publish(new AudioPeakEvent(nodeId, level));
                OnPeakLevelChanged?.Invoke(nodeId, level);
            }
        }
    }
}