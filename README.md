# CPI Screen Recorder

**CPI Screen Recorder** is a Windows screen-recording utility for CUTTING POINT INNOVATION CO., LTD. It is designed for CpiPOS product reviews, customer manuals, training videos, and feature walkthroughs.

## Stable baseline

stable/v0.2.2 points to the tested v0.2.2 Windows build and is kept as the rollback baseline.

## v0.3.1 — stability candidate

- Full-monitor capture with multi-monitor selection
- Application-window capture with Windows 10 stability fallback
- Drag-to-select screen region
- MP4 / H.264 High profile
- 30 or 60 FPS
- Cursor capture and optional click highlight
- Microphone recording from built-in, USB, headset, or other Windows input devices
- System-audio loopback recording
- Microphone + system audio mixed into one MP4
- Real-time microphone level meter before recording; monitoring pauses during active capture to reduce overhead
- Pause / Resume recording without creating a new file
- Global hotkeys: F8 start / stop, F9 pause / resume
- Webcam overlay using a connected Windows camera
- Webcam position: top-left, top-right, bottom-left, bottom-right
- Webcam size presets: small, medium, large
- User-selectable output folder
- Responsive company-branded UI
- Self-contained Windows x64 installer built by GitHub Actions

## Build

Every push to main that changes the application, installer, or workflow triggers a Windows build.

Artifacts:
- CPI-Screen-Recorder-v0.3.1-portable
- CPI-Screen-Recorder-v0.3.1-Setup

The installer artifact contains CPI-Screen-Recorder-v0.3.1-Setup.exe.

## Capture notes

For product-review footage where UI color matching is critical, record with Windows HDR disabled on the selected display.

On Windows 10, application-window mode uses the tested desktop-duplication crop fallback when direct window capture is not dependable. Keep the target application visible and avoid moving it during that recording mode.

## Audio notes

Microphone devices are read from Windows capture devices. The real-time meter is a monitoring view only and does not change the recorded gain.

System audio is captured from Windows loopback audio. Microphone and system audio may be enabled together and are mixed into the same video file.

## Data & privacy

The app stores recordings only in the folder selected by the user. It does not upload recordings and does not require an online account.

## Third-party components

- ScreenRecorderLib 7.0.1 (MIT) for screen/video/audio capture and encoding
- NAudio 2.2.1 (MIT) for Windows microphone level monitoring