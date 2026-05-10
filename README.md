# UGFW - Unity Game Framework

A comprehensive game development framework for Unity.

## Installation

Add as a git submodule into your Unity project:

```bash
git submodule add https://github.com/invertibleMatrix/unity-game-framework.git Assets/UGFW
```

## Required Dependencies

Install these via Unity Package Manager or OpenUPM:

- **UniTask** - `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10`
- **Reflex** - `https://github.com/gustavopsantos/reflex.git?path=/Assets/Reflex/#14.3.0`
- **DOTween** - Install from Asset Store or OpenUPM
- **Addressables** - `com.unity.addressables` 2.11+
- **Cinemachine** - `com.unity.cinemachine` 3.1+

## Optional Dependencies

These enable additional service implementations:

- **Unity Purchasing** - Enables IAP service
- **Google Mobile Ads / Appodeal** - Enables Ad service
- **Firebase** - Enables Remote Config and Analytics providers
- **Unity Notifications** - Enables Notification service

## Modules

| Module | Description |
|--------|-------------|
| **Core** | StateMachine, AppStateMachine, EventBus, CameraSystem, Prefs, ResourceManagement, UID |
| **UISystem** | Full UI system with screens, fragments, and animation strategies |
| **GameplayCore** | MetaData system, GameModels, game-specific definitions |
| **Services** | Purchase, Ads, Analytics, RemoteConfig, Notifications, Storage |
| **Editor** | UID editor, UI stack visualizer, scene loader, inspector tools |

## Pulling Updates

```bash
cd Assets/UGFW
git pull origin main
```

Or from project root:

```bash
git submodule update --remote Assets/UGFW
```

## Making Changes

```bash
cd Assets/UGFW
git add -A
git commit -m "your change"
git push
```
