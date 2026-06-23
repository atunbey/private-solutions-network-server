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

## Suggested next repository split

- `private-solutions-network-server`: central APIs, databases, orchestration
- `private-solutions-network-edge-agent`: edge client sync/restore agent
- `private-solutions-network-admin-portal`: optional front-end if it grows beyond a static page