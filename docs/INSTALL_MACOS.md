# Cut The Pause macOS beta: install and source build

This guide covers the unsigned Apple Silicon beta and the commands used to
build the same artifact locally.

## Compatibility and package status

- The beta target is Apple Silicon (`osx-arm64`). Intel Macs are not a
  supported target for this package.
- The minimum supported macOS version is macOS 13.0. This is the value in
  `packaging/macos/Info.plist` (`LSMinimumSystemVersion`).
- The beta is unsigned and not notarized. It has no Apple Developer ID
  signature or notarization ticket. Gatekeeper may therefore say that Apple
  cannot check the app for malicious software, that the developer cannot be
  verified, or that the app was blocked. Those warnings are expected for this
  beta; they do not mean that the download checksum was checked.
- The ZIP includes FFmpeg, FFprobe, and the Silero VAD model. A separate
  FFmpeg installation is not required for the downloaded beta.

## Install the downloaded ZIP

The release files have these exact names:

```text
CutThePause-macos-arm64.zip
CutThePause-macos-arm64.zip.sha256
```

Download both files from the same release into `~/Downloads`. Do not open the
app until checksum verification succeeds.

### 1. Verify the checksum

```bash
cd ~/Downloads
shasum -a 256 -c CutThePause-macos-arm64.zip.sha256
```

The result must report `OK`. On a system without `shasum`, use:

```bash
cd ~/Downloads
sha256sum -c CutThePause-macos-arm64.zip.sha256
```

If verification fails, delete the ZIP and checksum file, download both again,
and verify that the checksum file belongs to the same release. Do not install
an artifact with a failed or missing checksum.

### 2. Extract and install

Extract the ZIP in Finder by double-clicking it, or with Terminal:

```bash
mkdir -p ~/Downloads/CutThePause-extracted
unzip -q ~/Downloads/CutThePause-macos-arm64.zip -d ~/Downloads/CutThePause-extracted
```

The extracted layout starts at:

```text
~/Downloads/CutThePause-extracted/CutThePause-macos-arm64/
```

It contains `Cut The Pause.app` and the release support files. Move the app to
your user Applications folder (or choose `/Applications` if you have the
required administrator permission):

```bash
mkdir -p ~/Applications
ditto -rsrc \
  ~/Downloads/CutThePause-extracted/CutThePause-macos-arm64/Cut\ The\ Pause.app \
  ~/Applications/Cut\ The\ Pause.app
```

Finder drag-and-drop is equivalent. Keep the bundle together; do not move
files out of `Contents/MacOS`.

### 3. First launch (required Finder action)

In Finder, open `~/Applications`, then **right-click (or Control-click) `Cut
The Pause.app` → Open**. In the warning dialog, choose **Open** again. The
first-launch Right-click → Open action records your intentional exception for
this app; ordinary double-click may continue to show the Gatekeeper warning.

If the app is in `/Applications`, use that copy for the same Right-click →
Open action. Do not launch a copy directly from inside the ZIP.

## What is inside the beta artifact

The package smoke test and release workflow expect this layout:

```text
CutThePause-macos-arm64/
├── Cut The Pause.app/
│   └── Contents/
│       └── MacOS/
│           ├── CutThePause.App
│           ├── ffmpeg
│           ├── ffprobe
│           └── Models/silero_vad.onnx
├── INSTALL_MACOS.md
├── LICENSE
├── THIRD_PARTY_NOTICES.md
└── VERSION.json
```

`VERSION.json` identifies the package as `osx-arm64`, `unsigned-beta`, with
`codeSigning` and `notarization` set to `none`.

## Troubleshooting

### Download, checksum, or extraction failure

Confirm that the ZIP and `.sha256` files came from the same release and that
the checksum command was run from the directory containing both files. A
failed checksum is a download/release-integrity problem, not a Gatekeeper
problem. Re-download before trying any quarantine command. If `unzip` reports
an error, download the ZIP again and confirm that it is not a partial file.

### Gatekeeper says the developer cannot be verified or Apple cannot check the app

This is expected because the beta is unsigned and not notarized. Use the
Finder **right-click → Open** flow above after verifying the checksum. Do not
disable Gatekeeper globally and do not run commands such as
`spctl --master-disable`.

If macOS reports that the app is damaged after a successful checksum check,
inspect the quarantine attribute on this app only:

```bash
xattr -l "$HOME/Applications/Cut The Pause.app"
```

For a verified beta copy that is still blocked, remove only that app's
quarantine attribute and retry Finder **right-click → Open**:

```bash
xattr -dr com.apple.quarantine "$HOME/Applications/Cut The Pause.app"
```

This does not disable Gatekeeper for other applications. If the app is in
`/Applications`, use that exact path instead. If the warning persists, restore
the original download and repeat checksum verification rather than weakening
system security further.

### Permission or file-access errors

Choose an input video and output directory that your user can read and write.
macOS may ask for access when the video is under Desktop, Documents, iCloud
Drive, an external disk, or another protected location. Grant access to the
app in **System Settings → Privacy & Security → Files and Folders**, then
retry. A user-owned folder under your home directory is a useful diagnostic.

The app stores settings, history, custom presets, and export checkpoints under
the macOS application-data location, normally:

```text
~/Library/Application Support/Cut The Pause/
├── custom-presets.json
├── detection-settings.json
├── export-checkpoints.json
└── history.json
```

If these files cannot be written, check that the directory is writable and
that no administrator-owned copy is being used for the data directory. The
app can still start without existing history or settings.

### App does not launch or quits immediately

First confirm that you moved the app out of the ZIP, verified the checksum,
and completed Finder **right-click → Open**. On Apple Silicon, this command
should report `arm64` for the app executable:

```bash
file "$HOME/Applications/Cut The Pause.app/Contents/MacOS/CutThePause.App"
```

To capture startup diagnostics without changing security settings, run the
bundle executable from Terminal:

```bash
"$HOME/Applications/Cut The Pause.app/Contents/MacOS/CutThePause.App" \
  2>&1 | tee "$HOME/Desktop/cut-the-pause-launch.log"
```

The UI status bar and warnings panel contain operation errors. Avalonia is
configured to send application traces to the platform trace system, so
**Console.app** can also be used while reproducing a launch or analysis issue;
filter for `CutThePause.App`. There is no separate application-managed log
file.

### FFmpeg or FFprobe errors

The downloaded bundle should contain executable copies at:

```text
Cut The Pause.app/Contents/MacOS/ffmpeg
Cut The Pause.app/Contents/MacOS/ffprobe
```

The app checks the bundled tools before falling back to `PATH`. If analysis or
export reports that FFmpeg was not found, verify that the app was moved as a
complete bundle and that both files are executable. For a source build, set
these variables to explicit executable paths when needed:

```bash
export CUTTHEPAUSE_FFMPEG_PATH="$(command -v ffmpeg)"
export CUTTHEPAUSE_FFPROBE_PATH="$(command -v ffprobe)"
```

`CUTTHEPAUSE_FFMPEG_PATH` and `CUTTHEPAUSE_FFPROBE_PATH` must point to files,
not directories. The app displays FFmpeg/FFprobe process errors in its status
and warnings UI.

### Silero model warning

The downloaded bundle stores the model at:

```text
Cut The Pause.app/Contents/MacOS/Models/silero_vad.onnx
```

If it is missing, unreadable, or its native ONNX runtime cannot load, the app
falls back to its energy-based detector and shows a warning. Reinstall from a
checksum-verified ZIP to restore the bundled model. Source-build users can
download it with `./scripts/download-silero-model.sh` as described below.

## Source build on macOS

These commands reproduce the repository's CI and release workflow. Run them
from the repository root on an Apple Silicon Mac.

### Prerequisites

- macOS 13.0 or newer on Apple Silicon.
- .NET SDK 8.0.300 or a later 8.0 feature-band accepted by `global.json`.
- Xcode Command Line Tools, including `otool`, `install_name_tool`, `file`,
  `ditto`, and `sips` for native packaging.
- Homebrew for packaging dependencies. The release workflow installs FFmpeg
  and `jq` with `brew install ffmpeg jq`.
- Network access for the first NuGet restore and for the Silero model download.

Check the toolchain before starting:

```bash
dotnet --version
uname -m
xcode-select -p
brew --version
```

The application can be built without the model and uses its energy-detector
fallback, but a self-contained macOS package requires the model file.

### Restore and build

```bash
dotnet restore CutThePause.sln
dotnet build CutThePause.sln --configuration Release --no-restore
```

### Test

The solution and CI run the three test projects explicitly:

```bash
bash scripts/validate-test-matrix.sh
dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj --configuration Release --no-build
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --configuration Release --no-build
dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --configuration Release --no-build
```

The real-video tests are skipped when their sample media is absent. The
multi-hour acceptance test is opt-in and requires both
`CUTTHEPAUSE_RUN_LONG_VIDEO=1` and `CUTTHEPAUSE_MACHINE_IDLE=1`; do not set
those variables unless the required external video and idle machine are
available.

### Run from source

Install FFmpeg first, or set the two override variables shown above. Then:

```bash
dotnet run --project src/CutThePause.App/CutThePause.App.csproj --configuration Release
```

To enable the primary Silero detector from a source checkout:

```bash
./scripts/download-silero-model.sh
```

The model is written to `assets/models/silero_vad.onnx`. Without it, source
runs use the energy-based fallback and show a warning.

### Publish the Apple Silicon app

```bash
dotnet publish src/CutThePause.App/CutThePause.App.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output artifacts/publish/osx-arm64
```

### Package the unsigned ZIP

Install the release dependencies and download the model before packaging:

```bash
brew install ffmpeg jq
./scripts/download-silero-model.sh
./scripts/package-macos-app.sh \
  artifacts/publish/osx-arm64 \
  "$(brew --prefix ffmpeg)" \
  artifacts/release
```

`scripts/package-macos-app.sh` must run on Darwin. It creates:

```text
artifacts/release/CutThePause-macos-arm64.zip
artifacts/release/CutThePause-macos-arm64.zip.sha256
```

The script also creates the intermediate
`artifacts/release/CutThePause-macos-arm64/` directory and app bundle. It
removes/replaces only those named output paths on each packaging run.

### Validate the artifact

Run the repository smoke test against the ZIP:

```bash
./scripts/test-macos-package.sh artifacts/release/CutThePause-macos-arm64.zip
```

It verifies the checksum, archive layout, support files, unsigned metadata,
absence of `_CodeSignature`, Apple Silicon executable architecture, and that
non-system dynamic-library dependencies resolve inside the app bundle.

The release workflow in `.github/workflows/release.yml` runs the same restore,
build, test, publish, package, and smoke-test commands before uploading the ZIP
and checksum. No signing or notarization step is present.
