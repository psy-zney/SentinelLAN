# Claude Code guidance

Follow `AGENTS.md` as the architecture and safety standard. Use `.agents/skills/sentinellan-dev/SKILL.md` for the normal change workflow. Treat `docs/architecture/` and `docs/security/` as source documentation.

Work in small, tested slices; preserve module dependency direction and tenant isolation. Use the repository scripts for setup, verification, and demo data. Never expose secrets or turn simulated lock/isolation commands into real device actions without an explicit, documented lab-only change.
