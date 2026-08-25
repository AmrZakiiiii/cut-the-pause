# Commercial licensing and redistribution

This document is general information about the files and build inputs in this
repository, not legal advice. A distributor is responsible for reviewing the
license texts and the exact binaries, model files, and configuration that it
ships. Re-check the upstream terms before each release, especially when
replacing a dependency or FFmpeg build.

## Project license and beta funding posture

The Cut The Pause source in this repository is licensed under the MIT License
in [`LICENSE`](../LICENSE). The MIT permission is broad: subject to retaining
the copyright and permission notices, recipients may use, copy, modify,
publish, distribute, sublicense, and sell copies of the project. The MIT
disclaimer also applies; the project is provided without warranty. See the
[Open Source Initiative MIT License text](https://opensource.org/license/mit/)
for general reference, and use the repository license as the governing text
for this project.

The current beta is open-source and supporter-funded. Voluntary payments,
sponsorships, or other support help fund maintenance; they do not turn the
MIT code into proprietary software, create an exclusive license, remove the
MIT redistribution rights, or grant a separate warranty or support contract.
Any separately offered support, hosting, or services must be described in its
own terms. Nothing in this document promises a live checkout, paid feature
gate, or proprietary edition.

## What a distributor must carry forward

When redistributing the repository or a compiled application, keep the
project copyright and MIT permission notice in the copies or substantial
portions of the project. Include this document and
[`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md) with a release artifact
and preserve the notices supplied by the dependency packages and runtime.
The macOS packaging script currently copies `LICENSE`,
`THIRD_PARTY_NOTICES.md`, `COMMERCIAL_LICENSING.md`, and
`packaging/macos/INSTALL_MACOS.md` into the distribution directory; it does
not make an arbitrary FFmpeg or model build licensed by the project.

Before shipping, inventory the exact publish output and any native files in
the artifact. The application directly references Avalonia 11.3.12 packages
and `Microsoft.ML.OnnxRuntime` 1.24.1; inspect the restored NuGet package
metadata and notices for the exact versions and all transitive payloads. A
self-contained .NET publish also contains runtime files that should be
reviewed with the generated publish output.

## FFmpeg and FFprobe

FFmpeg is not committed to this repository. The app can use binaries found on
`PATH` or the configured environment variables, and the macOS release workflow
installs FFmpeg separately and bundles `ffmpeg` and `ffprobe` into the ZIP.
The macOS packager also recursively copies each non-system Mach-O dylib needed
by the app, FFmpeg, FFprobe, and copied dylibs. Those external libraries are
part of the shipped binary surface: inspect their licenses, notices, source,
and build/configuration obligations too.
The package records the FFmpeg version string in `VERSION.json`, but that is
not a substitute for the binary's license and build configuration.

Read [FFmpeg's official legal and license guidance](https://ffmpeg.org/legal.html)
for the current terms. FFmpeg is generally LGPL v2.1-or-later, but optional
GPL components can make the FFmpeg build GPL, and `--enable-nonfree` builds
have additional restrictions and are not redistributable under the ordinary
free-license posture. A distributor must determine the exact result from the
binary's `ffmpeg -version` configuration line and the source tree used to
build it. Do not assume that a Homebrew package or another vendor's binary is
an “LGPL-compatible build” without checking.

For an LGPL-compatible distribution, follow FFmpeg's checklist for the exact
binary: retain the applicable copyright/license notices, provide source that
corresponds to the binary (including modifications), document the configure
line/build choices, and provide the required way for recipients to obtain
corresponding source. If the binary is modified or linked in a way that
triggers additional LGPL obligations, assess the applicable relinking and
replacement requirements. If the build is GPL or nonfree, do not distribute
it as though it were the default LGPL build; obtain specialist advice about
the resulting obligations before shipping.

Useful checks for every release:

1. Run `ffmpeg -version` and save the version and configuration line.
2. Confirm whether `--enable-gpl`, `--enable-nonfree`, or other options change
   the license posture; review the exact FFmpeg source and any external
   libraries included by that build.
3. Package the applicable FFmpeg license/notices and a corresponding-source
   offer or archive, with a source URL that remains available to recipients.
4. Re-run this review for both `ffmpeg` and `ffprobe`, and for any replacement
   binaries supplied by a distributor.

The [FFmpeg source repository](https://git.ffmpeg.org/ffmpeg.git) and
[FFmpeg download page](https://ffmpeg.org/download.html) are useful upstream
starting points; they do not by themselves identify the source corresponding
to a particular vendor binary.

## Silero VAD code and model

The app's primary detector is implemented in
`src/CutThePause.Infrastructure/Vad/SileroVadAnalyzer.cs` and runs an ONNX
model named `silero_vad.onnx`. The model is not tracked in this repository by
default. `scripts/download-silero-model.sh` downloads it from the
`snakers4/silero-vad` repository, and the macOS packaging script requires a
non-empty `assets/models/silero_vad.onnx` and copies it into the app bundle.
If the model is absent, the app uses its built-in energy detector instead.

The [Silero VAD upstream repository](https://github.com/snakers4/silero-vad)
publishes an MIT license naming the Silero Team; retain that attribution and
the MIT notice when redistributing the code or model artifact. The upstream
[license file](https://raw.githubusercontent.com/snakers4/silero-vad/master/LICENSE)
is the authoritative notice to check. The checked-in downloader and macOS
packager now pin the official `v6.2.1` model at
`https://github.com/snakers4/silero-vad/raw/v6.2.1/src/silero_vad/data/silero_vad.onnx`
and verify SHA-256
`1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3`.
The packager records the resolved model version, source URL, and hash in
`VERSION.json`, and the ZIP includes this document. Overrides require an
explicit expected SHA-256; re-check upstream terms for every substituted
version or source.
Re-check the upstream release/tag and model path before running the helper;
substituted or newly trained weights may have different terms, attribution,
or data-use conditions. Do not bundle a model from another source under the
Silero notice without verifying that source's rights.

## Direct package notices

The direct package references currently visible in the project files are:

| Input | Repository evidence | Distributor action |
| --- | --- | --- |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter 11.3.12 | [Avalonia license](https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md) | Preserve the package notices and inspect the exact restored package/transitives. |
| Microsoft.ML.OnnxRuntime 1.24.1 | [ONNX Runtime license](https://github.com/microsoft/onnxruntime/blob/main/LICENSE) and [third-party notices](https://github.com/microsoft/onnxruntime/blob/main/ThirdPartyNotices.txt) | Ship the exact package notices, including notices for native/runtime payloads where applicable. |
| FFmpeg/FFprobe and recursively copied non-system dylibs | [FFmpeg legal guidance](https://ffmpeg.org/legal.html) | Identify each exact binary/library build, license mode, notices, corresponding-source/relinking path, and any external-library obligations. |
| `silero_vad.onnx` supplied by the downloader/packager | [Silero repository](https://github.com/snakers4/silero-vad) and [MIT notice](https://raw.githubusercontent.com/snakers4/silero-vad/master/LICENSE) | Verify the pinned `v6.2.1` URL/hash or the explicit override's URL/hash, and re-check upstream terms for every substituted artifact. |

This table is not a substitute for a complete artifact scan. Dependency
versions, native providers, operating-system runtime files, and replacement
tools can add notices or obligations.

## Release checklist

- [ ] `LICENSE` is included unchanged, and the MIT/project notices remain
      with the source or binary distribution.
- [ ] `THIRD_PARTY_NOTICES.md`, direct package notices, and applicable runtime
      notices are included in the artifact.
- [ ] FFmpeg and FFprobe exact versions, configuration flags, source commit,
      license mode, notices, and corresponding-source/relinking plan are
      recorded.
- [ ] Silero model URL/tag or commit, SHA-256, upstream license/attribution,
      and any substituted-model terms are recorded.
- [ ] No voluntary support payment is represented as a proprietary license,
      exclusive right, or required purchase for MIT rights.
- [ ] A qualified reviewer has checked any jurisdiction-specific commercial,
      patent, codec, trademark, export, or model/data-use questions.
