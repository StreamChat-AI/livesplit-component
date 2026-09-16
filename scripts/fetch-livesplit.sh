#!/usr/bin/env bash
# Downloads the LiveSplit release the component is compiled against and pulls
# out the two assemblies it references. They are not committed: LiveSplit's
# licence allows it, but a checked-in binary quietly goes stale.
set -euo pipefail
VERSION="${LIVESPLIT_VERSION:-1.8.37}"
cd "$(dirname "$0")/.."
mkdir -p lib
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
curl -sfL -o "$tmp/ls.zip" "https://github.com/LiveSplit/LiveSplit/releases/download/${VERSION}/LiveSplit_${VERSION}.zip"
unzip -q -o "$tmp/ls.zip" -d "$tmp/ls"
find "$tmp/ls" \( -name LiveSplit.Core.dll -o -name UpdateManager.dll \) -exec cp {} lib/ \;
ls -l lib
