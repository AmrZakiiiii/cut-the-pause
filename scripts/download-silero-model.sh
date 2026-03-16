#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$repo_root/assets/models"

curl -L "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx" \
  -o "$repo_root/assets/models/silero_vad.onnx"

echo "Downloaded Silero VAD model to assets/models/silero_vad.onnx"
