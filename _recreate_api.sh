#!/usr/bin/env bash
set -euo pipefail

ADMIN_IMG=$(docker inspect -f '{{.Image}}' psn-server-admin-api)
POLICY_IMG=$(docker inspect -f '{{.Image}}' psn-server-policy-api)

echo "ADMIN_IMG=$ADMIN_IMG"
echo "POLICY_IMG=$POLICY_IMG"

docker tag "$ADMIN_IMG" ghcr.io/atunbey/private-solutions-network-server/admin-api:2026.06.23.1
docker tag "$POLICY_IMG" ghcr.io/atunbey/private-solutions-network-server/policy-api:2026.06.23.1

cd /home/atun/private-solutions-network-server
docker compose up -d --force-recreate --no-build --pull never admin-api policy-api

docker exec psn-server-admin-api sh -lc 'printenv | grep -E ^Jwt__'
docker exec psn-server-policy-api sh -lc 'printenv | grep -E ^Jwt__'
