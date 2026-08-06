using System.Collections.Generic;

namespace Mate.Interfaces
{
    /// <summary>
    /// Abstraction over the grabbed PulseAudioManager monolith. The default
    /// implementation wraps the native P/Invoke; tests inject a fake.
    /// </summary>
    public interface IPulseAudio
    {
        List<AudioProgramInfo> GetPlayingPrograms();
        float GetPeakLevel(uint nodeId);
    }

    public record AudioProgramInfo(string Name, string ProcessName, int ProcessId, double Volume, uint NodeId = 0);
}