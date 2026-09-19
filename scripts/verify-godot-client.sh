#!/usr/bin/env bash
set -euo pipefail

# Keep the Godot editor out of the repository and verify the exact archived
# .NET-capable engine in CI. The project itself remains a normal C# project,
# while this step proves Godot can import its scene and build the attached code.
readonly GODOT_VERSION="4.7.2"
readonly GODOT_RELEASE="4.7.2-stable"
readonly GODOT_ARCHIVE="Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip"
readonly GODOT_SHA256="129f82db7bafd54ae14bb5bb284041c73860e8c7a009a3a026ca5e946cbff247"
readonly GODOT_URL="https://github.com/godotengine/godot/releases/download/${GODOT_RELEASE}/${GODOT_ARCHIVE}"
readonly DOTNET_RUNTIME_VERSION="8.0.31"
readonly DOTNET_RUNTIME_ARCHIVE="dotnet-runtime-${DOTNET_RUNTIME_VERSION}-linux-x64.tar.gz"
readonly DOTNET_RUNTIME_SHA256="e2e392eedd49fd5a6eba078d508383fa9191059cb362c9d93cf533d5207b8db6"
readonly DOTNET_RUNTIME_URL="https://builds.dotnet.microsoft.com/dotnet/Runtime/${DOTNET_RUNTIME_VERSION}/${DOTNET_RUNTIME_ARCHIVE}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scratch_root="${RUNNER_TEMP:-$(mktemp -d)}/agentworld-godot-${GODOT_VERSION}"
archive_path="${scratch_root}/${GODOT_ARCHIVE}"
tool_root="${scratch_root}/tool"
dotnet_root="${scratch_root}/dotnet"
dotnet_archive_path="${scratch_root}/${DOTNET_RUNTIME_ARCHIVE}"

mkdir -p "${scratch_root}"
printf 'Downloading Godot %s and its required .NET runtime\n' "${GODOT_VERSION}"
curl --fail --location --retry 3 --retry-all-errors --silent --show-error --output "${archive_path}" "${GODOT_URL}"
printf '%s  %s\n' "${GODOT_SHA256}" "${archive_path}" | sha256sum --check --status
curl --fail --location --retry 3 --retry-all-errors --silent --show-error --output "${dotnet_archive_path}" "${DOTNET_RUNTIME_URL}"
printf '%s  %s\n' "${DOTNET_RUNTIME_SHA256}" "${dotnet_archive_path}" | sha256sum --check --status
unzip -qq "${archive_path}" -d "${tool_root}"
mkdir -p "${dotnet_root}"
tar -xzf "${dotnet_archive_path}" -C "${dotnet_root}"

godot_bin="$(find "${tool_root}" -type f -name "Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64" -print -quit)"
test -n "${godot_bin}"

DOTNET_ROOT="${dotnet_root}" "${godot_bin}" --headless --path "${repo_root}/src/AgentWorld.GodotClient" --build-solutions --quit
DOTNET_ROOT="${dotnet_root}" "${godot_bin}" --headless --path "${repo_root}/src/AgentWorld.GodotClient" --quit-after 120
