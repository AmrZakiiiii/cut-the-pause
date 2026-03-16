# Contributing

## Workflow

1. Open an issue before large feature work.
2. Keep pull requests focused on one change area.
3. Add or update tests for any behavior change in `Core` or `Infrastructure`.
4. Include screenshots for UI changes.

## Local Setup

- Use .NET 8.0.300 or newer in the 8.0 feature band.
- Install FFmpeg locally for manual end-to-end testing.
- Download the Silero ONNX model with `./scripts/download-silero-model.sh` if you want the primary detector instead of the fallback detector.

## Pull Request Checklist

- `dotnet build CutThePause.sln`
- `dotnet test CutThePause.sln`
- Update docs when behavior or setup changes
