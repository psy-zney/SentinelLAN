# ADR 0009: Organize Backend source by domain

Status: Accepted — 2026-09-29

## Context

The API composition root also contained 62 business route registrations, reaching 1,090 lines. Application services and contracts for unrelated domains shared root-level files, including `ManagementServices.cs` and `Contracts.cs`. This made ownership and the location of a change difficult to identify.

## Decision

Keep the existing project dependencies and deployment model. Extract API handlers into `Endpoints/` by business domain and move DI, middleware, initialization, and route composition into `Extensions/`. `Program.cs` remains the entry point and retains its public partial type for integration tests. Keep middleware and initialization order, route paths, response contracts, authorization policies, rate limits, and development/production configuration unchanged.

Organize Application source under Authentication, Users, Organizations, Enrollment, Devices, Commands, Policies, Alerts, Audit, AssetManagement, SelfService, VpsNodes, Platform, and Common. Split management services into files named after their types and place contracts beside their owning domain. Preserve the existing `SentinelLAN.Application` namespace and public type signatures. Common retains the existing shared management port and validation; this relocation does not introduce new module boundaries or cross-module data access.

Use `company-admin` and `it-operator` for the corresponding Mobile feature folders. Preserve Expo Router paths and update their imports and existing tests.

## Consequences

New handlers and use cases have an explicit source location. Endpoint files still depend on Application and Infrastructure where the existing handlers did; moving infrastructure access behind Application ports remains a separate change. Namespace isolation and splitting the shared management port can be considered when their contracts need to change. Existing Backend integration and authorization tests and Mobile component tests verify that behavior remains compatible.

Temporary work and private deployment notes remain local under ignored `scratch/` and `docs/private/`; they must not be force-added to Git.
