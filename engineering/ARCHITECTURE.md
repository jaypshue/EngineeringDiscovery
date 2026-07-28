# Engineering Discovery Architecture

## Mission

Engineering Discovery exists to create the most believable live engineering investigation anyone has seen.

Every architectural decision should answer one question:

> Does this increase the user's confidence that our conclusions are based on observable evidence?

If not, reconsider the design.

---

# Core Principles

## Evidence First

Engineering Discovery never invents findings.

Every finding must be supported by observable evidence collected during the investigation.

---

# Engineering Principles

## Repository as the Engineering Brain

The repository is the authoritative source of project-specific engineering knowledge.

Engineering assets exist to make engineering decisions explicit rather than implicit.

Any competent engineer—human or AI—should be able to join the project, read the engineering assets, and contribute consistently without prior project-specific training.

If knowledge is required to contribute consistently, it should be encoded in the repository rather than relying on memory, prompts, or tribal knowledge.

---

## Role-Based Engineering Agents

AI participants operate in well-defined engineering roles.

Examples include:

- Architect
- Planner
- Developer
- Reviewer

Each role has distinct responsibilities.

Agents do not coordinate through shared memory or private conversations.

They coordinate by reading and contributing to shared engineering assets stored in the repository.

This allows different AI models, tools, and human engineers to participate in the same engineering process while maintaining consistent project knowledge.

---

## Deterministic Before AI

Version 1 is completely deterministic.

The investigation engine discovers facts.

AI may explain findings in the future, but it never creates them.

---

## Investigation, Not Analysis

Engineering Discovery performs investigations.

Investigations produce:

- Evidence
- Findings
- Recommendations

This vocabulary is used consistently throughout the solution.

---

## Behavior Over Data

Domain models contain behavior.

Avoid anemic models that are only collections of properties.

Prefer:

investigation.Start()

instead of

investigation.Status = Started

---

## Framework Independence

EngineeringDiscovery.Core must not depend on:

- ASP.NET Core
- Entity Framework
- Blazor
- Serialization libraries
- Logging frameworks

Core represents the engineering domain only.

---

## Simplicity Wins

Do not introduce abstractions until there is a demonstrated need.

Favor straightforward implementations over speculative extensibility.

Earn complexity.

---

# Solution Structure

EngineeringDiscovery.Api

Hosts the application.

Contains controllers and API configuration.

No engineering logic.

---

EngineeringDiscovery.Core

Contains the engineering domain.

Owns:

- Investigation
- Finding
- Evidence
- Recommendation

Contains business rules.

Contains no infrastructure concerns.

---

Additional projects should only be created when they represent a meaningful architectural boundary.

---

# Domain Vocabulary

## Investigation

A complete examination of a software repository.

---

## Evidence

An observable fact discovered during an investigation.

---

## Finding

A conclusion derived from one or more pieces of evidence.

---

## Recommendation

An actionable engineering improvement based on a finding.

---

## Investigation Step

A deterministic operation that contributes evidence or findings.

---

# Coding Standards

Use meaningful names.

Avoid abbreviations.

Prefer immutable objects where practical.

Favor composition over inheritance.

Make invalid states impossible whenever practical.

---

# AI Collaboration

When generating code:

- Preserve the existing architecture.
- Do not introduce frameworks into Core.
- Do not invent additional layers without justification.
- Prefer readable code over clever code.
- Ask whether new abstractions are necessary.
- Keep generated code consistent with the domain vocabulary.

The architecture is more important than the implementation.


# Product Philosophy

The product is not the report.

The product is the investigation.

Users should feel they are watching a real engineering investigation unfold.

Whenever possible:

- Show the work.
- Show progress.
- Show evidence.
- Show reasoning.

Do not hide the investigation behind a loading spinner.

## Work Items

Work items define implementation scope for a single milestone.

A work item answers:

- What are we building?
- How do we know it is complete?
- What is explicitly out of scope?

A work item does not:

- Design the solution.
- Describe implementation details.
- Document future milestones.
- Replace the architecture.