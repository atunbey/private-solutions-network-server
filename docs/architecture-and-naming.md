# Private Solutions Network Server

## Goal

This repository is the central server stack for balenaOS edge orchestration, policy control, identity, and account-state synchronization.

## What belongs here

- .NET APIs and workers
- Docker Compose and container definitions
- Reverse proxy configuration
- Database schema and migrations
- Shared contracts between services
- Deployment documentation

## What does not belong here

- Published binaries
- Local database volumes
- Secrets
- Ad hoc backups mixed into source folders
- Device-specific runtime state from deployed edge nodes

## Naming convention

- Repository: `private-solutions-network-server`
- Solution: `PrivateSolutions.Network.Server`
- API projects: `AdminApi`, `PolicyApi`
- Worker project: `BalenaOrchestrator`
- Shared libraries: `Platform.Data`, `Shared.Contracts`
- Container prefix: `psn-server-`
- Internal network: `psn-internal`
- Public ingress network: `psn-public`

## Storage model

- Windows workstation: primary editable source checkout
- GitHub: authoritative remote backup and collaboration point
- Linux server: deployment checkout only
- Offline archive: periodic zip/tar or git bundle kept outside the server

## Federation model

- Central server stores identities, policies, backups, and restore metadata.
- Edge devices authenticate against the central identity model or a synced local token set.
- Edge devices maintain local runtime state for resiliency.
- User-specific data is synchronized back to the central server as versioned snapshots.
- Replacement devices restore assigned user state from the central server snapshot history.

## Operational scenarios

### Scenario A: Central server as controller and system of record

- The central server controls identity, authorization, app assignment, and backup retention.
- The server decides which containerized apps a user can run on a specific edge device.
- For server-authoritative apps (example: Moodle, GNU Health), the server stores the canonical account and progress record.
- The server may host the primary web experience for these apps while edge nodes provide constrained offline or local continuity.

### Scenario B: Edge devices as constrained consumers

- Edge devices consume assigned containers and enforce access policies from the server.
- Edge devices can cache or download account data only for users who are allowed on that node.
- Logging into an edge device must not grant global visibility into all accounts or all records.
- Edge devices sync changes (for example lesson progress or health updates) back to the server for durable storage.

## Access boundary rules

- Main website (server-hosted app UI): full account scope according to app authorization policy.
- Edge device local UI: least-privilege scope limited to the authenticated user and assigned node context.
- Cross-user data browsing from edge runtime is disallowed unless an explicit admin role is granted.
- Central server remains the only place with complete record history for server-authoritative apps.

## Backup and synchronization policy

- Applications are classified into two modes:
	- `server-authoritative`: backup and restore to central server is required.
	- `edge-autonomous`: no mandatory central backup; server only coordinates installation and assignment.
- Moodle and GNU Health default to `server-authoritative` mode.
- Backup payloads are append-only snapshots with node, user, app, and capture timestamp.
- Restore operation selects the latest valid snapshot for the same user and app (optionally scoped by node policy).

## Required API behavior

- Policy API returns only the applications assigned to the authenticated user.
- Node backup ingestion accepts snapshots from authorized nodes and stores immutable records.
- Account download endpoints for edge runtimes must filter by authenticated user identity and node assignment.
- Admin APIs manage user-group-app mapping and app mode (`server-authoritative` or `edge-autonomous`).

## Example flow (Moodle)

1. User signs in on the main Moodle website hosted centrally and receives full authorized view.
2. User later signs in on an edge node and receives only their own local/offline view.
3. Edge node posts progress updates to central backup API.
4. If node is replaced, server restores the user snapshot to the replacement node.

## Suggested next repository split

- `private-solutions-network-server`: central APIs, databases, orchestration
- `private-solutions-network-edge-agent`: edge client sync/restore agent
- `private-solutions-network-admin-portal`: optional front-end if it grows beyond a static page