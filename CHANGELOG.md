# Changelog

All notable changes to DM Video Player will be documented in this file.

## [Unreleased]

### 🐛 Bug Fixes

- Fix very fast pause/play switch break BPM led ([f96b782](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/f96b782))
- Fix timecode interpolation for a smoother timecode display with frame ([a380961](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/a380961))

### ⭐ New Features

- Cubase tempo track management final working version ([3aa4d59](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/3aa4d59))
- Cubase tempo track management (first implementation) ([cae25cc](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/cae25cc))

### 🔨 Dependency Upgrades

- Update dependencies ([db07237](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/db07237))

### 🔧 Maintenance

- Fix timecode size and label ([bd3c938](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/bd3c938))

## [1.0.0] - 2026-01-24

### 🎉 Initial Release

First public release of DM Video Player - A minimalist video player based on VLC dedicated to musicians and people with hearing impairments.

#### Key Features

- Classic video playback powered by LibVLC
- Dynamically add extra audio tracks (STEMS) to videos
- Multiple audio output routing capabilities
- Subtitle support with toggle functionality
- Timecode display with frame accuracy
- Keyboard shortcuts (0 to stop, space to play/pause)
- Mouse shortcuts (single click to play/pause, double click for fullscreen)
- Cross-platform support via AvaloniaUI

#### 📝 Documentation

- Add screenshot to README ([811385f](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/811385f))
- Update copyright year and name in LICENSE file ([76fadc2](https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/commit/76fadc2))

#### ⚙️ Technical Stack

- Built with .NET 10
- AvaloniaUI for cross-platform UI
- LibVLCSharp for media playback
- Supports all audio/video formats compatible with VLC

---

**Note**: This project is a "vibe coding" experiment created with GitHub Copilot assistance, dedicated to Didier Martini, a musician with hearing loss. The player enables musicians to isolate and route specific audio tracks (STEMS) for a more accessible listening experience.

[Unreleased]: https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/compare/1.0.0...HEAD
[1.0.0]: https://github.com/Fabrice-Deshayes-aka-Xtream/DMVideoPlayer/releases/tag/1.0.0
