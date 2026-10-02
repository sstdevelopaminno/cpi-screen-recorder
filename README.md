# CPI Screen Recorder

**CPI Screen Recorder** is a focused Windows screen-recording utility for CUTTING POINT INNOVATION CO., LTD. It is intended for CpiPOS product reviews, customer manuals, training videos, and feature walkthroughs.

## v0.1 goals

- Modern company-themed desktop UI
- Select a monitor and record at its native resolution
- MP4 / H.264 High profile
- 30 or 60 FPS
- Cursor capture and optional click highlight
- High-fidelity SDR-oriented capture settings for UI/tutorial recording
- Local-only files — no upload or cloud dependency
- Self-contained Windows x64 installer built by GitHub Actions

## Build

The repository includes a Windows GitHub Actions workflow. Every push to `main` that changes the application, installer, or workflow triggers a build.

Artifacts:

- `CPI-Screen-Recorder-v0.1-portable`
- `CPI-Screen-Recorder-v0.1-Setup`

The installer artifact contains `CPI-Screen-Recorder-Setup.exe`.

## Local development

Requirements:

- Windows 10/11 x64
- .NET 8 SDK (development only)

```powershell
dotnet restore .\src\CpiScreenRecorder\CpiScreenRecorder.csproj
dotnet run --project .\src\CpiScreenRecorder\CpiScreenRecorder.csproj
```

## Color fidelity note

v0.1 records the selected monitor at native resolution using a standard SDR desktop capture path and high-quality H.264 settings. For product-review footage where UI color matching is critical, record with Windows HDR disabled on the selected display. HDR-to-SDR conversion can change brightness and saturation regardless of the recording application.

## Data & privacy

The app stores recordings only in the folder selected by the user. It does not upload recordings or require an online account.

## Third-party component

Screen capture/encoding is implemented with `ScreenRecorderLib` 7.0.1 (MIT), which uses Microsoft Media Foundation for H.264 encoding.
