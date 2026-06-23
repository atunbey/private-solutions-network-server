# private-solutions-network-server

Containerized platform for user/group/app policy administration and balenaOS orchestration.

## Included services
- PostgreSQL (relational database)
- Redis
- Keycloak
- Admin API (.NET 8)
- Policy API (.NET 8)
- Balena Orchestrator Worker (.NET 8)
- Admin portal placeholder (nginx static page)
- Reverse proxy (nginx)

## Core policy model
- Users belong to groups.
- Groups are assigned applications.
- Policy API returns allowed applications for logged-in users.
- Orchestrator service is where balena API reconciliation logic runs.

## Moodle-first node pattern
- Moodle is treated as the primary record system for user learning progress on each node.
- Nodes authenticate users, run required apps, and periodically POST progress snapshots to the central server.
- Central server stores immutable progress backups in PostgreSQL using the Node Backup API.
- Architecture is reusable for most programs: node-local runtime + periodic authoritative backup to this server.

## Quick start
1. Copy env file:
   - cp .env.example .env
2. Start stack:
   - docker compose up -d --build
3. Access:
   - Reverse proxy: http://localhost
   - Admin API health: http://localhost/api/admin/healthz (or http://localhost:8080/healthz)
   - Policy API health: http://localhost/api/policy/healthz (or http://localhost:8081/healthz)
   - Keycloak UI: http://localhost:8082
   - Node backup endpoint: http://localhost/api/policy/node-backups

## Database migrations
Run from project root:
- dotnet tool restore
- dotnet ef database update --project src/Platform.Data/Platform.Data.csproj --startup-project src/AdminApi/AdminApi.csproj

If dotnet-ef is not installed:
- dotnet tool install --global dotnet-ef

## Notes
- JWT validation is configured to use Keycloak realm private-solutions-network.
- Seed data and full balena reconciliation implementation are left as next steps.

## GitHub submission guidance
- Yes, you should commit the scaffolded source files to GitHub.
- Do not commit secrets or machine-specific files.
- Keep .env untracked; commit .env.example only.

## Source of truth and storage rule
- The source repository must live outside the running server runtime as the system of record.
- The server may run containers built from this source, but it should not be the only place the source exists.
- Keep only source, compose files, deployment configs, and docs in Git.
- Do not store published output, build artifacts, database data directories, or secrets in the repository.

## Recommended naming scheme
- GitHub repository: private-solutions-network-server
- .NET solution display name: PrivateSolutions.Network.Server
- Compose project name: psn-server
- Container names: psn-server-admin-api, psn-server-policy-api, psn-server-orchestrator, psn-server-postgres, psn-server-redis, psn-server-keycloak
- Internal Docker network: psn-internal
- Public edge network: psn-public

## Windows local copy strategy
- Preferred: keep the canonical working copy on the Windows machine and push from there to GitHub.
- The Linux server should host a deployment checkout or cloned release branch, not the only editable source tree.
- If working through SSH, first export or clone this repository to Windows, then connect GitHub remote, then push.
- Keep a second offline backup of the Git repository archive in addition to GitHub.

## Federation direction
- Central server remains the authority for identity, policies, and backup state.
- Edge devices host local app runtimes and local user state needed for offline operation.
- Each edge node should sync user-specific state back to the central server and be able to restore that state to a replacement node.
- Moodle, GNU Health, or similar systems can remain server-primary while edge devices cache only the per-user or per-node state required for continuity.
