# Self-hosted OSRM road delivery

The API calls a private OSRM Table endpoint using the car profile. All active
metros and the store are origins; the customer is the destination. Distances
are metres along OSRM's fastest driving routes, not mathematically shortest
routes. No live traffic data is used. The metro with the smallest returned
road distance wins. One-way streets and car access depend on OpenStreetMap data.

0–1 km inclusive: 6 AZN. Above 1 through 2 km inclusive: 7 AZN. Beyond 2 km,
use the store-to-customer road distance and existing kilometre tariff. Metro
handover is 4 AZN; store pickup is free. Those two modes need no routing server.
No straight-line fallback, public demo server or Google key is used. Missing
configuration, timeout, invalid response or unavailable required route blocks
home delivery with HTTP 503. Null metro routes are excluded. Road snapping is
limited to 100 metres (configurable 1–500), preventing distant road substitution.

## Linux VPS preparation

Requires Docker Compose, curl, sufficient free RAM/disk for extraction and CH
preprocessing. Check the actual VPS resources before running alongside the shop;
preprocessing can use substantially more memory than serving requests. No paid
API account is required; hosting, maintenance and map updates remain your costs.

From the directory containing docker-compose.yml:

```sh
bash osrm/prepare.sh /srv/nemesis-osrm/initial
```

Set `OSRM_DATA_DIR=/srv/nemesis-osrm/initial` in the Compose environment or .env.
Both preparation and serving pin the same official OSRM image. CH is used for
car distance matrices. Then start only the new service:

```sh
docker compose -f docker-compose.yml -f docker-compose.osrm.yml up -d osrm
```

OSRM is exposed only to the Compose network, not the public internet. Before
activating the API, verify a real table request from that network (or temporarily
run the same image with `-p 127.0.0.1:5000:5000` for a host-only test):

```text
http://osrm:5000/table/v1/driving/49.831,40.379;49.847,40.400?sources=0&destinations=1&annotations=distance&radiuses=100;100
```

Expect code Ok and a non-negative distances[0][0]. Verify actual metro and store
coordinates and delivery boundary cases on staging. Then rebuild/recreate the API
with the same two Compose files. The override sets RoadRouting__BaseUrl internally.
Always include the override in subsequent deployments. For a separately hosted
API, set RoadRouting__BaseUrl to its reachable private OSRM address; localhost
inside an API container refers to that container, not the VPS host.

Remove any old RoadRouting__GoogleApiKey setting. No database migration is needed.
Optional settings: RoadRouting__TimeoutSeconds (1–30, default 10),
RoadRouting__MaxSnapDistanceMeters (1–500, default 100). Never expose OSRM directly
through a public reverse proxy. Road coordinates must not be added to access logs.

## Updates and rollback

Prepare each map update in a new absolute directory using the script. Keep the
old dataset. Change OSRM_DATA_DIR and recreate only OSRM, verify routing, and
revert the path if needed. Quotes fail closed during downtime. Do not replace
files under a running instance. Schedule map updates to reflect road changes.
The frontend attributes route data to OpenStreetMap contributors.

Automated tests mock HTTP. A successful application build does not confirm that
a VPS has OSRM installed or sufficient capacity; live route checks are required.

Official references:
- https://github.com/Project-OSRM/osrm-backend
- https://project-osrm.org/docs/v5.24.0/api/#table-service
- https://download.geofabrik.de/asia/azerbaijan.html
- https://www.openstreetmap.org/copyright
