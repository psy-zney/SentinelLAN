# ADR 0028: Trusted proxies and attachment metadata

Date: 2026-10-21. Status: Accepted.

## Context

The API runs behind Nginx. Client-controlled forwarding headers must not change the IP used by authentication/rate limiting. Support image lists previously loaded encrypted image content merely to calculate its plaintext size.

## Decision

Run ForwardedHeadersMiddleware first, processing only X-Forwarded-For and X-Forwarded-Proto. Configure explicit KnownProxies and bounded KnownIPNetworks (.NET 10's replacement for KnownNetworks) through environment variables. Consume one hop and require symmetric header chains. Defaults trust loopback only; VPS Compose assigns Nginx a fixed address and trusts that peer only. Invalid or empty trust configuration fails startup.

Persist the original attachment Size separately from Content. A new migration backfills legacy plaintext and the v1 AES-GCM envelope. Infrastructure projects metadata directly to the existing Application DTO with AsNoTracking, and uses COUNT for upload limits. Downloads retain the original tenant/request checks and decrypt content on demand.

## Consequences and verification

Operators must choose a free Docker subnet and configure the actual immediate proxy peer; edge proxies must overwrite client forwarding headers. No X-Forwarded-Host is accepted. The migration touches existing attachment rows and requires the normal backup/maintenance procedure. Attachment HTTP contracts remain unchanged.

Regression tests cover trusted/untrusted peers, IPv4/IPv6, one-hop limits, mismatched headers and invalid configuration. Npgsql query interception verifies that the actual list/count SQL keeps tenant/request filters and does not select Content, without database I/O. HTTP tests check original image size and download bytes; PostgreSQL migration validation remains a separate deployment check.
