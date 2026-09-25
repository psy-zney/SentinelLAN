# ADR 0002: Modular monolith

Status: Accepted — 2026-08-17

Use one backend deployment. Application depends on Domain; Infrastructure implements Application ports; API composes Application and Infrastructure. Business modules own their data and communicate through contracts/events rather than cross-module table access. This keeps the MVP operable while retaining explicit boundaries for later extraction.
