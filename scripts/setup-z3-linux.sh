#!/usr/bin/env bash
# Microsoft.Z3 4.12.2 has no Linux native asset. Use the matching upstream build.
set -euo pipefail
version=4.12.2
archive="z3-${version}-x64-glibc-2.35.zip"
destination="${RUNNER_TEMP:-/tmp}/sharpcontracts-z3-${version}"
mkdir -p "$destination"
curl --fail --location --retry 3 "https://github.com/Z3Prover/z3/releases/download/z3-${version}/${archive}" --output "$destination/$archive"
echo "29604c87d74855690f5f43fa2ecb8e214af89bdddffac378ce403e29c6ea30ec  $destination/$archive" | sha256sum --check
unzip -q -o "$destination/$archive" -d "$destination"
native="$destination/z3-${version}-x64-glibc-2.35/bin"
"$native/z3" --version
# Prefer the matching library over a runner's older system libz3.
echo "LD_LIBRARY_PATH=$native${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" >> "$GITHUB_ENV"
