#!/usr/bin/env bash
set -euo pipefail

publish_dir="${1:?publish directory is required (the osx-arm64 dotnet publish output)}"
ffmpeg_prefix="${2:?FFmpeg prefix is required (for example: brew --prefix ffmpeg)}"
output_dir="${3:?output directory is required}"
version_override="${4:-}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version_file="$repo_root/packaging/macos/version.txt"
install_guide="$repo_root/packaging/macos/INSTALL_MACOS.md"
commercial_license="$repo_root/docs/COMMERCIAL_LICENSING.md"
app_dir="$output_dir/Cut The Pause.app"
distribution_name="CutThePause-macos-arm64"
distribution_dir="$output_dir/$distribution_name"
zip_path="$output_dir/$distribution_name.zip"
checksum_path="$zip_path.sha256"

fail() {
  echo "package-macos-app: $*" >&2
  exit 1
}

[[ "$(uname -s)" == "Darwin" ]] || fail "unsigned macOS packaging must run on Darwin"
command -v otool >/dev/null 2>&1 || fail "otool is required to inspect and bundle dylib dependencies"
command -v install_name_tool >/dev/null 2>&1 || fail "install_name_tool is required to rewrite dylib dependencies"
command -v file >/dev/null 2>&1 || fail "file is required to verify Mach-O inputs"
command -v jq >/dev/null 2>&1 || fail "jq is required to write escaped VERSION.json metadata"
[[ -d "$publish_dir" ]] || fail "publish directory does not exist: $publish_dir"
[[ -f "$version_file" ]] || fail "version source is missing: $version_file"
[[ -f "$install_guide" ]] || fail "install guide is missing: $install_guide"
[[ -f "$commercial_license" ]] || fail "commercial licensing guide is missing: $commercial_license"
[[ -f "$repo_root/LICENSE" ]] || fail "license is missing: $repo_root/LICENSE"
[[ -f "$repo_root/THIRD_PARTY_NOTICES.md" ]] || fail "third-party notices are missing: $repo_root/THIRD_PARTY_NOTICES.md"
[[ -d "$ffmpeg_prefix/bin" ]] || fail "FFmpeg prefix has no bin directory: $ffmpeg_prefix"

version="${version_override:-$(tr -d '[:space:]' < "$version_file")}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]] || fail "invalid version '$version' (source: $version_file)"

silero_model_version="${SILERO_MODEL_VERSION:-v6.2.1}"
silero_model_url="${SILERO_MODEL_URL:-https://github.com/snakers4/silero-vad/raw/${silero_model_version}/src/silero_vad/data/silero_vad.onnx}"
default_silero_sha256="1a153a22f4509e292a94e67d6f9b85e8deb25b4988682b7e174c65279d8788e3"
silero_override_requested=0
[[ -n "${SILERO_MODEL_VERSION+x}" || -n "${SILERO_MODEL_URL+x}" ]] && silero_override_requested=1
if [[ "$silero_override_requested" == 1 && -z "${SILERO_MODEL_SHA256:-}" ]]; then
  fail "SILERO_MODEL_SHA256 is required when SILERO_MODEL_VERSION or SILERO_MODEL_URL is overridden"
fi
expected_silero_sha256="${SILERO_MODEL_SHA256:-$default_silero_sha256}"
[[ "$expected_silero_sha256" =~ ^[0-9a-fA-F]{64}$ ]] || fail "invalid expected Silero SHA-256"

ffmpeg="$ffmpeg_prefix/bin/ffmpeg"
ffprobe="$ffmpeg_prefix/bin/ffprobe"
model="$repo_root/assets/models/silero_vad.onnx"
publish_executable="$publish_dir/CutThePause.App"
[[ -f "$publish_executable" ]] || fail "published app executable is missing: $publish_executable"
[[ -x "$publish_executable" ]] || fail "published app executable is not executable: $publish_executable"
[[ -x "$ffmpeg" ]] || fail "FFmpeg executable is missing or not executable: $ffmpeg"
[[ -x "$ffprobe" ]] || fail "FFprobe executable is missing or not executable: $ffprobe"
[[ -s "$model" ]] || fail "Silero model is missing or empty: $model"

if command -v shasum >/dev/null 2>&1; then
  actual_silero_sha256="$(shasum -a 256 "$model" | awk '{print $1}')"
else
  actual_silero_sha256="$(sha256sum "$model" | awk '{print $1}')"
fi
expected_silero_sha256_normalized="$(printf '%s' "$expected_silero_sha256" | tr '[:upper:]' '[:lower:]')"
[[ "$actual_silero_sha256" == "$expected_silero_sha256_normalized" ]] || fail "Silero model SHA-256 mismatch (expected $expected_silero_sha256, got $actual_silero_sha256)"

mkdir -p "$output_dir"
rm -rf "$app_dir" "$distribution_dir"
rm -f "$zip_path" "$checksum_path"
mkdir -p "$app_dir/Contents/MacOS/Models" "$app_dir/Contents/Resources"

cp "$repo_root/packaging/macos/Info.plist" "$app_dir/Contents/Info.plist"
cp -R "$publish_dir"/. "$app_dir/Contents/MacOS/"
cp "$ffmpeg" "$app_dir/Contents/MacOS/ffmpeg"
cp "$ffprobe" "$app_dir/Contents/MacOS/ffprobe"
cp "$model" "$app_dir/Contents/MacOS/Models/silero_vad.onnx"
chmod +x "$app_dir/Contents/MacOS/CutThePause.App" "$app_dir/Contents/MacOS/ffmpeg" "$app_dir/Contents/MacOS/ffprobe"

# Resolve every non-system Mach-O dependency from the app, FFmpeg, FFprobe,
# and each copied dylib. Homebrew paths are copied beside the consumers and
# rewritten to @loader_path, so the shipped app never loads Homebrew files.
bundle_dir="$app_dir/Contents/MacOS"
is_system_dependency() {
  case "$1" in
    /System/*|/usr/lib/*|/usr/lib/system/*|/Library/Apple/System/*) return 0 ;;
    *) return 1 ;;
  esac
}

trim_path() {
  local candidate="$1"
  local candidate_dir
  candidate_dir="$(dirname "$candidate")"
  [[ -d "$candidate_dir" ]] || return 1
  (cd "$candidate_dir" && printf '%s/%s\n' "$(pwd -P)" "$(basename "$candidate")")
}

resolve_dependency() {
  local consumer="$1"
  local dependency="$2"
  local candidate
  case "$dependency" in
    /*) candidate="$dependency" ;;
    @loader_path/*) candidate="$(dirname "$consumer")/${dependency#@loader_path/}" ;;
    @executable_path/*) candidate="$bundle_dir/${dependency#@executable_path/}" ;;
    @rpath/*)
      candidate="$bundle_dir/${dependency#@rpath/}"
      [[ -e "$candidate" ]] || candidate="$app_dir/Contents/Frameworks/${dependency#@rpath/}"
      ;;
    *) return 1 ;;
  esac
  trim_path "$candidate"
}

path_in_list() {
  local wanted="$1"
  shift
  local item
  for item in "$@"; do
    [[ "$item" == "$wanted" ]] && return 0
  done
  return 1
}

bundle_dependencies() {
  local queue=("$app_dir/Contents/MacOS/CutThePause.App" "$app_dir/Contents/MacOS/ffmpeg" "$app_dir/Contents/MacOS/ffprobe")
  local processed=()
  local consumer dependency source target base line own_id dylib index=0
  for dylib in "$bundle_dir"/*.dylib; do
    [[ -f "$dylib" ]] && queue+=("$dylib")
  done

  while (( index < ${#queue[@]} )); do
    consumer="${queue[$index]}"
    index=$((index + 1))
    path_in_list "$consumer" "${processed[@]-}" && continue
    processed+=("$consumer")
    file -b "$consumer" | grep -Eq 'Mach-O' || fail "not a Mach-O executable: $consumer"
    own_id="$(otool -D "$consumer" 2>/dev/null | sed -n 's|^/|/|p' | tail -n 1)"

    while IFS= read -r line; do
      [[ "$line" == *"(architecture "* ]] && continue
      dependency="${line#${line%%[![:space:]]*}}"
      dependency="$(printf '%s\n' "$dependency" | sed 's/[[:space:]]*(compatibility.*$//')"
      [[ "$dependency" == /* || "$dependency" == @* ]] || continue
      [[ -n "$own_id" && "$dependency" == "$own_id" ]] && continue
      is_system_dependency "$dependency" && continue

      source=""
      if source="$(resolve_dependency "$consumer" "$dependency")" && [[ -e "$source" ]]; then
        :
      else
        fail "unresolved non-system dependency '$dependency' from $consumer"
      fi

      base="$(basename "$source")"
      target="$bundle_dir/$base"
      if [[ "$source" != "$target" && ! -e "$target" ]]; then
        cp "$source" "$target"
        chmod u+rw,go+r "$target"
      fi
      if [[ "$dependency" != "@loader_path/$base" && "$consumer" != "$target" ]]; then
        install_name_tool -change "$dependency" "@loader_path/$base" "$consumer"
      fi
      if file -b "$target" | grep -Eq 'Mach-O.*(dynamically linked shared library|bundle)' || [[ "$target" == *.dylib ]]; then
        install_name_tool -id "@loader_path/$base" "$target" 2>/dev/null || true
        path_in_list "$target" "${queue[@]}" || queue+=("$target")
      fi
    done < <(otool -L "$consumer" | tail -n +2)
  done
}

# The macOS ONNX Runtime binary is shipped as libonnxruntime.dylib, but its
# install name can reference a versioned @rpath filename. Preserve that
# loader contract inside the self-contained app bundle before dependency walk.
onnx_runtime="$app_dir/Contents/MacOS/libonnxruntime.dylib"
if [[ -f "$onnx_runtime" ]]; then
  onnx_install_name="$(otool -D "$onnx_runtime" | tail -n 1)"
  onnx_versioned_name="$(basename "$onnx_install_name")"
  if [[ "$onnx_versioned_name" != "libonnxruntime.dylib" && "$onnx_versioned_name" == libonnxruntime.*.dylib ]]; then
    ln -sfn "libonnxruntime.dylib" "$app_dir/Contents/MacOS/$onnx_versioned_name"
  fi
  if [[ "$onnx_versioned_name" != "libonnxruntime.dylib" && ! -e "$app_dir/Contents/MacOS/$onnx_versioned_name" ]]; then
    fail "ONNX Runtime dependency is missing from the app bundle: $onnx_versioned_name"
  fi
fi

bundle_dependencies

# Finder/Developer Tools may emit AppleDouble resource-fork sidecars when
# copying Homebrew files. They are not runtime inputs and must not be shipped
# as apparent dylibs in the distributable artifact.
find "$app_dir" -name '._*' -type f -delete

icon_source="$repo_root/src/CutThePause.App/Assets/AppIcon.png"
if [[ -f "$icon_source" ]] && command -v sips >/dev/null 2>&1; then
  sips -s format icns "$icon_source" --out "$app_dir/Contents/Resources/CutThePause.icns" >/dev/null
fi

mkdir -p "$distribution_dir"
cp -R "$app_dir" "$distribution_dir/"
cp "$repo_root/LICENSE" "$distribution_dir/LICENSE"
cp "$repo_root/THIRD_PARTY_NOTICES.md" "$distribution_dir/THIRD_PARTY_NOTICES.md"
cp "$install_guide" "$distribution_dir/INSTALL_MACOS.md"
cp "$commercial_license" "$distribution_dir/COMMERCIAL_LICENSING.md"

ffmpeg_version="unknown"
if ffmpeg_version_output="$("$ffmpeg" -version 2>/dev/null | head -n 1)" && [[ -n "$ffmpeg_version_output" ]]; then
  ffmpeg_version="$ffmpeg_version_output"
fi
jq -n \
  --arg product "Cut The Pause" \
  --arg version "$version" \
  --arg target "osx-arm64" \
  --arg package "unsigned-beta" \
  --arg codeSigning "none" \
  --arg notarization "none" \
  --arg ffmpeg "$ffmpeg_version" \
  --arg sileroVersion "$silero_model_version" \
  --arg sileroSource "$silero_model_url" \
  --arg sileroSha256 "$actual_silero_sha256" \
  '{product:$product,version:$version,target:$target,package:$package,codeSigning:$codeSigning,notarization:$notarization,ffmpeg:$ffmpeg,sileroModel:{version:$sileroVersion,source:$sileroSource,sha256:$sileroSha256}}' \
  > "$distribution_dir/VERSION.json"

if command -v ditto >/dev/null 2>&1; then
  ditto -c -k --keepParent "$distribution_dir" "$zip_path"
elif command -v zip >/dev/null 2>&1; then
  (cd "$output_dir" && zip -qry "$zip_path" "$distribution_name")
else
  fail "neither ditto nor zip is available to create $zip_path"
fi

if command -v shasum >/dev/null 2>&1; then
  (cd "$output_dir" && shasum -a 256 "$(basename "$zip_path")" > "$(basename "$checksum_path")")
elif command -v sha256sum >/dev/null 2>&1; then
  (cd "$output_dir" && sha256sum "$(basename "$zip_path")" > "$(basename "$checksum_path")")
else
  fail "neither shasum nor sha256sum is available to create $checksum_path"
fi

echo "Created portable unsigned app bundle at $app_dir"
echo "Created distribution ZIP at $zip_path"
echo "Created SHA-256 checksum at $checksum_path"
