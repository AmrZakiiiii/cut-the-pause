#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solution="$repo_root/CutThePause.sln"

required_projects=(
  "src/CutThePause.Core/CutThePause.Core.csproj"
  "src/CutThePause.Infrastructure/CutThePause.Infrastructure.csproj"
  "src/CutThePause.App/CutThePause.App.csproj"
  "tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj"
  "tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj"
  "tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj"
)

solution_projects="$(dotnet sln "$solution" list | tr '\\' '/')"
for project in "${required_projects[@]}"; do
  if ! grep -Fq "$project" <<<"$solution_projects"; then
    echo "Solution is missing required project: $project" >&2
    exit 1
  fi
done

workflow_files=(
  "$repo_root/.github/workflows/ci.yml"
  "$repo_root/.github/workflows/release.yml"
)
required_test_projects=(
  "tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj"
  "tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj"
  "tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj"
)
for workflow in "${workflow_files[@]}"; do
  for project in "${required_test_projects[@]}"; do
    if ! grep -Fq "dotnet test $project" "$workflow"; then
      echo "Workflow $(basename "$workflow") is missing explicit test command: dotnet test $project" >&2
      exit 1
    fi
  done
done

release_workflow="$repo_root/.github/workflows/release.yml"
if ! grep -Eq 'dotnet restore CutThePause\.sln --runtime osx-arm64' "$release_workflow"; then
  echo "Release workflow must restore the solution for runtime osx-arm64." >&2
  exit 1
fi

if ! grep -Eq 'dotnet publish .*--runtime osx-arm64 .*--no-restore' "$release_workflow"; then
  echo "Release workflow must publish osx-arm64 with --no-restore." >&2
  exit 1
fi

echo "Validated solution membership, explicit test commands in ${#workflow_files[@]} workflows, and release runtime restore/publish contracts."
