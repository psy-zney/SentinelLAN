# ADR 0002: Modular monolith

Status: Accepted — 2026-08-17

Use one backend deployment with Domain → Application → Infrastructure → API dependency direction. Business modules own their data and communicate through contracts/events rather than cross-module table access. This keeps a one-student MVP operable while retaining explicit boundaries for later extraction.
