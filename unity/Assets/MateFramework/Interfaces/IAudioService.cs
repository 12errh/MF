using System;

namespace Mate.Interfaces
{
    /// <summary>Audio monitoring and playback-program detection.</summary>
    public interface IAudioService
    {
        bool IsMonitoring { get; }
        float Threshold { get; }
        float GetPeakLevel(int nodeId);
        int[] GetPlayingAudioPrograms();
        bool IsAppPlaying(string appName);
        bool IsAllowedApp(string appName);
        void StartMonitoring(int nodeId);
        void StopMonitoring(int nodeId);
        event Action<int, float> OnPeakLevelChanged;
    }

    public record AudioPeakEvent(int NodeId, float Level);
    public record AudioAppPlayingEvent(string AppName, bool IsPlaying);
}