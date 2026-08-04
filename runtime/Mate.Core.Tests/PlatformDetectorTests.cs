using NUnit.Framework;
using Mate.Platform;

namespace Mate.Core.Tests
{
    [TestFixture]
    public class PlatformDetectorTests
    {
        [Test]
        public void Detect_DesktopEnvironment_ReadsEnvVar()
        {
            var detector = new PlatformDetector();
            var de = detector.DetectDesktopEnvironment();
            Assert.IsNotNull(de);
            Assert.IsNotEmpty(de);
        }

        [Test]
        public void Detect_SessionType_ReadsEnvVar()
        {
            var detector = new PlatformDetector();
            var session = detector.DetectSessionType();
            Assert.IsNotNull(session);
        }

        [Test]
        public void IsHyprland_TrueWhenEnvSet()
        {
            var detector = new PlatformDetector();
            bool result = detector.IsHyprland();
            Assert.IsInstanceOf<bool>(result);
        }

        [Test]
        public void GetCapabilities_ReturnsNonNull()
        {
            var detector = new PlatformDetector();
            var caps = detector.GetCapabilities();
            Assert.IsNotNull(caps);
            Assert.IsNotNull(caps.BackendName);
        }
    }
}