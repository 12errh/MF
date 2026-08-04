using System;
using NUnit.Framework;
using Mate.Platform;

namespace Mate.Core.Tests
{
    [TestFixture]
    public class PlatformDetectorTests
    {
        private const string DesktopEnv = "XDG_CURRENT_DESKTOP";
        private const string SessionEnv = "XDG_SESSION_TYPE";
        private const string HyprlandEnv = "HYPRLAND_INSTANCE_SIGNATURE";

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(DesktopEnv, null);
            Environment.SetEnvironmentVariable(SessionEnv, null);
            Environment.SetEnvironmentVariable(HyprlandEnv, null);
        }

        [Test]
        public void Detect_DesktopEnvironment_ReadsEnvVar()
        {
            Environment.SetEnvironmentVariable(DesktopEnv, "sway");
            Assert.AreEqual("sway", new PlatformDetector().DetectDesktopEnvironment());
        }

        [Test]
        public void Detect_DesktopEnvironment_Unknown_WhenUnset()
        {
            Environment.SetEnvironmentVariable(DesktopEnv, null);
            Assert.AreEqual("unknown", new PlatformDetector().DetectDesktopEnvironment());
        }

        [Test]
        public void Detect_SessionType_ReadsEnvVar()
        {
            Environment.SetEnvironmentVariable(SessionEnv, "wayland");
            Assert.AreEqual("wayland", new PlatformDetector().DetectSessionType());
        }

        [Test]
        public void IsHyprland_True_WhenSignatureSet()
        {
            Environment.SetEnvironmentVariable(HyprlandEnv, "abc123");
            Assert.IsTrue(new PlatformDetector().IsHyprland());
        }

        [Test]
        public void IsHyprland_False_WhenUnset()
        {
            Environment.SetEnvironmentVariable(HyprlandEnv, null);
            Assert.IsFalse(new PlatformDetector().IsHyprland());
        }

        [Test]
        public void IsX11_True_WhenSessionIsX11()
        {
            Environment.SetEnvironmentVariable(SessionEnv, "x11");
            Assert.IsTrue(new PlatformDetector().IsX11());
            Assert.IsFalse(new PlatformDetector().IsWayland());
        }

        [Test]
        public void IsWayland_True_WhenSessionIsWayland()
        {
            Environment.SetEnvironmentVariable(SessionEnv, "wayland");
            Assert.IsTrue(new PlatformDetector().IsWayland());
            Assert.IsFalse(new PlatformDetector().IsX11());
        }

        [Test]
        public void GetCapabilities_ReturnsNonNull()
        {
            var caps = new PlatformDetector().GetCapabilities();
            Assert.IsNotNull(caps);
            Assert.IsNotNull(caps.BackendName);
        }

        [Test]
        public void GetCapabilities_Hyprland_BackendName()
        {
            Environment.SetEnvironmentVariable(HyprlandEnv, "sig");
            Environment.SetEnvironmentVariable(SessionEnv, "wayland");
            var caps = new PlatformDetector().GetCapabilities();
            Assert.AreEqual("Hyprland", caps.BackendName);
            Assert.IsTrue(caps.SupportsTransparency);
        }

        [Test]
        public void GetCapabilities_X11_Session()
        {
            Environment.SetEnvironmentVariable(HyprlandEnv, null);
            Environment.SetEnvironmentVariable(SessionEnv, "x11");
            var caps = new PlatformDetector().GetCapabilities();
            Assert.AreEqual("X11", caps.BackendName);
            Assert.IsTrue(caps.SupportsDesktopSitting);
        }
    }
}