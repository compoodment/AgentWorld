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

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
scratch_root="${RUNNER_TEMP:-$(mktemp -d)}/agentworld-godot-${GODOT_VERSION}"
archive_path="${scratch_root}/${GODOT_ARCHIVE}"
tool_root="${scratch_root}/tool"

verify_sha256() {
    local expected="$1"
    local path="$2"
    local label="$3"
    local actual
    actual="$(sha256sum "${path}" | awk '{ print $1 }')"
    if [[ "${actual}" != "${expected}" ]]; then
        printf '%s SHA-256 mismatch\nexpected: %s\nactual:   %s\n' "${label}" "${expected}" "${actual}" >&2
        return 1
    fi
}

mkdir -p "${scratch_root}"
printf 'Downloading Godot %s\n' "${GODOT_VERSION}"
printf 'Fetching Godot archive\n'
curl --fail --location --retry 3 --retry-all-errors --silent --show-error --output "${archive_path}" "${GODOT_URL}"
printf 'Checking Godot archive SHA-256\n'
verify_sha256 "${GODOT_SHA256}" "${archive_path}" "Godot archive"
printf 'Extracting Godot archive\n'
unzip -q "${archive_path}" -d "${tool_root}"

godot_bin="$(find "${tool_root}" -type f -name "Godot_v${GODOT_VERSION}-stable_mono_linux.x86_64" -print -quit)"
test -n "${godot_bin}"

printf 'Building Godot C# scripts and starting the scene headlessly\n'
"${godot_bin}" --headless --path "${repo_root}/src/AgentWorld.GodotClient" --build-solutions --quit
"${godot_bin}" --headless --path "${repo_root}/src/AgentWorld.GodotClient" --quit-after 120
"${godot_bin}" --headless --path "${repo_root}/src/AgentWorld.GodotClient" -- --ui-smoke-test
