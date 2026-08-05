# Dependency Map: Mate-Engine-Linux-Port

## Critical God Objects (Ranked by Risk)

### 1. SaveLoadHandler.Instance.data — CRITICAL RISK

This is the single most dangerous architectural element. A flat `SettingsData` class with 50+ fields accessed by 30+ MonoBehaviours via a global singleton. Any change to SettingsData can break any consumer. The JSON serialization creates implicit version coupling.

**Consumers:** 30+ classes
**Fields:** 50+ (mix of animation params, visual settings, AI config, platform flags)
**Persistence:** JSON file on disk
**Risk:** Any schema change can silently corrupt settings or break consumers

### 2. WindowManager.Instance — HIGH RISK

Second god object. 2618-line MonoBehaviour serving as both X11 library binding layer and application-level window manager. Mixes concerns: P/Invoke declarations, X11 protocol, window management, input handling, cursor management, monitor enumeration.

**Consumers:** 10+ classes (AvatarMouseTracking, AvatarWindowHandler, AvatarBigScreenHandler, UniWindowController, etc.)
**Risk:** Changes to window management logic affect every desktop-integrated feature

### 3. Singleton<T> Base Class — MEDIUM RISK

Generic MonoBehaviour singleton with lazy FindFirstObjectByType. Used by HyprlandManager, DBusNotificationHelper, and indirectly by many classes.

**Risk:** Hides dependencies, makes testing impossible, creates implicit lifecycle coupling

### 4. PulseAudioManager.Instance — MEDIUM RISK

Audio monitoring singleton. Accessed by AvatarAnimatorController for dance triggering.

**Consumers:** AvatarAnimatorController (via IsValidAppPlayingCoroutine)
**Risk:** Audio pipeline is synchronous in places; errors propagate to animation

### 5. MEModLoader.Instance — LOW RISK

Mod loading singleton. Called by VRMLoader after model load.

**Consumers:** VRMLoader
**Risk:** Mod loading is file I/O bound; failures are non-fatal

## Dependency Chains

### Settings Propagation Chain

```
SaveLoadHandler.LoadFromDisk()
  -> SaveLoadHandler.ApplyAllSettingsToAllAvatars()
    -> AvatarAnimatorController (SOUND_THRESHOLD, IDLE_SWITCH_TIME, etc.)
    -> AvatarMouseTracking (enableMouseTracking, headBlend, etc.)
    -> IKFix (enableIK)
    -> AvatarParticleHandler (featureEnabled, selectedTheme)
    -> HandHolder (enableHandHolding)
    -> AvatarFoodController (SetFeatureEnabled)
    -> AvatarWindowHandler (windowSitYOffset)
```

### Window Management Chain

```
WindowManager.OnEnable()
  -> Check XDG_CURRENT_DESKTOP
    -> If Hyprland: Singleton<HyprlandManager>.Instance
    -> If KDE: new KWinManager(connection)
  -> SetXUnityWindow(_unityWindow)
  -> EnableClickThroughTransparency()
  -> LoadCursors()
```

### VRM Loading Chain

```
VRMLoader.Start()
  -> Check SaveLoadHandler.Instance.data.selectedModelPath
  -> LoadVRM(path)
    -> Parse VRM 1.0 or 0.x
    -> FinalizeLoadedModel()
      -> DisableMainModel()
      -> AssignAnimatorController()
      -> InjectComponentsFromPrefab()  // template-based component injection
      -> AvatarLibraryMenu.AddAvatarToLibrary()
      -> MEModLoader.Instance.AssignHandlersForCurrentAvatar()
      -> SettingsHandlerUtility.ReloadAllSettingsHandlers()
```

### Audio-Reactive Dance Chain

```
AvatarAnimatorController.Update()
  -> CheckSoundContinuously() [coroutine, every 2s]
    -> PulseAudioManager.Instance.GetPlayingAudioPrograms()
      -> For each program: check against allowedApps list
      -> PulseAudioManager.Instance.StartMonitoringStream(nodeId)
      -> Check PulseAudioManager.Instance.ProgramPeaks[nodeId] > SOUND_THRESHOLD
    -> If valid: StartDancing()
      -> animator.SetBool(isDancingParam, true)
      -> animator.SetFloat(danceIndexParam, danceState)
```

### Mouse Tracking Chain

```
AvatarMouseTracking.LateUpdate()
  -> WindowManager.Instance.GetMousePosition()
  -> WindowManager.Instance.GetWindowPosition()
  -> Calculate screen-space mouse position
  -> DoHead() -> headBone rotation based on mouse
  -> DoSpine() -> spineBone rotation based on mouse X
  -> DoEye() -> eye bone rotation / VRM LookAt target
```

## Cross-Cutting Dependencies

### Every Avatar Handler Depends On:
1. Animator component (GetComponent<Animator>())
2. Camera.main (static reference)
3. SaveLoadHandler.Instance.data (for settings)
4. WindowManager.Instance (for mouse/position)

### Every Settings Handler Depends On:
1. SaveLoadHandler.Instance.data (read/write)
2. UI components (Slider, Toggle, Dropdown)
3. Unity UI framework (EventSystem)

### Platform Detection Depends On:
1. Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
2. Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")
3. WindowManager._display (X11 display handle)
4. HYPRLAND_INSTANCE_SIGNATURE (for Hyprland)

## Circular Dependency Risks

No true circular dependencies detected, but there are **implicit circular flows**:

1. SaveLoadHandler reads settings -> pushes to AvatarAnimatorController -> AvatarAnimatorController reads PulseAudioManager -> PulseAudioManager is initialized in scene
2. WindowManager.OnEnable -> creates HyprlandManager -> HyprlandManager reads SaveLoadHandler -> SaveLoadHandler is initialized in scene
3. VRMLoader -> FinalizeLoadedModel -> MEModLoader.AssignHandlers -> needs current model -> VRMLoader just set current model

## Interface Boundaries

### IWindowManagerImplementation (existing, good)
- SetWindowPosition, GetWindowPosition
- SetWindowSize, GetWindowSize
- GetMousePosition
- GetWindowPid, GetWindowRect
- SetTopmost, HideFromTaskbar
- SetWindowBorderless, SetWindowType
- GetAllVisibleWindows, GetAllMonitors
- IsWindowVisible, IsWindowFullscreen, IsWindowMaximized
- IsDesktop, IsDock, GetClassName
- GetClientStackingList
- SetXUnityWindow, SetSnapedWindow

This interface is well-designed but needs:
- MonitorInfo struct (not raw IntPtr)
- WindowInfo struct (not raw IntPtr)
- Error handling (currently fails silently)
- Async variants (KWin DBus calls are async)
