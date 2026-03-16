#!/usr/bin/env bash
set -euo pipefail

publish_dir="${1:?publish directory is required}"
ffmpeg_prefix="${2:?ffmpeg prefix is required}"
output_dir="${3:?output directory is required}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
app_dir="$output_dir/Cut The Pause.app"

rm -rf "$app_dir"
mkdir -p "$app_dir/Contents/MacOS"
mkdir -p "$app_dir/Contents/Resources"
mkdir -p "$app_dir/Contents/MacOS/Models"

cp "$repo_root/packaging/macos/Info.plist" "$app_dir/Contents/Info.plist"
cp -R "$publish_dir"/. "$app_dir/Contents/MacOS/"

if [[ -x "$ffmpeg_prefix/bin/ffmpeg" ]]; then
  cp "$ffmpeg_prefix/bin/ffmpeg" "$app_dir/Contents/MacOS/ffmpeg"
fi

if [[ -x "$ffmpeg_prefix/bin/ffprobe" ]]; then
  cp "$ffmpeg_prefix/bin/ffprobe" "$app_dir/Contents/MacOS/ffprobe"
fi

if [[ -f "$repo_root/assets/models/silero_vad.onnx" ]]; then
  cp "$repo_root/assets/models/silero_vad.onnx" "$app_dir/Contents/MacOS/Models/silero_vad.onnx"
fi

chmod +x "$app_dir/Contents/MacOS/CutThePause.App" || true

echo "Created app bundle at $app_dir"
