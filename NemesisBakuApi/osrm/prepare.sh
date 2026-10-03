#!/usr/bin/env bash
set -euo pipefail
# Run on the Linux VPS. Prepare a new directory; never modify the serving dataset.
image=ghcr.io/project-osrm/osrm-backend:v26.10.0
root="${1:?Usage: bash osrm/prepare.sh /absolute/path/to/new-dataset-directory}"
[[ "$root" = /* ]] || { echo 'An absolute directory is required.' >&2; exit 1; }
[[ ! -e "$root" ]] || { echo 'Choose a new directory; existing data is preserved.' >&2; exit 1; }
command -v docker >/dev/null
command -v curl >/dev/null
mkdir -p "$root"
curl --fail --location --retry 3 https://download.geofabrik.de/asia/azerbaijan-latest.osm.pbf -o "$root/azerbaijan-latest.osm.pbf"
docker pull "$image"
docker run --rm --volume "$root:/data" "$image" osrm-extract --threads 2 -p /opt/car.lua /data/azerbaijan-latest.osm.pbf
docker run --rm --volume "$root:/data" "$image" osrm-contract --threads 2 /data/azerbaijan-latest.osrm
printf 'Preparation complete. Set OSRM_DATA_DIR=%s in your Compose environment.\n' "$root"
