# Cut The Pause macOS beta install guide

This is the install handout included inside the release ZIP. The beta is an
unsigned Apple Silicon (`osx-arm64`) build for macOS 13.0 or newer. It is not
notarized. Intel Macs are not a supported target. The ZIP includes the app,
FFmpeg, FFprobe, and the Silero VAD model; no separate FFmpeg installation is
required.

## Install

The release provides these two files:

```text
CutThePause-macos-arm64.zip
CutThePause-macos-arm64.zip.sha256
```

Download both into `~/Downloads`, then verify the ZIP before opening it:

```bash
cd ~/Downloads
shasum -a 256 -c CutThePause-macos-arm64.zip.sha256
```

The checksum command must report `OK`. If `shasum` is unavailable, use
`sha256sum -c CutThePause-macos-arm64.zip.sha256`. If verification fails,
download both release files again; do not install the failed download.

Extract the verified ZIP by double-clicking it in Finder, or run:

```bash
mkdir -p ~/Downloads/CutThePause-extracted
unzip -q ~/Downloads/CutThePause-macos-arm64.zip -d ~/Downloads/CutThePause-extracted
```

The app is at:

```text
~/Downloads/CutThePause-extracted/CutThePause-macos-arm64/Cut The Pause.app
```

Move `Cut The Pause.app` to `~/Applications` or `/Applications`. In Finder,
**right-click (or Control-click) `Cut The Pause.app` → Open**, then choose
**Open** again in the warning dialog. This first-launch Right-click → Open
action is required for this unsigned, non-notarized beta. Do not launch a copy
from inside the ZIP.

## Expected warnings and recovery

Gatekeeper may report that Apple cannot check the app for malicious software,
that the developer cannot be verified, or that the app was blocked. These
warnings are expected because this beta has no Apple Developer ID signature or
notarization ticket. Verify the checksum and use Finder **right-click → Open**;
never disable Gatekeeper globally.

If a checksum-verified copy is still reported as damaged, inspect and, only if
needed, remove quarantine from that app copy:

```bash
xattr -l "$HOME/Applications/Cut The Pause.app"
xattr -dr com.apple.quarantine "$HOME/Applications/Cut The Pause.app"
```

Use the `/Applications` path if that is where you installed it. This command
only changes the selected app; it does not disable Gatekeeper for the system.
If the warning persists, download and verify a fresh copy instead of changing
global security settings.

The app bundles these runtime files under
`Cut The Pause.app/Contents/MacOS/`:

```text
ffmpeg
ffprobe
Models/silero_vad.onnx
```

If the model cannot load, the app falls back to its energy-based detector and
shows a warning. The status bar and warnings panel show operation failures.
For launch diagnostics, run the executable from Terminal and capture output:

```bash
"$HOME/Applications/Cut The Pause.app/Contents/MacOS/CutThePause.App" \
  2>&1 | tee "$HOME/Desktop/cut-the-pause-launch.log"
```

Avalonia sends application traces to the platform trace system; Console.app
can be used while reproducing a problem (filter for `CutThePause.App`). There
is no separate application-managed log file. Choose user-readable input and
output folders; for protected Desktop/Documents locations, grant access under
**System Settings → Privacy & Security → Files and Folders**.
