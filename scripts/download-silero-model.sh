#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
model_dir="$repo_root/assets/models"
model_path="$model_dir/silero_vad.onnx"
mkdir -p "$model_dir"

default_version="v6.2.1"
default_url="https://github.com/snakers4/silero-vad/raw/${default_version}/src/silero_vad/data/silero_vad.onnx"
default_sha256="1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3"
silero_model_version="${SILERO_MODEL_VERSION:-$default_version}"
silero_model_url="${SILERO_MODEL_URL:-https://github.com/snakers4/silero-vad/raw/${silero_model_version}/src/silero_vad/data/silero_vad.onnx}"

override_requested=0
[[ -n "${SILERO_MODEL_VERSION+x}" || -n "${SILERO_MODEL_URL+x}" ]] && override_requested=1
if [[ "$override_requested" == 1 && -z "${SILERO_MODEL_SHA256:-}" ]]; then
  echo "download-silero-model: SILERO_MODEL_SHA256 is required when SILERO_MODEL_VERSION or SILERO_MODEL_URL is overridden" >&2
  exit 1
fi
expected_sha256="${SILERO_MODEL_SHA256:-$default_sha256}"
[[ "$expected_sha256" =~ ^[0-9a-fA-F]{64}$ ]] || {
  echo "download-silero-model: expected SHA-256 must be 64 hexadecimal characters" >&2
  exit 1
}

command -v curl >/dev/null 2>&1 || { echo "download-silero-model: curl is required" >&2; exit 1; }
if command -v shasum >/dev/null 2>&1; then
  sha256_file() { shasum -a 256 "$1" | awk '{print $1}'; }
elif command -v sha256sum >/dev/null 2>&1; then
  sha256_file() { sha256sum "$1" | awk '{print $1}'; }
else
  echo "download-silero-model: shasum or sha256sum is required" >&2
  exit 1
fi

temporary_model="$(mktemp "$model_dir/.silero_vad.onnx.XXXXXX")"
cleanup() { rm -f "$temporary_model"; }
trap cleanup EXIT

echo "Downloading Silero VAD model ${silero_model_version} from ${silero_model_url}"
curl --fail --location --retry 3 --retry-delay 2 "$silero_model_url" -o "$temporary_model"
[[ -s "$temporary_model" ]] || {
  echo "download-silero-model: downloaded model is missing or empty" >&2
  exit 1
}

actual_sha256="$(sha256_file "$temporary_model")"
expected_sha256_normalized="$(printf '%s' "$expected_sha256" | tr '[:upper:]' '[:lower:]')"
if [[ "$actual_sha256" != "$expected_sha256_normalized" ]]; then
  echo "download-silero-model: SHA-256 mismatch (expected ${expected_sha256}, got ${actual_sha256})" >&2
  exit 1
fi

mv -f "$temporary_model" "$model_path"
trap - EXIT
echo "Downloaded and verified Silero VAD model ${silero_model_version} (${actual_sha256}) to assets/models/silero_vad.onnx"
