using System;
using System.IO;
using Mate.Bootstrap;
using NUnit.Framework;

[TestFixture]
public class BootstrapArgsTests
{
    [Test]
    public void Parses_ProjectPathArg()
    {
        var dir = "/tmp/my-mate";
        var result = BootstrapArgs.ParseProjectPath(new[] { "--projectPath", dir });
        Assert.AreEqual(dir, result);
    }

    [Test]
    public void ProjectPathArg_CanAppearAnywhere()
    {
        var dir = "/tmp/another";
        var result = BootstrapArgs.ParseProjectPath(new[] { "-batchmode", "--projectPath", dir, "--foo" });
        Assert.AreEqual(dir, result);
    }

    [Test]
    public void MissingArg_FallsBackToEnv()
    {
        var prev = Environment.GetEnvironmentVariable(BootstrapArgs.ProjectDirEnv);
        try
        {
            Environment.SetEnvironmentVariable(BootstrapArgs.ProjectDirEnv, "/env/project");
            var result = BootstrapArgs.ParseProjectPath(new[] { "-batchmode" });
            Assert.AreEqual("/env/project", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapArgs.ProjectDirEnv, prev);
        }
    }

    [Test]
    public void NoArgNoEnv_FallsBackToCurrentDir()
    {
        var prev = Environment.GetEnvironmentVariable(BootstrapArgs.ProjectDirEnv);
        try
        {
            Environment.SetEnvironmentVariable(BootstrapArgs.ProjectDirEnv, null);
            var result = BootstrapArgs.ParseProjectPath(Array.Empty<string>());
            Assert.AreEqual(Directory.GetCurrentDirectory(), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapArgs.ProjectDirEnv, prev);
        }
    }

    [Test]
    public void EmptyValueArg_NotUsed()
    {
        var prev = Environment.GetEnvironmentVariable(BootstrapArgs.ProjectDirEnv);
        try
        {
            Environment.SetEnvironmentVariable(BootstrapArgs.ProjectDirEnv, "/env/fallback");
            var result = BootstrapArgs.ParseProjectPath(new[] { "--projectPath", "" });
            Assert.AreEqual("/env/fallback", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BootstrapArgs.ProjectDirEnv, prev);
        }
    }
}
