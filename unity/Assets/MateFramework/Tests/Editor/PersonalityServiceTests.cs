using System.IO;
using Mate.AI;
using NUnit.Framework;

[TestFixture]
public class PersonalityServiceTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mate-personality-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void WriteToml(string content) =>
        File.WriteAllText(Path.Combine(_testDir, "personality.toml"), content);

    [Test]
    public void PersonalityService_LoadsFromToml()
    {
        WriteToml(@"
name = ""Luna""
greeting = ""Hello! I'm Luna, your desktop companion!""
trait_cheerful = 8
trait_shy = 3
trait_playful = 7
");

        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Luna", service.Name);
        Assert.AreEqual("Hello! I'm Luna, your desktop companion!", service.Greeting);
    }

    [Test]
    public void PersonalityService_GeneratesSystemPrompt()
    {
        WriteToml(@"
name = ""Luna""
greeting = ""Hi there!""
trait_cheerful = 8
trait_playful = 7
");

        var service = new PersonalityService(_testDir);
        var prompt = service.GenerateSystemPrompt();
        Assert.IsNotNull(prompt);
        Assert.IsTrue(prompt.Contains("Luna"));
        Assert.IsTrue(prompt.Contains("cheerful"));
    }

    [Test]
    public void PersonalityService_Defaults_WhenNoFile()
    {
        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Mate", service.Name);
        Assert.AreEqual(string.Empty, service.Greeting);
    }

    [Test]
    public void PersonalityService_GetTraitValue()
    {
        WriteToml(@"
name = ""Test""
trait_cheerful = 5
trait_shy = 2
");

        var service = new PersonalityService(_testDir);
        Assert.AreEqual(5, service.GetTrait("cheerful"));
        Assert.AreEqual(2, service.GetTrait("shy"));
        Assert.AreEqual(5, service.GetTrait("unknown")); // default
    }

    [Test]
    public void PersonalityService_ResponseForEvent()
    {
        WriteToml(@"
name = ""Luna""
response_hello = ""Hi hi! *waves*""
response_idle = ""*yawns*""
");

        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Hi hi! *waves*", service.GetResponseForEvent("hello"));
        Assert.AreEqual("*yawns*", service.GetResponseForEvent("idle"));
    }

    [Test]
    public void PersonalityService_CommentAndBlankLines_AreIgnored()
    {
        WriteToml(@"
# a comment
name = ""Test""

trait_cheerful = 7
");

        var service = new PersonalityService(_testDir);
        Assert.AreEqual("Test", service.Name);
        Assert.AreEqual(7, service.GetTrait("cheerful"));
    }
}