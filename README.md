# Hand Tracking - Unity + MediaPipe (Android)

Real-time hand landmark detection on Android using **Unity 6** and **MediaPipe**. The app captures live camera feed, runs ML inference on-device, and renders a 21-point skeleton overlay with a 3D sphere that follows the index finger tip - all at 30 FPS with no cloud dependency.

---

## Demo

[![Watch Demo on YouTube](https://img.youtube.com/vi/WgTET3-1kDE/hqdefault.jpg)](https://www.youtube.com/embed/WgTET3-1kDE)

[<img src="https://img.youtube.com/vi/WgTET3-1kDE/hqdefault.jpg" width="640" height="480"
/>](https://www.youtube.com/embed/WgTET3-1kDE)

---

## Features

- **21-point hand skeleton** - full hand landmarks rendered as colored dots and connecting lines directly on the camera feed
- **3D finger sphere** - a procedurally created sphere that sticks to the index finger tip (landmark 8) in 3D world space
- **Live camera feed** - front camera at 640×480 @ 30 FPS, displayed on a 3D Quad so 3D objects can render on top of the video
- **FPS counter** - color-coded overlay
- **Thread-safe ML pipeline** - MediaPipe callbacks arrive on a background thread; a `UnityMainThreadDispatcher` marshals them safely to the Unity main thread
- **Android runtime permissions** - camera permission is requested at runtime (API 24+)

---

## How It Works

```
AppController  ──►  PermissionManager  (Android camera permission)
                         │
                         ▼
                    CameraManager  (WebCamTexture 640×480 @ 30 FPS)
                         │ OnFrameReady event (every frame)
                         ▼
               HandTrackingController  (MediaPipe HandLandmarker)
                    DetectAsync()  ──►  background thread
                    callback      ──►  UnityMainThreadDispatcher
                         │
               ┌──────────────────┐
               │                  │
               ▼                  ▼
    HandOverlayRenderer    FingerSphereController
    (21 dots + 25 lines    (3D sphere on landmark 8,
     on Canvas)             placed in front of VideoQuad)
```

---

## Architecture

| Layer | Component | Responsibility |
|-------|-----------|----------------|
| Orchestration | `AppController` | Sequential async startup: permissions → camera → MediaPipe |
| Infrastructure | `CameraManager` | WebCamTexture lifecycle, front-camera selection, `OnFrameReady` event |
| Infrastructure | `PermissionManager` | Android runtime camera permission request |
| Business Logic | `HandTrackingController` | MediaPipe `HandLandmarker` init, `DetectAsync`, result events |
| Visualization | `HandOverlayRenderer` | 21 UI dots + 25 UI lines drawn on Canvas |
| Visualization | `FingerSphereController` | Procedural 3D sphere following index finger tip |
| Visualization | `VideoQuadDisplay` | 3D Quad with `WebCamTexture`; hides `RawImage` after first frame |
| Visualization | `FPSCounter` | Real-time FPS with color thresholds |
| Utility | `UnityMainThreadDispatcher` | Thread-safe queue; flushes MediaPipe callbacks in `Update()` |

---

## Tech Stack

| | |
|---|---|
| Engine | Unity 6.0.62f1 |
| ML Framework | [MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin) v0.16.x |
| ML Model | `hand_landmarker.bytes` (7.5 MB, bundled in StreamingAssets) |
| Platform | Android |
| Scripting Backend | IL2CPP (required for MediaPipe native libs) |
| Graphics API | OpenGL ES 3.0 |

---

## Project Structure

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── AppController.cs
│   │   │   ├── CameraManager.cs
│   │   │   ├── HandTrackingController.cs
│   │   │   └── PermissionManager.cs
│   │   ├── Visualization/
│   │   │   ├── HandOverlayRenderer.cs
│   │   │   ├── FingerSphereController.cs
│   │   │   ├── VideoQuadDisplay.cs
│   │   │   └── FPSCounter.cs
│   │   └── Utils/
│   │       └── UnityMainThreadDispatcher.cs
│   └── Content/
│       └── Scenes/
│           └── HandTrackingScene.unity
├── StreamingAssets/
│   └── hand_landmarker.bytes       ← MediaPipe model (do not move)
└── Plugins/
    └── Android/
        └── mainTemplate.gradle     ← noCompress for MediaPipe .so libs
```

---

## Build & Run

**Android APK (Development):**
1. Open the project in **Unity 6.0.62f1**
2. `File → Build Settings → Android → Switch Platform`
3. Ensure **IL2CPP** scripting backend and **ARM64** target architecture are set
4. Click **Build**
