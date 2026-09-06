#!/usr/bin/env bash
# Import images into a k3d cluster through a single-platform tarball.
#   bash scripts/k3d-import.sh <cluster> <image> [<image>...]
# Why not `k3d image import <image>` directly: with Docker's containerd image store (Docker Desktop >= 4.34 default),
# images pulled from a registry carry multi-platform attestation manifests and the import fails on every node with
# "content digest sha256:...: not found" (the pods then pull from the registry, slowly, and the console shows ERRO).
# `docker save --platform` writes only the platform the nodes run, which imports cleanly. Locally built images are
# single-platform already and work either way, so everything goes through the same path.
set -euo pipefail
CLUSTER="$1"; shift
[ $# -gt 0 ] || { echo "usage: $0 <cluster> <image>..." >&2; exit 2; }
PLATFORM="${PLATFORM:-$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}')}"
TAR="$(mktemp -d)/images.tar"
trap 'rm -rf "$(dirname "$TAR")"' EXIT

for image in "$@"; do docker image inspect "$image" >/dev/null 2>&1 || docker pull "$image" >/dev/null; done
echo "   docker save ($PLATFORM): $*"
docker save --platform "$PLATFORM" -o "$TAR" "$@" 2>/dev/null || docker save -o "$TAR" "$@"   # older Docker: no --platform
# k3d needs an absolute path; on Git Bash the POSIX form (/c/Users/...) is accepted, a relative path is not.
k3d image import -c "$CLUSTER" "$TAR"
