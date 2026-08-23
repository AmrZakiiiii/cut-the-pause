# CapCut Editable Project Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a macOS CapCut 8.7 export route that installs a native editable project containing one adjacent clip per retained range while referencing the original media.

**Architecture:** Build an editor-neutral, frame-based timeline in Core; generate and validate a versioned CapCut 8.7 JSON package in Infrastructure; install it transactionally through a separately tested environment/registry layer; expose it through a dedicated Avalonia workflow that leaves rendered export unchanged. All feature code and verification run in an isolated worktree, and all implementation edits are delegated through the user-approved GO/Ox Alpha workflow.

**Tech Stack:** .NET 8, C# 12, `System.Text.Json`, Avalonia 11.3.12, xUnit 2.5.3, FFmpeg/FFprobe, macOS filesystems and process APIs.

**Spec:** `docs/superpowers/specs/2026-08-23-capcut-editable-project-export-design.md`

## Global Constraints

- Initial runtime support is macOS only and CapCut Desktop 8.7 only.
- Project creation must fail while any CapCut process is running.
- Generated projects reference the original source path; they do not copy or transcode media.
- Existing MP4/MOV export behavior must remain unchanged.
- Existing CapCut project folders must never be modified.
- Registry installation must be backed up, race-checked, atomic, validated, and rolled back on failure.
- Unknown or structurally incompatible CapCut schemas must fail closed.
- Generated fixtures must not contain user paths, media names, account IDs, device IDs, MAC addresses, or hardware fingerprints.
- No new NuGet dependency is permitted; use the .NET 8 base class library.
- Implementation must run on branch `feature/capcut-editable-project-export` in `.worktrees/capcut-editable-project-export`.
- Every GO bridge invocation, build, and test must use the isolated worktree as its working directory.
- Implementation model is `opencode/x-preview-f-free` through the `go-developer` bridge; Codex reviews diffs and issues numbered fix rounds.
- Do not merge the feature branch into `main` automatically.

## Planned File Structure

### Core

- Create `src/CutThePause.Core/Models/RationalFrameRate.cs` — validated rational frame rate and frame-to-microsecond conversion.
- Create `src/CutThePause.Core/Models/EditableMediaDescriptor.cs` — editor-neutral source metadata.
- Create `src/CutThePause.Core/Models/EditableTimelineClip.cs` — one retained source range and target placement in frames.
- Create `src/CutThePause.Core/Models/EditableTimelinePlan.cs` — immutable source plus ordered clip timeline.
- Create `src/CutThePause.Core/Services/EditableTimelinePlanBuilder.cs` — deterministic keep-range-to-frame conversion.
- Create `tests/CutThePause.Core.Tests/EditableTimelinePlanBuilderTests.cs` — frame rounding, adjacency, bounds, rotation, and common frame rates.

### Infrastructure metadata

- Modify `src/CutThePause.Infrastructure/Models/VideoMetadata.cs` — add dimensions, rotation, rational frame rate, audio presence, and source byte size with backward-compatible defaults.
- Modify `src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoMetadataReader.cs` — parse one JSON FFprobe response and preserve duration-only fallback for analysis.
- Create `tests/CutThePause.Infrastructure.Tests/FfmpegVideoMetadataReaderTests.cs` — JSON parsing, rotation, fractional rate, audio, and fallback coverage.

### CapCut infrastructure

- Create `src/CutThePause.Infrastructure/CapCut/CapCutModels.cs` — request/result/progress/environment/package value types.
- Create `src/CutThePause.Infrastructure/CapCut/CapCutMac87Profile.cs` — sanitized schema defaults, material buckets, file manifest, and canonical filename selection.
- Create `src/CutThePause.Infrastructure/CapCut/CapCutDraftBuilder.cs` — deterministic in-memory JSON/file package generation.
- Create `src/CutThePause.Infrastructure/CapCut/CapCutDraftValidator.cs` — independent package and installed-project validation.
- Create `src/CutThePause.Infrastructure/CapCut/ICapCutFileSystem.cs` — focused filesystem seam for fault-injected tests.
- Create `src/CutThePause.Infrastructure/CapCut/PhysicalCapCutFileSystem.cs` — flushed physical-file implementation.
- Create `src/CutThePause.Infrastructure/CapCut/ICapCutProcessDetector.cs` — process-state seam.
- Create `src/CutThePause.Infrastructure/CapCut/SystemCapCutProcessDetector.cs` — detects `CapCut` and `CapCut Helper` processes.
- Create `src/CutThePause.Infrastructure/CapCut/CapCutEnvironmentProbe.cs` — registry/version/root discovery and snapshot hashing.
- Create `src/CutThePause.Infrastructure/CapCut/CapCutProjectInstaller.cs` — staging, atomic install, backup, registry replacement, final validation, and rollback.
- Create `src/CutThePause.Infrastructure/Abstractions/ICapCutProjectExportService.cs` — app-facing export contract.
- Create `src/CutThePause.Infrastructure/CapCut/CapCutProjectExportService.cs` — orchestration from source metadata through installation.
- Create `tests/CutThePause.Infrastructure.Tests/Fixtures/CapCut/Mac87/profile-contract.json` — sanitized contract keys and required file list, copied to test output.
- Modify `tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj` — copy fixture to output.
- Create `tests/CutThePause.Infrastructure.Tests/CapCutMac87ProfileTests.cs`.
- Create `tests/CutThePause.Infrastructure.Tests/CapCutDraftBuilderTests.cs`.
- Create `tests/CutThePause.Infrastructure.Tests/CapCutDraftValidatorTests.cs`.
- Create `tests/CutThePause.Infrastructure.Tests/CapCutEnvironmentProbeTests.cs`.
- Create `tests/CutThePause.Infrastructure.Tests/CapCutProjectInstallerTests.cs`.
- Create `tests/CutThePause.Infrastructure.Tests/CapCutProjectExportServiceTests.cs`.

### App

- Create `src/CutThePause.App/Services/CapCutExportPreferences.cs` — remembered successful draft root.
- Create `src/CutThePause.App/Services/ICapCutExportPreferencesStore.cs`.
- Create `src/CutThePause.App/Services/JsonCapCutExportPreferencesStore.cs` — atomic settings persistence.
- Modify `src/CutThePause.App/Services/AppBootstrapper.cs` — wire production CapCut services.
- Modify `src/CutThePause.App/ViewModels/MainWindowViewModel.cs` — CapCut command, state, progress, success, and shell actions.
- Modify `src/CutThePause.App/MainWindow.axaml` — CapCut button and dedicated progress/success overlay.
- Modify `src/CutThePause.App/MainWindow.axaml.cs` — draft-root folder picker and CapCut events.
- Modify `tests/CutThePause.App.Tests/TestDoubles.cs` — fake CapCut exporter/preferences.
- Create `tests/CutThePause.App.Tests/CapCutExportPreferencesStoreTests.cs`.
- Create `tests/CutThePause.App.Tests/MainWindowCapCutExportTests.cs`.

### Acceptance and documentation

- Create `tests/CutThePause.Infrastructure.Tests/CapCutLiveSmokeTests.cs` — environment-variable-gated real-library smoke test with exact cleanup.
- Modify `README.md` — document CapCut 8.7/macOS limits, original-media dependency, CapCut-closed requirement, and usage.

---

### Task 0: Create the isolated worktree and prove the baseline

**Files:**
- Modify before worktree creation: `.gitignore`
- Create through Git metadata: `.worktrees/capcut-editable-project-export`

**Interfaces:**
- Consumes: clean `main` at the approved plan commit.
- Produces: isolated branch `feature/capcut-editable-project-export` with a passing pre-feature baseline.

- [ ] **Step 1: Ignore the project-local worktree container**

Add this exact entry to `.gitignore` in the main checkout:

```gitignore
.worktrees/
```

- [ ] **Step 2: Verify and commit the ignore rule**

Run:

```bash
git check-ignore -v .worktrees/probe
git add .gitignore
git commit -m "chore: ignore isolated worktrees"
```

Expected: `git check-ignore` names `.gitignore`; the commit changes only `.gitignore`.

- [ ] **Step 3: Create the feature worktree**

Run from the main checkout:

```bash
git worktree add .worktrees/capcut-editable-project-export -b feature/capcut-editable-project-export
```

Expected: the new branch is checked out only in `.worktrees/capcut-editable-project-export`.

- [ ] **Step 4: Verify isolation**

Run inside the new worktree:

```bash
git rev-parse --show-toplevel
git branch --show-current
git status --short
```

Expected: top level ends in `.worktrees/capcut-editable-project-export`, branch is `feature/capcut-editable-project-export`, and status is clean.

- [ ] **Step 5: Run the complete baseline**

Run inside the worktree:

```bash
dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj
dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj
dotnet build src/CutThePause.App/CutThePause.App.csproj
```

Expected: all three test projects and the application build pass before feature edits.

### Task 1: Enrich FFprobe metadata without breaking analysis

**Files:**
- Modify: `src/CutThePause.Infrastructure/Models/VideoMetadata.cs`
- Modify: `src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoMetadataReader.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/FfmpegVideoMetadataReaderTests.cs`

**Interfaces:**
- Consumes: existing `IVideoMetadataReader.ReadAsync(string, CancellationToken)`.
- Produces: `VideoMetadata` properties `Duration`, `Width`, `Height`, `RotationDegrees`, `FrameRateNumerator`, `FrameRateDenominator`, `HasAudio`, and `FileSizeBytes`.

- [ ] **Step 1: Write failing JSON-probe tests**

Add tests using the existing fake locator/runner style. Use this representative response:

```csharp
const string ProbeJson = """
{
  "streams": [
    {
      "codec_type": "video",
      "width": 1080,
      "height": 1920,
      "avg_frame_rate": "30000/1001",
      "r_frame_rate": "30000/1001",
      "tags": { "rotate": "90" }
    },
    { "codec_type": "audio" }
  ],
  "format": { "duration": "12.345" }
}
""";

Assert.Equal(TimeSpan.FromSeconds(12.345), metadata.Duration);
Assert.Equal(1080, metadata.Width);
Assert.Equal(1920, metadata.Height);
Assert.Equal(90, metadata.RotationDegrees);
Assert.Equal(30_000, metadata.FrameRateNumerator);
Assert.Equal(1_001, metadata.FrameRateDenominator);
Assert.True(metadata.HasAudio);
```

Also test side-data rotation, invalid `avg_frame_rate` fallback to `r_frame_rate`, no-audio input, and the existing duration-only FFmpeg fallback.

- [ ] **Step 2: Run the metadata tests and confirm failure**

Run:

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter FfmpegVideoMetadataReaderTests
```

Expected: FAIL because enriched properties and JSON parsing do not exist.

- [ ] **Step 3: Extend the metadata record with compatible defaults**

Implement this exact public shape so existing `new VideoMetadata(duration)` test doubles continue compiling:

```csharp
public sealed record VideoMetadata(
    TimeSpan Duration,
    int Width = 0,
    int Height = 0,
    int RotationDegrees = 0,
    int FrameRateNumerator = 30,
    int FrameRateDenominator = 1,
    bool HasAudio = true,
    long FileSizeBytes = 0);
```

Change FFprobe arguments to request one JSON document:

```csharp
new[]
{
    "-v", "error",
    "-show_entries",
    "format=duration:stream=codec_type,width,height,avg_frame_rate,r_frame_rate:stream_tags=rotate:stream_side_data=rotation",
    "-of", "json",
    inputPath
}
```

Parse with `JsonDocument`; normalize rotation to `0`, `90`, `180`, or `270`; reduce frame-rate fractions by greatest common divisor; use `new FileInfo(inputPath).Length` when the source exists. Keep duration-only FFmpeg fallback metadata at safe defaults so ordinary analysis still succeeds without FFprobe.

- [ ] **Step 4: Run focused and regression tests**

Run:

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter "FfmpegVideoMetadataReaderTests|VideoWorkflowServiceTests|RealVideoSmokeTests"
```

Expected: PASS; analysis duration behavior is unchanged.

- [ ] **Step 5: Commit the metadata unit**

```bash
git add src/CutThePause.Infrastructure/Models/VideoMetadata.cs src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoMetadataReader.cs tests/CutThePause.Infrastructure.Tests/FfmpegVideoMetadataReaderTests.cs
git commit -m "feat: read editable video metadata"
```

### Task 2: Build the editor-neutral frame timeline

**Files:**
- Create: `src/CutThePause.Core/Models/RationalFrameRate.cs`
- Create: `src/CutThePause.Core/Models/EditableMediaDescriptor.cs`
- Create: `src/CutThePause.Core/Models/EditableTimelineClip.cs`
- Create: `src/CutThePause.Core/Models/EditableTimelinePlan.cs`
- Create: `src/CutThePause.Core/Services/EditableTimelinePlanBuilder.cs`
- Create: `tests/CutThePause.Core.Tests/EditableTimelinePlanBuilderTests.cs`

**Interfaces:**
- Consumes: `EditableMediaDescriptor` and `IEnumerable<KeepSegment>`.
- Produces: `EditableTimelinePlanBuilder.Build(EditableMediaDescriptor, IEnumerable<KeepSegment>)` returning an immutable contiguous `EditableTimelinePlan`.

- [ ] **Step 1: Write frame-plan tests**

Cover 24, 25, `30000/1001`, 30, and 60 fps with this API:

```csharp
var media = new EditableMediaDescriptor(
    "/tmp/source.mov",
    TimeSpan.FromSeconds(10),
    1920,
    1080,
    0,
    new RationalFrameRate(30_000, 1_001),
    HasAudio: true,
    FileSizeBytes: 1234);

var plan = EditableTimelinePlanBuilder.Build(media, new[]
{
    new KeepSegment(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2.5)),
    new KeepSegment(1, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(5))
});

Assert.Equal(2, plan.Clips.Count);
Assert.Equal(0, plan.Clips[0].TargetStartFrame);
Assert.Equal(plan.Clips[0].TargetFrameCount, plan.Clips[1].TargetStartFrame);
Assert.Equal(plan.Clips.Sum(clip => clip.TargetFrameCount), plan.TotalFrameCount);
```

Also assert rejection of invalid frame rates, out-of-bounds ranges, empty results, and ranges rounding below one frame. Assert 90/270-degree media exposes swapped canvas dimensions.

- [ ] **Step 2: Run tests and confirm missing-type failures**

```bash
dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj --filter EditableTimelinePlanBuilderTests
```

Expected: FAIL because the new timeline types do not exist.

- [ ] **Step 3: Implement immutable models and deterministic rounding**

Use these public signatures:

```csharp
public readonly record struct RationalFrameRate(int Numerator, int Denominator)
{
    public long TimeToNearestFrame(TimeSpan value);
    public long FrameToMicroseconds(long frameIndex);
}

public sealed record EditableMediaDescriptor(
    string SourcePath,
    TimeSpan Duration,
    int Width,
    int Height,
    int RotationDegrees,
    RationalFrameRate FrameRate,
    bool HasAudio,
    long FileSizeBytes)
{
    public int CanvasWidth { get; }
    public int CanvasHeight { get; }
}

public sealed record EditableTimelineClip(
    int Order,
    long SourceStartFrame,
    long SourceFrameCount,
    long TargetStartFrame,
    long TargetFrameCount);

public sealed record EditableTimelinePlan(
    EditableMediaDescriptor Media,
    IReadOnlyList<EditableTimelineClip> Clips,
    long TotalFrameCount);

public static class EditableTimelinePlanBuilder
{
    public static EditableTimelinePlan Build(
        EditableMediaDescriptor media,
        IEnumerable<KeepSegment> keepSegments);
}
```

Use `decimal` arithmetic plus `MidpointRounding.AwayFromZero` to avoid overflow and floating drift. Derive serialized duration as `FrameToMicroseconds(endFrame) - FrameToMicroseconds(startFrame)` rather than separately rounding duration.

- [ ] **Step 4: Run Core tests**

```bash
dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj
```

Expected: PASS, including existing export-plan tests.

- [ ] **Step 5: Commit the editor-neutral plan**

```bash
git add src/CutThePause.Core/Models src/CutThePause.Core/Services/EditableTimelinePlanBuilder.cs tests/CutThePause.Core.Tests/EditableTimelinePlanBuilderTests.cs
git commit -m "feat: build frame-based editable timelines"
```

### Task 3: Define the sanitized CapCut 8.7 profile and build packages

**Files:**
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutModels.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutMac87Profile.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutDraftBuilder.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/Fixtures/CapCut/Mac87/profile-contract.json`
- Modify: `tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj`
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutMac87ProfileTests.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutDraftBuilderTests.cs`

**Interfaces:**
- Consumes: `EditableTimelinePlan`, selected timeline filename, project/root/registry paths, injected timestamp, and injected IDs.
- Produces: `CapCutDraftPackage` containing canonical timeline bytes, all relative files, registry entry JSON, metadata entry JSON, and identity values.

- [ ] **Step 1: Add the sanitized contract fixture and failing profile tests**

The fixture must contain only contract data:

```json
{
  "appVersion": "8.7.0",
  "schemaVersion": 360000,
  "newVersion": "171.0.0",
  "allowedTimelineFiles": ["draft_content.json", "draft_info.json"],
  "materialBuckets": [
    "flowers", "videos", "tail_leaders", "audios", "images", "texts",
    "effects", "stickers", "canvases", "transitions", "audio_effects",
    "audio_fades", "beats", "material_animations", "placeholders",
    "placeholder_infos", "speeds", "common_mask", "chromas",
    "text_templates", "realtime_denoises", "audio_pannings",
    "audio_pitch_shifts", "video_trackings", "hsl", "drafts",
    "color_curves", "hsl_curves", "primary_color_wheels",
    "log_color_wheels", "video_effects", "audio_balances", "handwrites",
    "manual_deformations", "manual_beautys", "plugin_effects",
    "sound_channel_mappings", "green_screens", "shapes", "material_colors",
    "digital_humans", "digital_human_model_dressing", "smart_crops",
    "ai_translates", "audio_track_indexes", "loudnesses", "vocal_beautifys",
    "vocal_separations", "smart_relights", "time_marks", "multi_language_refs",
    "video_shadows", "video_strokes", "video_radius"
  ]
}
```

Configure the fixture:

```xml
<None Update="Fixtures/CapCut/Mac87/profile-contract.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

Tests assert exact version constants, bucket equality, empty device fingerprints, and both allowed timeline filenames.

- [ ] **Step 2: Write failing builder contract tests**

Use deterministic IDs:

```csharp
var ids = new Queue<string>(new[]
{
    "DRAFT-ID", "TIMELINE-ID", "TIMELINES-PROJECT-ID", "LOCAL-MATERIAL-ID",
    "SEGMENT-1", "VIDEO-MATERIAL-1", "SEGMENT-2", "VIDEO-MATERIAL-2"
});

var package = builder.Build(new CapCutDraftBuildRequest(
    Plan: plan,
    DisplayName: "source - Cut The Pause",
    ProjectFolderName: "source - Cut The Pause",
    ProjectRoot: "/tmp/CapCut Drafts",
    RegistryPath: "/tmp/capcut/root_meta_info.json",
    TimelineFileName: "draft_content.json",
    Now: DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)),
    () => ids.Dequeue());
```

Assert one track, two segments, adjacent target microseconds, two distinct video-material IDs sharing one `local_material_id`, one sidecar physical-source entry, three distinct identity domains, source file size in metadata/registry, and byte-identical root/nested/backup/template timeline files.

- [ ] **Step 3: Implement profile and package builder**

Define these value types in `CapCutModels.cs`:

```csharp
public sealed record CapCutDraftBuildRequest(
    EditableTimelinePlan Plan,
    string DisplayName,
    string ProjectFolderName,
    string ProjectRoot,
    string RegistryPath,
    string TimelineFileName,
    DateTimeOffset Now);

public sealed record CapCutDraftPackage(
    string DraftId,
    string TimelineId,
    string TimelinesProjectId,
    string DisplayName,
    string ProjectFolderName,
    string ProjectPath,
    string TimelineFileName,
    IReadOnlyDictionary<string, byte[]> Files,
    JsonObject RegistryEntry,
    int ClipCount,
    TimeSpan Duration);

public sealed record CapCutProjectExportProgress(string Stage, double FractionComplete);

public enum CapCutProjectExportStatus
{
    Success,
    DraftRootSelectionRequired,
    Failed
}

public sealed record CapCutProjectExportResult(
    CapCutProjectExportStatus Status,
    string? ProjectName,
    string? ProjectPath,
    int ClipCount,
    TimeSpan Duration,
    string? ExpectedVolumePath = null,
    string? ErrorMessage = null);
```

Build JSON with `JsonObject`/`JsonArray`, never string interpolation. Serialize once with a shared `JsonSerializerOptions` instance and reuse the exact timeline byte array for every mirror. Give each segment default identity companion records for speed, placeholder info, canvas, smart color identity state, realtime denoise disabled state, channel mapping, material color, loudness, vocal beautify disabled state, and vocal separation disabled state. Each `extra_material_refs` ID must resolve to its exact bucket.

Required package paths include:

```csharp
var requiredPaths = new[]
{
    timelineFileName,
    $"{timelineFileName}.bak",
    "template-2.tmp",
    "draft_meta_info.json",
    "attachment_editing.json",
    "attachment_pc_common.json",
    "draft_agency_config.json",
    "draft_agency_info.json",
    "draft_biz_config.json",
    "draft_settings",
    "draft_virtual_store.json",
    "key_value.json",
    "performance_opt_info.json",
    "timeline_layout.json",
    "common_attachment/attachment_pc_timeline.json",
    "Timelines/project.json",
    "Timelines/project.json.bak",
    $"Timelines/{timelineId}/{timelineFileName}",
    $"Timelines/{timelineId}/{timelineFileName}.bak",
    $"Timelines/{timelineId}/template-2.tmp",
    $"Timelines/{timelineId}/attachment_editing.json",
    $"Timelines/{timelineId}/attachment_pc_common.json",
    $"Timelines/{timelineId}/common_attachment/attachment_pc_timeline.json"
};
```

- [ ] **Step 4: Run profile and builder tests**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter "CapCutMac87ProfileTests|CapCutDraftBuilderTests"
```

Expected: PASS with no real paths or device values in fixture snapshots.

- [ ] **Step 5: Commit the package builder**

```bash
git add src/CutThePause.Infrastructure/CapCut/CapCutModels.cs src/CutThePause.Infrastructure/CapCut/CapCutMac87Profile.cs src/CutThePause.Infrastructure/CapCut/CapCutDraftBuilder.cs tests/CutThePause.Infrastructure.Tests/Fixtures tests/CutThePause.Infrastructure.Tests/CapCutMac87ProfileTests.cs tests/CutThePause.Infrastructure.Tests/CapCutDraftBuilderTests.cs tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj
git commit -m "feat: build CapCut 8.7 draft packages"
```

### Task 4: Validate CapCut packages independently

**Files:**
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutDraftValidator.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutDraftValidatorTests.cs`

**Interfaces:**
- Consumes: `CapCutDraftPackage` or installed project path plus expected package.
- Produces: `CapCutValidationResult(bool IsValid, IReadOnlyList<string> Errors)` from `ValidatePackage` and `ValidateInstalledAsync`.

- [ ] **Step 1: Write corruption-focused failing tests**

Start from a valid deterministic package, then mutate one condition per test:

```csharp
Assert.False(validator.ValidatePackage(package with
{
    TimelineId = package.DraftId
}).IsValid);

var missingMirror = package.Files
    .Where(pair => !pair.Key.EndsWith("template-2.tmp", StringComparison.Ordinal))
    .ToDictionary();
Assert.Contains(result.Errors, error => error.Contains("template-2.tmp"));
```

Cover duplicate identity domains, missing files, invalid JSON, dangling primary/companion refs, duplicate sidecar sources, non-adjacent target ranges, source bounds, duration mismatch, mirror-byte drift, path traversal, and registry/path mismatch.

- [ ] **Step 2: Run validator tests and confirm failure**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutDraftValidatorTests
```

Expected: FAIL because validator types do not exist.

- [ ] **Step 3: Implement structured validation**

Use this public contract:

```csharp
public sealed record CapCutValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static CapCutValidationResult Success { get; } = new(true, Array.Empty<string>());
}

public sealed class CapCutDraftValidator
{
    public CapCutValidationResult ValidatePackage(CapCutDraftPackage package);

    public Task<CapCutValidationResult> ValidateInstalledAsync(
        CapCutDraftPackage package,
        ICapCutFileSystem fileSystem,
        CancellationToken cancellationToken);
}
```

Collect all deterministic errors in one pass where safe; do not throw for user-data validation failures. Throw only for programmer argument errors or cancellation.

- [ ] **Step 4: Run focused tests**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter "CapCutDraftBuilderTests|CapCutDraftValidatorTests"
```

Expected: PASS.

- [ ] **Step 5: Commit validation**

```bash
git add src/CutThePause.Infrastructure/CapCut/CapCutDraftValidator.cs tests/CutThePause.Infrastructure.Tests/CapCutDraftValidatorTests.cs
git commit -m "feat: validate CapCut draft integrity"
```

### Task 5: Probe CapCut safely and select a compatible draft root

**Files:**
- Create: `src/CutThePause.Infrastructure/CapCut/ICapCutFileSystem.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/PhysicalCapCutFileSystem.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/ICapCutProcessDetector.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/SystemCapCutProcessDetector.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutEnvironmentProbe.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutEnvironmentProbeTests.cs`

**Interfaces:**
- Consumes: optional remembered/override draft root, internal registry, process detector, and filesystem.
- Produces: compatible `CapCutEnvironment` or a typed `CapCutDraftRootRequiredException`/`CapCutCompatibilityException`.

- [ ] **Step 1: Write environment tests**

Use temp directories plus a fake process detector. Assert:

```csharp
await Assert.ThrowsAsync<CapCutRunningException>(() =>
    probe.ProbeAsync(null, CancellationToken.None));

Assert.Equal(externalRoot, environment.ProjectRoot);
Assert.Equal("draft_content.json", environment.TimelineFileName);
Assert.Equal(expectedRegistryHash, environment.RegistrySha256);
```

Cover CapCut running, missing/malformed registry, unsupported app/schema versions, disconnected remembered external volume, explicit-root validation, most-recent mounted root selection, no root requiring picker, and both allowed timeline filenames.

- [ ] **Step 2: Run tests and confirm failure**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutEnvironmentProbeTests
```

Expected: FAIL because the environment seam and probe do not exist.

- [ ] **Step 3: Implement process, filesystem, and probe contracts**

Define focused contracts:

```csharp
public interface ICapCutProcessDetector
{
    bool IsRunning();
}

public interface ICapCutFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    byte[] ReadAllBytes(string path);
    void CreateDirectory(string path);
    void WriteAllBytesFlushed(string path, ReadOnlySpan<byte> content);
    void MoveDirectory(string source, string destination);
    void DeleteDirectory(string path, bool recursive);
    void CopyFile(string source, string destination, bool overwrite);
    void ReplaceFile(string temporaryPath, string destinationPath);
    long GetFileLength(string path);
    DateTimeOffset GetLastWriteTimeUtc(string path);
    bool CanWriteToDirectory(string path);
}

public sealed record CapCutEnvironment(
    string RegistryPath,
    string ProjectRoot,
    string TimelineFileName,
    byte[] RegistryBytes,
    string RegistrySha256,
    DateTimeOffset RegistryLastWriteTime,
    JsonObject RegistryRoot);
```

The physical detector must treat case-insensitive process names equal to `CapCut` or starting with `CapCut Helper` as running. Resolve the default registry from `Environment.SpecialFolder.MyVideos` plus `CapCut/User Data/Projects/com.lveditor.draft/root_meta_info.json`. Never log unrelated registry contents.

- [ ] **Step 4: Run focused environment tests**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutEnvironmentProbeTests
```

Expected: PASS.

- [ ] **Step 5: Commit environment probing**

```bash
git add src/CutThePause.Infrastructure/CapCut/ICapCutFileSystem.cs src/CutThePause.Infrastructure/CapCut/PhysicalCapCutFileSystem.cs src/CutThePause.Infrastructure/CapCut/ICapCutProcessDetector.cs src/CutThePause.Infrastructure/CapCut/SystemCapCutProcessDetector.cs src/CutThePause.Infrastructure/CapCut/CapCutEnvironmentProbe.cs tests/CutThePause.Infrastructure.Tests/CapCutEnvironmentProbeTests.cs
git commit -m "feat: probe compatible CapCut environments"
```

### Task 6: Install and roll back projects transactionally

**Files:**
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutProjectInstaller.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutProjectInstallerTests.cs`

**Interfaces:**
- Consumes: compatible `CapCutEnvironment`, validated `CapCutDraftPackage`, process detector, filesystem, and clock.
- Produces: installed `CapCutProjectExportResult` or a typed exception after proven rollback.

- [ ] **Step 1: Write transaction and fault-injection tests**

Create a fake filesystem decorator that throws at named operations:

```csharp
var fileSystem = new FaultingCapCutFileSystem(
    physical,
    operationToFail: CapCutFileOperation.ReplaceRegistry);

await Assert.ThrowsAsync<CapCutInstallException>(() => installer.InstallAsync(
    environment,
    package,
    CancellationToken.None));

Assert.Equal(originalRegistryBytes, File.ReadAllBytes(environment.RegistryPath));
Assert.False(Directory.Exists(package.ProjectPath));
Assert.True(File.Exists(expectedBackupPath));
```

Cover unique-name suffixing, staging validation, registry hash race, CapCut reopening before commit, source/root disappearance, project rename failure, registry replacement failure, final validation failure, successful rollback, rollback failure preserving backup paths, and byte preservation of all existing registry entries.

- [ ] **Step 2: Run installer tests and confirm failure**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutProjectInstallerTests
```

Expected: FAIL because the installer does not exist.

- [ ] **Step 3: Implement the exact transaction order**

Use this signature:

```csharp
public sealed class CapCutProjectInstaller
{
    public string ResolveUniqueProjectName(
        CapCutEnvironment environment,
        string requestedName);

    public Task<CapCutProjectExportResult> InstallAsync(
        CapCutEnvironment environment,
        CapCutDraftPackage package,
        CancellationToken cancellationToken);
}
```

Implement, in order: hidden same-root staging; flushed writes; installed-package validation; process/source/root/registry-hash recheck; atomic staging rename; timestamped backup; temporary updated registry write; parse verification; atomic replace; final re-read validation. On post-rename failure, restore the exact original registry bytes and remove only the generated project folder. Preserve backup and paths in a `CapCutRollbackException` if restoration cannot be proven.

Use collision names based on existing directories and registry `draft_name` values. Never overwrite a directory even when its registry entry is missing.

- [ ] **Step 4: Run installer and builder/validator regression tests**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter "CapCutDraftBuilderTests|CapCutDraftValidatorTests|CapCutEnvironmentProbeTests|CapCutProjectInstallerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the transaction installer**

```bash
git add src/CutThePause.Infrastructure/CapCut/CapCutProjectInstaller.cs tests/CutThePause.Infrastructure.Tests/CapCutProjectInstallerTests.cs
git commit -m "feat: install CapCut projects transactionally"
```

### Task 7: Orchestrate CapCut export behind one app-facing service

**Files:**
- Create: `src/CutThePause.Infrastructure/Abstractions/ICapCutProjectExportService.cs`
- Create: `src/CutThePause.Infrastructure/CapCut/CapCutProjectExportService.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutProjectExportServiceTests.cs`

**Interfaces:**
- Consumes: source path, reviewed `KeepSegment` values, project name, optional root override, progress, and cancellation.
- Produces: `CapCutProjectExportResult` with status, path, name, clip count, and duration.

- [ ] **Step 1: Write orchestration tests**

Use this service contract:

```csharp
var result = await service.CreateAsync(
    sourcePath: "/tmp/source.mov",
    keepSegments,
    projectName: "source - Cut The Pause",
    draftRootOverride: null,
    progress,
    CancellationToken.None);
```

Assert progress order:

```csharp
Assert.Equal(new[]
{
    "Checking CapCut...",
    "Building timeline...",
    "Validating project...",
    "Registering project...",
    "Verifying installation..."
}, stages);
```

Also test invalid metadata, no keep ranges, one-frame rounding loss, root-selection-required result, cancellation, and exception propagation without misleading success.

- [ ] **Step 2: Run service tests and confirm failure**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutProjectExportServiceTests
```

Expected: FAIL because the service contract does not exist.

- [ ] **Step 3: Implement the service contract and orchestration**

```csharp
public interface ICapCutProjectExportService
{
    Task<CapCutProjectExportResult> CreateAsync(
        string sourcePath,
        IReadOnlyList<KeepSegment> keepSegments,
        string projectName,
        string? draftRootOverride,
        IProgress<CapCutProjectExportProgress>? progress,
        CancellationToken cancellationToken);
}
```

Use the progress, status, and result types already defined in `CapCutModels.cs` in Task 3. Map `VideoMetadata` to `EditableMediaDescriptor`, build the Core plan, probe the environment, call `CapCutProjectInstaller.ResolveUniqueProjectName`, build the package with that resolved name, validate in memory, and install. Return `DraftRootSelectionRequired` only for the no-compatible-root condition; all unsafe/corrupt states remain actionable typed exceptions.

- [ ] **Step 4: Run all Infrastructure tests**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit orchestration**

```bash
git add src/CutThePause.Infrastructure/Abstractions/ICapCutProjectExportService.cs src/CutThePause.Infrastructure/CapCut/CapCutProjectExportService.cs tests/CutThePause.Infrastructure.Tests/CapCutProjectExportServiceTests.cs
git commit -m "feat: orchestrate CapCut project export"
```

### Task 8: Add preferences, ViewModel workflow, and Avalonia UI

**Files:**
- Create: `src/CutThePause.App/Services/CapCutExportPreferences.cs`
- Create: `src/CutThePause.App/Services/ICapCutExportPreferencesStore.cs`
- Create: `src/CutThePause.App/Services/JsonCapCutExportPreferencesStore.cs`
- Modify: `src/CutThePause.App/Services/AppBootstrapper.cs`
- Modify: `src/CutThePause.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/CutThePause.App/MainWindow.axaml`
- Modify: `src/CutThePause.App/MainWindow.axaml.cs`
- Modify: `tests/CutThePause.App.Tests/TestDoubles.cs`
- Create: `tests/CutThePause.App.Tests/CapCutExportPreferencesStoreTests.cs`
- Create: `tests/CutThePause.App.Tests/MainWindowCapCutExportTests.cs`

**Interfaces:**
- Consumes: `ICapCutProjectExportService`, reviewed cuts, optional saved root, Avalonia folder picker, and existing busy/cancellation state.
- Produces: one-click project creation, fallback root selection, dedicated progress/success overlay, Open CapCut, and Reveal Project.

- [ ] **Step 1: Write preferences and ViewModel tests**

Tests must assert:

```csharp
Assert.False(viewModel.CanCreateCapCutProject);
viewModel.SetInputPath("/tmp/source.mov");
await viewModel.AnalyzeAsync();
Assert.True(viewModel.CanCreateCapCutProject);

var result = await viewModel.CreateCapCutProjectAsync();
Assert.Equal(CapCutProjectExportStatus.Success, result.Status);
Assert.True(viewModel.IsCapCutProjectCompleted);
Assert.Equal("source - Cut The Pause", viewModel.CapCutProjectName);
Assert.Contains("original video", viewModel.CapCutProjectDetailText, StringComparison.OrdinalIgnoreCase);
```

Cover busy disablement, project name derivation, current enabled-cut reconstruction via `ExportPlanBuilder.BuildKeepSegments`, progress stages, cancellation, CapCut-running error, disconnected-root message, root-selection-required retry, preference saved only after success, and rendered-export state remaining unchanged.

Preferences JSON tests cover round-trip, malformed JSON fallback, and failed atomic save.

- [ ] **Step 2: Run App tests and confirm failure**

```bash
dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter "CapCutExportPreferencesStoreTests|MainWindowCapCutExportTests"
```

Expected: FAIL because CapCut app state and stores do not exist.

- [ ] **Step 3: Implement app state and production wiring**

Add this constructor extension while preserving existing optional test construction:

```csharp
public MainWindowViewModel(
    VideoWorkflowService workflowService,
    IAnalysisSettingsStore? settingsStore = null,
    IHistoryStore? historyStore = null,
    ICustomPresetStore? customPresetStore = null,
    IExportCheckpointStore? exportCheckpointStore = null,
    ICapCutProjectExportService? capCutProjectExportService = null,
    ICapCutExportPreferencesStore? capCutPreferencesStore = null)
```

Add:

```csharp
public bool CanCreateCapCutProject =>
    !_isBusy &&
    _analysisResult is not null &&
    ExportPlanBuilder.BuildKeepSegments(_analysisResult.Duration, Cuts).Count > 0 &&
    _capCutProjectExportService is not null;

public Task<CapCutProjectExportResult> CreateCapCutProjectAsync(
    string? draftRootOverride = null);

public void DismissCapCutProjectOverlay();
public void RevealCapCutProject();
public void OpenCapCut();
```

Use a separate CapCut overlay state rather than overloading `IsExporting`, so pause/checkpoint behavior remains render-only. Reuse `_operationCancellationSource` for cancellation and window-close safety.

Catch the service's typed environment, validation, install, and rollback exceptions at the ViewModel boundary, map them to actionable status text, and return a `Failed` result with `ErrorMessage`; cancellation remains cancellation and must not be reported as failure or success. A disconnected saved draft root must explain which volume/path is expected. Never expose registry JSON or unrelated CapCut project names in UI text or logs.

Wire production objects in `AppBootstrapper`: physical filesystem, system process detector, profile, probe, builder, validator, installer, CapCut service, and JSON preferences.

- [ ] **Step 4: Implement the folder picker and XAML**

Add a `Create CapCut Project` button beside rendered export. Its event performs one auto-detect attempt, then opens a folder picker only when the result status is `DraftRootSelectionRequired`:

```csharp
private async void OnCreateCapCutProjectClick(object? sender, RoutedEventArgs e)
{
    var result = await ViewModel.CreateCapCutProjectAsync();
    if (result.Status != CapCutProjectExportStatus.DraftRootSelectionRequired)
    {
        return;
    }

    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
        Title = "Choose the CapCut Drafts folder",
        AllowMultiple = false
    });

    var folder = folders.FirstOrDefault();
    if (folder is not null)
    {
        await ViewModel.CreateCapCutProjectAsync(folder.Path.LocalPath);
    }
}
```

The dedicated overlay binds progress stage/detail, success name/path/clip count/duration, the original-media warning, `Open CapCut`, `Reveal Project`, Close, and Cancel controls.

- [ ] **Step 5: Run App tests and build, then commit**

```bash
dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj
dotnet build src/CutThePause.App/CutThePause.App.csproj
git add src/CutThePause.App tests/CutThePause.App.Tests
git commit -m "feat: add CapCut project export workflow"
```

Expected: App tests and build pass; existing rendered export controls remain present.

### Task 9: Add gated live compatibility testing and documentation

**Files:**
- Create: `tests/CutThePause.Infrastructure.Tests/CapCutLiveSmokeTests.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `CUTTHEPAUSE_RUN_CAPCUT_LIVE=1`, `CUTTHEPAUSE_CAPCUT_DRAFT_ROOT`, FFmpeg, a closed CapCut 8.7 installation, and the real local registry.
- Produces: an opt-in create/open-inspection project with exact registry/project cleanup in `finally`.

- [ ] **Step 1: Write the skipped-by-default live test**

Gate before any filesystem mutation:

```csharp
if (!string.Equals(
        Environment.GetEnvironmentVariable("CUTTHEPAUSE_RUN_CAPCUT_LIVE"),
        "1",
        StringComparison.Ordinal))
{
    return;
}
```

The test creates a short synthetic 30 fps video with audio under a temporary directory, captures the exact original registry bytes, calls the production service with three known keep ranges, validates the installed project, and records its generated folder. In `finally`, while CapCut is closed, restore the exact pre-test registry bytes, delete only that recorded generated folder, and verify both cleanup operations.

- [ ] **Step 2: Run the live test in safe skipped mode**

```bash
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutLiveSmokeTests
```

Expected: PASS without reading or writing the real CapCut library.

- [ ] **Step 3: Document the feature and its limits**

Add a README section containing these explicit points:

```markdown
### Editable CapCut project export

- Available on macOS for the verified CapCut Desktop 8.7 project format.
- Close CapCut before creating a project.
- Every retained range becomes a separate adjacent timeline clip.
- The project references the original video; keep it in place and keep external volumes connected.
- Newer or incompatible CapCut versions are rejected without changing the project library.
```

- [ ] **Step 4: Run the complete automated suite**

```bash
dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj
dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj
dotnet build src/CutThePause.App/CutThePause.App.csproj -c Release
```

Expected: all automated tests pass; the live test remains non-mutating unless explicitly enabled.

- [ ] **Step 5: Commit acceptance coverage and docs**

```bash
git add tests/CutThePause.Infrastructure.Tests/CapCutLiveSmokeTests.cs README.md
git commit -m "test: cover CapCut project export acceptance"
```

### Task 10: GO fix rounds, final review, and controlled real smoke test

**Files:**
- Review: every file changed on `feature/capcut-editable-project-export`
- Do not modify: original `main` checkout application files

**Interfaces:**
- Consumes: complete delegated diff, automated test evidence, and optional real CapCut 8.7 availability.
- Produces: reviewed feature branch, reported live-compatibility status, and explicit integration choices.

- [ ] **Step 1: Review the complete diff against the spec**

Run inside the worktree:

```bash
git status --short
git log --oneline main..HEAD
git diff --stat main...HEAD
git diff --check main...HEAD
```

Inspect security boundaries, registry replacement, rollback, path containment, fixture privacy, JSON reference integrity, timing math, UI busy state, and preservation of rendered export.

- [ ] **Step 2: Send numbered GO fix rounds when findings exist**

Use one concrete list per round, for example:

```text
Fix round 1:
1. CapCutProjectInstaller must re-hash root_meta_info.json immediately before replacing it.
2. Rollback must restore the exact original bytes, not reserialize parsed JSON.
3. Add a failing test proving a moved external root leaves the registry unchanged.
Run all three test projects and report exact counts.
```

Use the same approved GO model/provider. Stop after the configured maximum three rounds or earlier when review passes.

- [ ] **Step 3: Re-run final automated verification**

```bash
dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj
dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj
dotnet build src/CutThePause.App/CutThePause.App.csproj -c Release
git status --short
```

Expected: all commands pass and worktree status is clean after reviewed commits.

- [ ] **Step 4: Run the controlled live test only when prerequisites are proven**

First verify CapCut 8.7 exists and is closed and the selected draft root is mounted. Then run inside the worktree:

```bash
CUTTHEPAUSE_RUN_CAPCUT_LIVE=1 \
CUTTHEPAUSE_CAPCUT_DRAFT_ROOT="/Volumes/AmrZaki EXT/C/CapCut Drafts" \
dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter CapCutLiveSmokeTests
```

Expected: project installation validates and cleanup restores the exact pre-test registry. If CapCut 8.7 is unavailable, report live compatibility as unverified; do not infer success from fixtures.

- [ ] **Step 5: Present integration choices without merging**

Report branch name, worktree path, GO model and rounds, changed files, test counts, build result, live-test result, and any limitation. Offer review/merge/keep/discard choices; do not modify `main` until the user chooses.
