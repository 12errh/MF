using System.Collections.Generic;
using Mate.Audio;
using NUnit.Framework;
using PulseAudio;

[TestFixture]
public class PulseAudioAdapterTests
{
    [Test]
    public void MapPrograms_MapsNodeId()
    {
        var program = new AudioProgram
        {
            Name = "spotify",
            ProcessName = "spotify",
            ProcessId = 100,
            Volume = 0.5,
            NodeId = 42,
        };

        var mapped = PulseAudioAdapter.MapPrograms(new List<AudioProgram> { program });

        Assert.AreEqual(1, mapped.Count);
        Assert.AreEqual("spotify", mapped[0].Name);
        Assert.AreEqual("spotify", mapped[0].ProcessName);
        Assert.AreEqual(100, mapped[0].ProcessId);
        Assert.AreEqual(0.5, mapped[0].Volume, 0.001);
        Assert.AreEqual(42u, mapped[0].NodeId);
    }

    [Test]
    public void MapPrograms_Empty_ReturnsEmpty()
    {
        var mapped = PulseAudioAdapter.MapPrograms(new List<AudioProgram>());
        Assert.AreEqual(0, mapped.Count);
    }

    [Test]
    public void MapPrograms_MultiplePrograms_PreservesNodeIds()
    {
        var programs = new List<AudioProgram>
        {
            new AudioProgram { Name = "a", ProcessName = "a", ProcessId = 1, Volume = 0.1, NodeId = 7 },
            new AudioProgram { Name = "b", ProcessName = "b", ProcessId = 2, Volume = 0.2, NodeId = 9 },
        };

        var mapped = PulseAudioAdapter.MapPrograms(programs);

        Assert.AreEqual(7u, mapped[0].NodeId);
        Assert.AreEqual(9u, mapped[1].NodeId);
    }
}
