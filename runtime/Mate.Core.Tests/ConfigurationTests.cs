using System;
using System.IO;
using NUnit.Framework;
using Mate.Core;

namespace Mate.Core.Tests
{
    [TestFixture]
    public class ConfigurationTests
    {
        private string _testDir;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "mate-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Test]
        public void Load_SettingsFile()
        {
            var json = @"{ ""soundThreshold"": 0.5, ""fpsLimit"": 60 }";
            File.WriteAllText(Path.Combine(_testDir, "settings.json"), json);

            var config = new FileConfiguration(_testDir);
            Assert.AreEqual(0.5f, config.GetFloat("soundThreshold", 0.2f));
            Assert.AreEqual(60, config.GetInt("fpsLimit", 90));
        }

        [Test]
        public void Load_DefaultValues_WhenMissing()
        {
            var config = new FileConfiguration(_testDir);
            Assert.AreEqual(0.2f, config.GetFloat("soundThreshold", 0.2f));
            Assert.AreEqual(90, config.GetInt("fpsLimit", 90));
        }

        [Test]
        public void Save_And_Reload()
        {
            var config = new FileConfiguration(_testDir);
            config.Set("soundThreshold", 0.8f);
            config.Save();

            var config2 = new FileConfiguration(_testDir);
            Assert.AreEqual(0.8f, config2.GetFloat("soundThreshold", 0.2f));
        }

        [Test]
        public void GetString_ReturnsDefault_WhenMissing()
        {
            var config = new FileConfiguration(_testDir);
            Assert.AreEqual("default", config.GetString("nonexistent", "default"));
        }
    }
}