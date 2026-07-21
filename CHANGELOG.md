# Changelog

All notable changes to DM Video Player will be documented in this file.

## work in progress

### ⭐ New Features

- allow to move timecode / BPM panel
- adapt displayed components with application width 
- set a minimum width and height for application

### 🐛 Bug Fixes

- prevent to play/pause on mouse click if app has not the focus yet

## [1.2.0] - 2026-07-21

### ⭐ New Features

- Add a dedicated setting windows
- Move audio output selection to settings windows
- Move timecode display checkbox to settings windows
- Add BPM display checkbox to settings windows
- Add default vidéo folder to settings windows
- Allow to move in video based on mouse wheel 
- Add mouse wheel move step in settings

### 🐛 Bug Fixes

- Restore drag'n drop feature for quick vidéo playing

### 📝 Documentation

- Add CHANGELOG.md

## [1.1.0] - 2026-07-15

### ⭐ New Features

- Cubase tempo track management

### 🐛 Bug Fixes

- Fix very fast pause/play switch break BPM led
- Fix timecode interpolation for a smoother timecode display with frame
- Fix timecode size and label


### 🔨 Dependency Upgrades

- Update dependencies


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