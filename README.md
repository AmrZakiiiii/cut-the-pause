# Cut The Pause

Cut The Pause is a mac-first desktop app for solo creators who are tired of manually trimming awkward silence between spoken lines. It analyzes a video, detects speech gaps, lets you review the proposed cuts, and exports a cleaned version with the quiet sections removed.

## Current Scope

- Import a single MP4 or MOV file.
- Detect speech gaps with a Silero VAD ONNX pipeline when the model is available.
- Fall back to an energy-based detector when the ONNX model is missing.
- Review each detected cut and disable any false positives before export.
- Export a trimmed MP4 using FFmpeg with audio and video kept in sync.

## Repository Layout

- `src/CutThePause.App`: Avalonia desktop UI.
- `src/CutThePause.Core`: timeline models and cut-planning logic.
- `src/CutThePause.Infrastructure`: FFmpeg integration, metadata extraction, and VAD adapters.
- `tests`: unit tests for planning, fallback detection, and FFmpeg command generation.

## Local Development

1. Install .NET 8.
2. Install FFmpeg and make sure `ffmpeg` and `ffprobe` are on your `PATH`, or set `CUTTHEPAUSE_FFMPEG_PATH` and `CUTTHEPAUSE_FFPROBE_PATH`.
3. Optionally download the Silero model:

   ```bash
   ./scripts/download-silero-model.sh
   ```

4. Restore and run:

   ```bash
   dotnet restore CutThePause.sln
   dotnet run --project src/CutThePause.App/CutThePause.App.csproj
   ```

If the Silero model is not present, the app still works with the built-in energy detector and shows a warning in the review panel.

## Packaging

- CI runs on macOS.
- Release packaging creates a zipped `.app` bundle.
- The release workflow bundles FFmpeg binaries and can include the Silero ONNX model when it is downloaded into `assets/models/`.

## License

The repository is licensed under MIT. FFmpeg and the Silero VAD model remain subject to their own licenses; see [THIRD_PARTY_NOTICES.md](/Users/amrzaky/Desktop/Video Silence Removal/THIRD_PARTY_NOTICES.md).
