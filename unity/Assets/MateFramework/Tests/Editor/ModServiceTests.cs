using System.IO;
using Mate.Core;
using Mate.Interfaces;
using Mate.Mods;
using NUnit.Framework;

[TestFixture]
public class ModServiceTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mate-mods-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Test]
    public void ModService_ImplementsIModService()
    {
        var svc = new ModService();
        Assert.IsInstanceOf<IModService>(svc);
    }

    [Test]
    public void ModService_EmptyModsDir()
    {
        var svc = new ModService();
        var result = svc.LoadMods(_testDir).Result;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, svc.InstalledMods.Count);
    }

    [Test]
    public void ModService_FindsModWithToml()
    {
        var modDir = Path.Combine(_testDir, "custom-sounds");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "mod.toml"), @"
name = ""custom-sounds""
version = ""1.0.0""
description = ""Custom drag sounds""
");

        var svc = new ModService();
        svc.LoadMods(_testDir).Wait();
        Assert.AreEqual(1, svc.InstalledMods.Count);
        Assert.AreEqual("custom-sounds", svc.InstalledMods[0].Name);
        Assert.AreEqual("1.0.0", svc.InstalledMods[0].Version);
        Assert.AreEqual("Custom drag sounds", svc.InstalledMods[0].Description);
    }

    [Test]
    public void ModService_DirWithoutToml_IsSkipped()
    {
        var modDir = Path.Combine(_testDir, "no-manifest");
        Directory.CreateDirectory(modDir);

        var svc = new ModService();
        svc.LoadMods(_testDir).Wait();
        Assert.AreEqual(0, svc.InstalledMods.Count);
    }

    [Test]
    public void ModService_ReloadMods()
    {
        var svc = new ModService();
        svc.LoadMods(_testDir).Wait();
        Assert.AreEqual(0, svc.InstalledMods.Count);

        var modDir = Path.Combine(_testDir, "test-mod");
        Directory.CreateDirectory(modDir);
        File.WriteAllText(Path.Combine(modDir, "mod.toml"), @"
name = ""test-mod""
version = ""1.0.0""
");

        svc.ReloadMods().Wait();
        Assert.AreEqual(1, svc.InstalledMods.Count);
    }

    [Test]
    public void ModService_NoModsDir_DoesNotFail()
    {
        var nonexistentDir = Path.Combine(_testDir, "no-such-dir");
        var svc = new ModService();
        var result = svc.LoadMods(nonexistentDir).Result;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, svc.InstalledMods.Count);
    }
}