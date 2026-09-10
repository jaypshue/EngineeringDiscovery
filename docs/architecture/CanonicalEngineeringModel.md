# Canonical Engineering Model

> *EngineOS is the steward of engineering understanding.*

---

## Foundational Principle

Humans contribute intent.

AI contributes reasoning.

Systems contribute evidence.

EngineOS contributes understanding.

Everything within EngineOS exists to increase, preserve, recover, or apply engineering understanding.

Engineering understanding is the primary asset managed by the platform.

## Folder and EngineOS Project

**EngineOS doesn't manage projects. EngineOS understands software.**

A user-selected folder or repository is the actual software context on disk. The internal `Project` model is EngineOS's durable representation of the understanding accumulated for that folder: identity, repository evidence, architecture, investigations, decisions, issues, activity history, current understanding, and resume information.

Opening a folder must therefore recognize or establish the associated internal understanding. The user does not create, save, or manage a separate EngineOS project. The explicit relationship is `ImportedRepository` (folder) → `Project` (durable EngineOS understanding); legacy workspace-level path, investigation, and project-state fields remain compatibility projections until migration is complete.
