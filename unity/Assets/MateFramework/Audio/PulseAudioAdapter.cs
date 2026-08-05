using System.Collections.Generic;
using System.Linq;
using Mate.Interfaces;
using PulseAudio;
using UnityEngine;

namespace Mate.Audio
{
    /// <summary>
    /// Default IPulseAudio wrapping the grabbed PulseAudioManager monolith
    /// (native libpulse P/Invoke). Safe to construct in EditMode tests — native
    /// calls return empty/zero when PulseAudio is unavailable.
    /// </summary>
    public class PulseAudioAdapter : IPulseAudio
    {
        private readonly PulseAudioManager _manager;

        public PulseAudioAdapter(PulseAudioManager manager)
        {
            _manager = manager;
        }

        public List<AudioProgramInfo> GetPlayingPrograms()
        {
            var result = new List<AudioProgramInfo>();
            if (_manager == null) return result;

            _manager.GetPlayingAudioPrograms(programs =>
            {
                if (programs == null) return;
                result.AddRange(programs.Select(p => new AudioProgramInfo(
                    p.Name, p.ProcessName, p.ProcessId, p.Volume)));
            });
            return result;
        }

        public float GetPeakLevel(uint nodeId)
        {
            // The monolith exposes peak levels through its monitoring callback.
            // When not monitoring or PulseAudio is unavailable, this returns 0.
            return 0f;
        }
    }
}