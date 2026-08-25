#!/usr/bin/env bash
set -euo pipefail

zip_path="${1:?usage: scripts/test-macos-package.sh path/to/CutThePause-macos-arm64.zip}"
checksum_path="${zip_path}.sha256"

fail() {
  echo "test-macos-package: $*" >&2
  exit 1
}

[[ -f "$zip_path" ]] || fail "ZIP does not exist: $zip_path"
[[ -f "$checksum_path" ]] || fail "SHA-256 checksum does not exist: $checksum_path"

zip_dir="$(cd "$(dirname "$zip_path")" && pwd)"
zip_name="$(basename "$zip_path")"
if command -v shasum >/dev/null 2>&1; then
  (cd "$zip_dir" && shasum -a 256 -c "$(basename "$checksum_path")" >/dev/null) || fail "SHA-256 checksum does not match"
elif command -v sha256sum >/dev/null 2>&1; then
  (cd "$zip_dir" && sha256sum -c "$(basename "$checksum_path")" >/dev/null) || fail "SHA-256 checksum does not match"
else
  fail "neither shasum nor sha256sum is available"
fi

extract_dir="$(mktemp -d "${TMPDIR:-/tmp}/cutthepause-package.XXXXXX")"
trap 'rm -rf "$extract_dir"' EXIT
command -v unzip >/dev/null 2>&1 || fail "unzip is required"
command -v jq >/dev/null 2>&1 || fail "jq is required to parse VERSION.json"
unzip -q "$zip_path" -d "$extract_dir" || fail "unable to extract ZIP"
root="$extract_dir/CutThePause-macos-arm64"
app="$root/Cut The Pause.app"
macos="$app/Contents/MacOS"

[[ -d "$app/Contents" ]] || fail "missing app bundle: $app/Contents"
[[ -f "$app/Contents/Info.plist" ]] || fail "missing Info.plist"
[[ -x "$macos/CutThePause.App" ]] || fail "app executable is missing or not executable"
[[ -x "$macos/ffmpeg" ]] || fail "bundled ffmpeg is missing or not executable"
[[ -x "$macos/ffprobe" ]] || fail "bundled ffprobe is missing or not executable"
[[ -s "$macos/Models/silero_vad.onnx" ]] || fail "bundled Silero model is missing or empty"
[[ -f "$root/LICENSE" ]] || fail "missing LICENSE"
[[ -f "$root/THIRD_PARTY_NOTICES.md" ]] || fail "missing THIRD_PARTY_NOTICES.md"
[[ -f "$root/COMMERCIAL_LICENSING.md" ]] || fail "missing COMMERCIAL_LICENSING.md"
[[ -f "$root/INSTALL_MACOS.md" ]] || fail "missing INSTALL_MACOS.md"
[[ -f "$root/VERSION.json" ]] || fail "missing VERSION.json"
jq -e '
  .product == "Cut The Pause" and
  (.version | type == "string" and test("^[0-9]+\\.[0-9]+\\.[0-9]+")) and
  .target == "osx-arm64" and
  .package == "unsigned-beta" and
  .codeSigning == "none" and
  .notarization == "none" and
  .sileroModel.version == "v6.2.1" and
  .sileroModel.source == "https://github.com/snakers4/silero-vad/raw/v6.2.1/src/silero_vad/data/silero_vad.onnx" and
  .sileroModel.sha256 == "1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3"
' "$root/VERSION.json" >/dev/null || fail "VERSION.json is invalid or has incorrect metadata"
[[ ! -e "$app/Contents/_CodeSignature" ]] || fail "artifact unexpectedly contains a code signature"

[[ "$(uname -s)" == "Darwin" ]] || fail "native macOS artifact checks require Darwin"
command -v otool >/dev/null 2>&1 || fail "otool is required to inspect dylib dependencies"
command -v file >/dev/null 2>&1 || fail "file is required to inspect architectures"

require_arm64() {
  local binary="$1"
  local file_output
  file_output="$(file -b "$binary")"
  echo "$file_output" | grep -Eiq 'arm64|apple silicon' || fail "not arm64: $binary ($file_output)"
}

require_arm64 "$macos/CutThePause.App"
require_arm64 "$macos/ffmpeg"
require_arm64 "$macos/ffprobe"

resolve_bundle_path() {
  local consumer="$1"
  local dependency="$2"
  local candidate
  case "$dependency" in
    @loader_path/*) candidate="$(dirname "$consumer")/${dependency#@loader_path/}" ;;
    @executable_path/*) candidate="$macos/${dependency#@executable_path/}" ;;
    @rpath/*)
      candidate="$macos/${dependency#@rpath/}"
      [[ -e "$candidate" ]] || candidate="$app/Contents/Frameworks/${dependency#@rpath/}"
      ;;
    /*)
      [[ "$dependency" == "$app/"* ]] || return 1
      candidate="$dependency"
      ;;
    *) return 1 ;;
  esac
  [[ -e "$candidate" ]] || return 1
  printf '%s\n' "$candidate"
}

is_system_dependency() {
  case "$1" in
    /System/*|/usr/lib/*|/usr/lib/system/*|/Library/Apple/System/*) return 0 ;;
    *) return 1 ;;
  esac
}

check_dependencies() {
  local binary="$1"
  local dependency line resolved own_id
  own_id="$(otool -D "$binary" 2>/dev/null | sed -n 's|^/|/|p' | tail -n 1)"
  while IFS= read -r line; do
    [[ "$line" == *"(architecture "* ]] && continue
    dependency="${line#${line%%[![:space:]]*}}"
    dependency="$(printf '%s\n' "$dependency" | sed 's/[[:space:]]*(compatibility.*$//')"
    [[ "$dependency" == /* || "$dependency" == @* ]] || continue
    [[ -n "$own_id" && "$dependency" == "$own_id" ]] && continue
    is_system_dependency "$dependency" && continue
    resolved="$(resolve_bundle_path "$binary" "$dependency")" || fail "unresolved/non-system dependency '$dependency' from $binary"
    [[ "$resolved" == "$app/"* ]] || fail "dependency escaped app bundle: $dependency from $binary"
  done < <(otool -L "$binary" | tail -n +2)
}

while IFS= read -r -d '' binary; do
  [[ "$(basename "$binary")" == ._* ]] && continue
  require_arm64 "$binary"
  check_dependencies "$binary"
done < <(find "$macos" -type f \( -name '*.dylib' -o -perm -111 \) -print0)

echo "macOS package smoke test passed: $zip_name"
