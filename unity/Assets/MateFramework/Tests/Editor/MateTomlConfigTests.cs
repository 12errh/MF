using System.IO;
using Mate.Core;
using NUnit.Framework;

[TestFixture]
public class MateTomlConfigTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mate-toml-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void WriteToml(string content) => File.WriteAllText(Path.Combine(_dir, "mate.toml"), content);

    private const string FullToml = @"
[project]
name = ""demo""
runtime = ""1.0.0""

[character]
model = ""assets/avatar.vrm""
head_sensitivity = 1.5
eye_sensitivity = 0.8
spine_sensitivity = 0.3
head_max_angle = 25
spine_max_angle = 12

[audio]
threshold = 0.35
allowed_apps = [""spotify"", ""firefox""]

[animation]
dance_switch_time = 12.0
idle_switch_time = 20.0
dance_animation = ""dance_3""

[system]
tray_icon = ""assets/icon.png""
tray_tooltip = ""My Mate""

[mods]
mods_path = ""custom-mods/""

[ai]
enabled = true
model = ""phi3:mini""
base_url = ""http://localhost:11434""
";

    [Test]
    public void MapsManifestKeys_ToServiceKeys()
    {
        WriteToml(FullToml);
        var cfg = new MateTomlConfig(_dir);

        Assert.AreEqual("assets/avatar.vrm", cfg.GetString("modelPath", ""));
        Assert.AreEqual(0.35f, cfg.GetFloat("soundThreshold", 0.2f), 0.001f);
        Assert.AreEqual(12.0f, cfg.GetFloat("danceSwitchTime", 15.0f), 0.001f);
        Assert.AreEqual(20.0f, cfg.GetFloat("idleSwitchTime", 30.0f), 0.001f);
        Assert.AreEqual(1.5f, cfg.GetFloat("headSensitivity", 1.0f), 0.001f);
        Assert.AreEqual(0.8f, cfg.GetFloat("eyeSensitivity", 1.0f), 0.001f);
        Assert.AreEqual(0.3f, cfg.GetFloat("spineSensitivity", 0.5f), 0.001f);
        Assert.AreEqual(25f, cfg.GetFloat("headMaxAngle", 20f), 0.001f);
        Assert.AreEqual(12f, cfg.GetFloat("spineMaxAngle", 10f), 0.001f);
        Assert.AreEqual("assets/icon.png", cfg.GetString("trayIcon", ""));
        Assert.AreEqual("My Mate", cfg.GetString("trayTooltip", ""));
        Assert.AreEqual("custom-mods/", cfg.GetString("modsPath", "mods/"));
    }

    [Test]
    public void AllowedApps_Joined_WithCommas()
    {
        WriteToml(FullToml);
        var cfg = new MateTomlConfig(_dir);
        Assert.AreEqual("spotify,firefox", cfg.GetString("allowedApps", "spotify"));
    }

    [Test]
    public void AiKeys_Resolve()
    {
        WriteToml(FullToml);
        var cfg = new MateTomlConfig(_dir);
        Assert.AreEqual("phi3:mini", cfg.GetString("ai.model", "llama3.2"));
        Assert.AreEqual("http://localhost:11434", cfg.GetString("ai.baseUrl", ""));
        Assert.IsTrue(cfg.GetBool("ai.enabled", false));
    }

    [Test]
    public void DanceAnimation_ExtensionKey_Resolves()
    {
        WriteToml(FullToml);
        var cfg = new MateTomlConfig(_dir);
        Assert.AreEqual("dance_3", cfg.GetString("danceAnimation", "dance_0"));
    }

    [Test]
    public void MissingKeys_FallBackToDefaults()
    {
        WriteToml("[project]\nname = \"minimal\"\nruntime = \"1.0.0\"\n");
        var cfg = new MateTomlConfig(_dir);

        Assert.AreEqual(0.2f, cfg.GetFloat("soundThreshold", 0.2f), 0.001f);
        Assert.AreEqual("spotify", cfg.GetString("allowedApps", "spotify"));
        Assert.AreEqual("llama3.2", cfg.GetString("ai.model", "llama3.2"));
        Assert.AreEqual("", cfg.GetString("modelPath", ""));
    }

    [Test]
    public void MissingFile_AllDefaults()
    {
        var cfg = new MateTomlConfig(_dir);
        Assert.AreEqual(1.0f, cfg.GetFloat("headSensitivity", 1.0f), 0.001f);
        Assert.AreEqual("dance_0", cfg.GetString("danceAnimation", "dance_0"));
        Assert.IsFalse(cfg.GetBool("ai.enabled", false));
    }

    [Test]
    public void InvalidFile_AllDefaults_NoThrow()
    {
        WriteToml("not valid toml {{{{");
        var cfg = new MateTomlConfig(_dir);
        Assert.AreEqual(0.2f, cfg.GetFloat("soundThreshold", 0.2f), 0.001f);
    }

    [Test]
    public void CommentLines_Ignored()
    {
        WriteToml("# a comment\n[audio]\n# another comment\nthreshold = 0.5\n");
        var cfg = new MateTomlConfig(_dir);
        Assert.AreEqual(0.5f, cfg.GetFloat("soundThreshold", 0.2f), 0.001f);
    }
}
