#!/usr/bin/env bash
# Builds the plugin and adds it to a manifest.json (the plugin repository Jellyfin
# installs from).
#
#   ./release.sh 0.1.0.0 "What changed"
#       Publishes a GitHub release and commits manifest.json to the repo.
#   ./release.sh 0.1.0.0 "What changed" http://192.168.68.86:8098
#       Local: writes dist/manifest.json pointing at that URL. Serve it with
#       `node serve-dist.js` and add <url>/manifest.json as a repository in Jellyfin.
set -euo pipefail

version=${1:?usage: release.sh <version e.g. 0.1.0.0> <changelog> [local base url]}
changelog=${2:?usage: release.sh <version> <changelog> [local base url]}
local_url=${3:-}
repo=jsutcliff/jellyfin-plugin-vizio
target_abi=12.0.0.0

cd "$(dirname "$0")"
export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet build Jellyfin.Plugin.Vizio -c Release -p:Version="$version" -p:AssemblyVersion="$version" -p:FileVersion="$version"

mkdir -p dist
zip="dist/vizio_${version}.zip"
rm -f "$zip"
zip -j "$zip" Jellyfin.Plugin.Vizio/bin/Release/net10.0/Jellyfin.Plugin.Vizio.dll

if [ -n "$local_url" ]; then
    manifest=dist/manifest.json
    source_url="${local_url%/}/$(basename "$zip")"
else
    manifest=manifest.json
    source_url="https://github.com/$repo/releases/download/v$version/$(basename "$zip")"
    gh release create "v$version" "$zip" --repo "$repo" --title "v$version" --notes "$changelog"
fi

python3 - "$version" "$changelog" "$zip" "$source_url" "$target_abi" "$manifest" <<'PY'
import datetime, hashlib, json, os, sys
version, changelog, zip_path, source_url, target_abi, path = sys.argv[1:]
manifest = json.load(open(path)) if os.path.exists(path) else [{
    "guid": "5431edc7-e380-4835-bce1-ff6712a61cd9",
    "name": "Vizio SmartCast",
    "overview": "Use Jellyfin on Vizio SmartCast TVs",
    "description": "Fixes remote navigation in the web client on Vizio SmartCast TVs and adds a link that launches Jellyfin on the TV from your phone.",
    "owner": "jsutcliff",
    "category": "General",
    "versions": [],
}]
plugin = manifest[0]
plugin["versions"] = [v for v in plugin["versions"] if v["version"] != version]
plugin["versions"].insert(0, {
    "version": version,
    "changelog": changelog,
    "targetAbi": target_abi,
    "sourceUrl": source_url,
    "checksum": hashlib.md5(open(zip_path, 'rb').read()).hexdigest(),
    "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
})
json.dump(manifest, open(path, 'w'), indent=2)
PY

[ -n "$local_url" ] && exit 0

git add manifest.json
git commit -m "Release v$version"
git push
