# CPI Screen Recorder Roadmap

## Stable baseline — v0.2.2

Branch: stable/v0.2.2

This is the rollback point for the tested stable recorder before the v0.3 feature set.

## v0.3.1 — current stability candidate

- Windows 10/11 x64 desktop application
- Full-monitor / application-window / region capture
- Multi-monitor selection
- MP4 / H.264 High profile
- 30 FPS and 60 FPS
- Cursor capture and click highlight
- Microphone + system audio
- Real-time microphone level meter (paused during active capture to reduce overhead)
- Pause / Resume
- Global F8 start/stop hotkey
- Global F9 pause/resume hotkey
- Webcam overlay
- Webcam corner and size controls
- Responsive company UI
- Adaptive hardware H.264 encoding with software compatibility fallback
- Automated unit-test gate in GitHub Actions
- Refactored MainWindow code-behind and shared UI theme resources
- Self-contained Windows installer

## Later

- Webcam live preview inside the settings panel
- Configurable hotkey editor
- Noise suppression / compressor / limiter
- Separate audio tracks
- Multiple monitors merged into one canvas
- Streaming
- Cloud upload

## Separate future project

Video editing will not be added to this repository. A separate GitHub repository will be created for CPI Video Editor after the recorder v0.3 line is stable.