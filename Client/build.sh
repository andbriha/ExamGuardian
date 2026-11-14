#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
output="${1:-$source_dir/releases/linux-$(date -u +%Y%m%d-%H%M%S)}"
if [[ -e "$output" ]]; then echo 'Use a new release directory.' >&2; exit 1; fi
cmake -S "$source_dir" -B "$source_dir/build/verified-linux" -DCMAKE_BUILD_TYPE=Release -DEXAMGUARDIAN_FLAVOR=production
cmake --build "$source_dir/build/verified-linux" --parallel 2
mkdir -p -- "$output"
cp -- "$source_dir/build/verified-linux/appClient" "$output/appClient"
QT_QPA_PLATFORM=offscreen "$output/appClient" --smoke-test
sha256sum "$output/appClient" > "$output/checksums.txt"
git -C "$source_dir" rev-parse HEAD > "$output/source-commit.txt"
git -C "$source_dir" status --porcelain > "$output/source-changes.txt"
echo "Built Linux binary: $output. Install matching Qt libraries; X11 is required for environment controls."
