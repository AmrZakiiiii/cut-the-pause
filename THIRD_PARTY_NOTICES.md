# Third-Party Notices

These notices describe the direct inputs currently visible in the repository;
they are not legal advice and are not a substitute for checking the exact
artifact and dependency versions you redistribute. See
[docs/COMMERCIAL_LICENSING.md](docs/COMMERCIAL_LICENSING.md) for the release
checklist and distributor obligations.

## Avalonia

- Package: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`
- Version in `src/CutThePause.App/CutThePause.App.csproj`: `11.3.12`
- License: MIT
- Upstream license: [Avalonia licence](https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md)
- Distributor check: preserve the exact package notices and inspect restored transitive/native payloads.

## ONNX Runtime

- Package: `Microsoft.ML.OnnxRuntime`
- Version in `src/CutThePause.Infrastructure/CutThePause.Infrastructure.csproj`: `1.24.1`
- License: MIT
- Upstream license: [ONNX Runtime LICENSE](https://github.com/microsoft/onnxruntime/blob/main/LICENSE)
- Additional notices: [ONNX Runtime ThirdPartyNotices.txt](https://github.com/microsoft/onnxruntime/blob/main/ThirdPartyNotices.txt)
- Distributor check: retain package notices for the exact version and native execution-provider payloads included in the artifact.

## FFmpeg

- Used for audio extraction, metadata reading, and final export.
- Releases should use a deliberately reviewed FFmpeg build and ship the applicable notices with the artifact.
- Read [FFmpeg's official legal guidance](https://ffmpeg.org/legal.html): the default is LGPL v2.1-or-later, while optional GPL components and `--enable-nonfree` change the posture. Check the exact `ffmpeg -version` configuration line and source.
- For an LGPL distribution, provide corresponding source/build information and assess relinking/replacement obligations for the exact binary. Do not label a GPL or nonfree build “LGPL-compatible.” The macOS packager recursively copies non-system dylibs needed by FFmpeg/FFprobe and the app; inspect those copied external libraries and their notices/licenses as part of the same review.
- FFmpeg binaries are not committed to this repository.
- The macOS release packaging copies `ffmpeg` and `ffprobe`, recursively bundles required non-system dylibs, and records the FFmpeg version in `VERSION.json`; this does not replace the distributor's license/source checks.

## Silero VAD

- Upstream: [snakers4/silero-vad](https://github.com/snakers4/silero-vad)
- Upstream notice: [Silero MIT LICENSE](https://raw.githubusercontent.com/snakers4/silero-vad/master/LICENSE) (Copyright (c) 2020-present Silero Team)
- Model input: `assets/models/silero_vad.onnx`, downloaded by `scripts/download-silero-model.sh`; the model is not committed by default.
- The checked-in downloader and macOS packager pin `v6.2.1` at the official upstream model URL and verify SHA-256 `1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3`.
- If a release includes the model, `VERSION.json` records its resolved version, source URL, and SHA-256, and `COMMERCIAL_LICENSING.md` is included in the ZIP. Re-check upstream terms and record a new hash for substituted versions or model sources.
