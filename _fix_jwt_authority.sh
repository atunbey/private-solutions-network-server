#!/usr/bin/env bash
set -euo pipefail

AUTHORITY='https://psnadmin.atun-bey.com/realms/private-solutions-network'

for container in psn-server-admin-api psn-server-policy-api; do
  docker exec "$container" sh -lc "sed -i 's#http://keycloak:8080/realms/private-solutions-network#${AUTHORITY}#g' /app/appsettings.json"
done

docker restart psn-server-admin-api psn-server-policy-api >/dev/null

docker exec psn-server-admin-api sh -lc 'grep -n "Authority\|Audience" /app/appsettings.json'
docker exec psn-server-policy-api sh -lc 'grep -n "Authority\|Audience" /app/appsettings.json'
