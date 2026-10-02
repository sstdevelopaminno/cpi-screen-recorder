# CPI Screen Recorder

**CPI Screen Recorder** is a Windows screen-recording utility for CUTTING POINT INNOVATION CO., LTD. It is designed for CpiPOS product reviews, customer manuals, training videos, and feature walkthroughs.

## v0.2

- Modern company-themed desktop UI
- Three capture modes:
  - full monitor
  - application window
  - drag-to-select screen region
- Multi-monitor selection with monitor name and resolution
- MP4 / H.264 High profile
- 30 or 60 FPS
- Native-resolution capture and high-fidelity SDR-oriented settings
- Cursor capture and optional click highlight
- Microphone recording from built-in or external Windows input devices
- System-audio recording from the selected Windows playback device
- Microphone + system audio can be mixed into the same MP4
- Independent microphone and system-audio volume controls
- User-selectable output folder
- Recording timer and status
- Open latest file / reveal in Explorer
- Local-only settings
- Self-contained Windows x64 installer built by GitHub Actions

## Build

Every push to `main` that changes the application, installer, or workflow triggers a Windows build.

Artifacts:

- `CPI-Screen-Recorder-v0.2-portable`
- `CPI-Screen-Recorder-v0.2-Setup`

The installer artifact contains `CPI-Screen-Recorder-Setup.exe`.

## Local development

Requirements:

- Windows 10/11 x64
- .NET 8 SDK (development only)

```powershell
dotnet restore .\src\CpiScreenRecorder\CpiScreenRecorder.csproj -p:Platform=x64
dotnet run --project .\src\CpiScreenRecorder\CpiScreenRecorder.csproj -p:Platform=x64
```

## Capture notes

For product-review footage where UI color matching is critical, record with Windows HDR disabled on the selected display. HDR-to-SDR conversion can alter brightness and saturation independently of the recorder.

Window capture records only the selected application window. Region capture lets the user drag a frame over a selected monitor and stores the selected pixel dimensions.

## Audio notes

Microphone devices are read from Windows capture devices, so built-in laptop microphones, USB microphones, headsets, audio interfaces, and other active input devices can be selected.

System audio is captured from Windows loopback audio. Microphone and system audio may be enabled together and are mixed into the same video file.

## Data & privacy

The app stores recordings only in the folder selected by the user. It does not upload recordings and does not require an online account.

## Third-party component

Screen capture/encoding is implemented with `ScreenRecorderLib` 7.0.1 (MIT), which uses Microsoft Media Foundation for H.264 encoding.
