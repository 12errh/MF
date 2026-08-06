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
                result.AddRange(MapPrograms(programs));
            });
            return result;
        }

        /// <summary>Map grabbed AudioProgram records to framework AudioProgramInfo.</summary>
        public static List<AudioProgramInfo> MapPrograms(IEnumerable<AudioProgram> programs)
        {
            return programs
                .Select(p => new AudioProgramInfo(
                    p.Name, p.ProcessName, p.ProcessId, p.Volume, p.NodeId))
                .ToList();
        }

        public float GetPeakLevel(uint nodeId)
        {
            // The monolith exposes peak levels through its monitoring callback in
            // ProgramPeaks (populated by StartMonitoringStream's read callback).
            if (_manager == null)
                return 0f;

            // -1 is the sentinel the monolith writes before the first peak sample.
            return _manager.ProgramPeaks.TryGetValue(nodeId, out var peak) && peak >= 0f
                ? peak
                : 0f;
        }
    }
}