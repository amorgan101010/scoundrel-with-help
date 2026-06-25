#!/usr/bin/env bash
# Run gdUnit4 scene tests.
# Usage: ./run_scene_tests.sh [scan_path]
set -e

GODOT=/usr/lib/godot-mono/godot.linuxbsd.editor.x86_64.mono
PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
SCAN_PATH="${1:-res://scene_tests/}"

# Build C# project so the DLL is up to date.
dotnet build "$PROJECT_DIR/ScoundrelWithHelp.csproj" --configuration Debug --nologo -v quiet

# Reimport to repopulate the global GDScript class cache (required after every build).
DISPLAY=:0 "$GODOT" --path "$PROJECT_DIR" --import --quit 2>/dev/null || true

# Run tests.
DISPLAY=:0 "$GODOT" --path "$PROJECT_DIR" \
  -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd \
  -a "$SCAN_PATH"

# Link the newest report to reports/latest so it's easy to inspect without
# knowing the run number.
LATEST=$(ls -1d "$PROJECT_DIR/reports/report_"* 2>/dev/null | sort -V | tail -1)
if [ -n "$LATEST" ]; then
  ln -sfn "$LATEST" "$PROJECT_DIR/reports/latest"
  echo "Results: $LATEST/results.xml (also at reports/latest/results.xml)"
fi
