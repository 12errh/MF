using System.IO;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Platform;
using Mate.Window;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WindowServiceTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mate-win-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void WriteToml(string content) => File.WriteAllText(Path.Combine(_dir, "mate.toml"), content);

    [Test]
    public async Task Initialize_AppliesAlwaysOnTop_FromConfig()
    {
        WriteToml("[window]\nalways_on_top = true\n");
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        var result = await service.Initialize(System.IntPtr.Zero);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(backend.Initialized);
        Assert.IsTrue(backend.AlwaysOnTop);
    }

    [Test]
    public async Task Initialize_SetsWindowTitle_FromProjectName()
    {
        WriteToml("[project]\nname = \"my-mate\"\n");
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        await service.Initialize(System.IntPtr.Zero);

        Assert.AreEqual("my-mate", backend.WindowTitle);
    }

    [Test]
    public async Task Initialize_AppliesBorderlessAndClickThrough_WhenConfigured()
    {
        WriteToml("[window]\ntransparent = true\nclick_through = true\n");
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        await service.Initialize(System.IntPtr.Zero);

        Assert.IsTrue(backend.Borderless);
        Assert.IsTrue(backend.ClickThrough);
    }

    [Test]
    public async Task Initialize_MapsWindowType_Dock()
    {
        WriteToml("[window]\nwindow_type = \"dock\"\n");
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        await service.Initialize(System.IntPtr.Zero);

        Assert.AreEqual(1, backend.WindowType);
    }

    [Test]
    public async Task Initialize_AppliesInitialPosition_FromXY()
    {
        WriteToml("[window]\ninitial_position = \"100,200\"\n");
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        await service.Initialize(System.IntPtr.Zero);

        Assert.IsTrue(backend.PositionSet);
        Assert.AreEqual(new Vector2Int(100, 200), backend.Position);
    }

    [Test]
    public async Task Initialize_Fails_WhenBackendCannotOpenDisplay()
    {
        WriteToml("[window]\nalways_on_top = true\n");
        var backend = new FailingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        var result = await service.Initialize(System.IntPtr.Zero);

        Assert.IsFalse(result.IsSuccess);
    }

    [Test]
    public async Task Operations_ReturnFailure_BeforeInitialize()
    {
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));

        var pos = await service.GetPosition();
        Assert.IsFalse(pos.IsSuccess);
    }

    [Test]
    public async Task GetWindowInfo_MapsBackendData()
    {
        WriteToml("[window]\ntransparent = true\n");
        var backend = new RecordingBackend();
        var service = new WindowService(backend, new MateTomlConfig(_dir));
        await service.Initialize(System.IntPtr.Zero);

        var info = await service.GetWindowInfo(System.IntPtr.Zero);

        Assert.IsTrue(info.IsSuccess);
        Assert.AreEqual("Test-class", info.Value.ClassName);
    }

    private class RecordingBackend : IWindowBackend
    {
        public bool Initialized;
        public bool AlwaysOnTop;
        public bool Borderless;
        public bool ClickThrough;
        public int WindowType = -1;
        public bool PositionSet;
        public Vector2Int Position;
        public string WindowTitle;

        public bool Initialize(System.IntPtr unityWindow)
        {
            Initialized = true;
            return true;
        }

        public bool SetAlwaysOnTop(bool value) { AlwaysOnTop = value; return true; }
        public bool SetBorderless(bool value) { Borderless = value; return true; }
        public bool SetClickThrough(bool value) { ClickThrough = value; return true; }
        public bool SetWindowType(int type) { WindowType = type; return true; }
        public bool SetWindowPosition(Vector2Int position) { PositionSet = true; Position = position; return true; }
        public bool SetWindowTitle(string title) { WindowTitle = title; return true; }

        public bool GetWindowPosition(out Vector2Int position) { position = Vector2Int.zero; return true; }
        public bool GetWindowSize(out Vector2Int size) { size = Vector2Int.zero; return true; }
        public bool SetWindowSize(Vector2Int size) => true;
        public bool HideFromTaskbar(bool value) => true;
        public bool GetMousePosition(out Vector2Int position) { position = Vector2Int.zero; return true; }
        public System.Collections.Generic.List<MonitorInfoData> GetAllMonitors() => new();
        public System.Collections.Generic.List<System.IntPtr> GetAllVisibleWindows() => new();
        public WindowInfoData GetWindowInfo(System.IntPtr handle) =>
            new(handle, Vector2Int.zero, Vector2Int.zero, "Test-class");
        public void Dispose() { }
    }

    private class FailingBackend : IWindowBackend
    {
        public bool Initialize(System.IntPtr unityWindow) => false;
        public bool GetWindowPosition(out Vector2Int position) { position = Vector2Int.zero; return false; }
        public bool SetWindowPosition(Vector2Int position) => false;
        public bool GetWindowSize(out Vector2Int size) { size = Vector2Int.zero; return false; }
        public bool SetWindowSize(Vector2Int size) => false;
        public bool SetAlwaysOnTop(bool value) => false;
        public bool SetBorderless(bool value) => false;
        public bool SetClickThrough(bool value) => false;
        public bool HideFromTaskbar(bool value) => false;
        public bool SetWindowType(int type) => false;
        public bool SetWindowTitle(string title) => false;
        public bool GetMousePosition(out Vector2Int position) { position = Vector2Int.zero; return false; }
        public System.Collections.Generic.List<MonitorInfoData> GetAllMonitors() => new();
        public System.Collections.Generic.List<System.IntPtr> GetAllVisibleWindows() => new();
        public WindowInfoData GetWindowInfo(System.IntPtr handle) => default;
        public void Dispose() { }
    }
}