#!/usr/bin/env bash
# Copyright (c) 2026 Oleksiy Slyshyk
# SPDX-License-Identifier: MIT

set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd -- "$repo_root"

for rid in linux-x64 linux-arm64 linux-arm linux-musl-x64 linux-musl-arm64 linux-musl-arm; do
    printf '\nBuilding native helper for %s\n' "$rid"
    cmake --preset "$rid"
    cmake --build "artifacts/build/$rid" --target clean
    cmake --build "artifacts/build/$rid"
    cmake --install "artifacts/build/$rid"
done

dotnet pack Mono.Device.Ports/Mono.IO.Ports.csproj \
    -c Release -o artifacts/packages "$@"
