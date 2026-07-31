# EngineeringDiscovery

## Overview

EngineeringDiscovery is an experimental engineering workflow platform for conducting structured investigations of software systems.

An Investigation is the persistent engineering knowledge model of a software system.

It is progressively enriched as engineers and automated workflows discover, analyze, interpret, design, implement, and review software.

An Investigation preserves both factual observations and engineering reasoning, allowing every phase of the software lifecycle to build upon a shared understanding rather than creating isolated documents or disconnected analyses.

The Investigation is the single source of truth for the engineering workflow.

Rather than beginning with AI or code generation, EngineeringDiscovery begins with understanding.

The platform captures engineering knowledge within a shared domain model and enables multiple engineering roles to collaborate through that model without directly depending on one another.

The long-term goal is to transform engineering knowledge into high-quality implementation guidance and AI-assisted software production while preserving engineering discipline and architectural integrity.

---

## Design Philosophy

EngineeringDiscovery is guided by several core principles:

- Domain-first architecture
- Workflow drives architecture
- Shared domain model
- Role-specific workspaces
- Business rules belong in the domain
- Infrastructure is introduced only when earned

The application intentionally delays persistence, AI integration, and infrastructure until the workflow demonstrates a genuine need for them.

---

## Engineering Principle

Each workspace presents a role-specific view of the shared Investigation.

The Investigation serves as the single source of truth for every engineering role. Each role contributes additional knowledge while building upon the understanding established by previous phases.

---

## The Investigation

An Investigation is the persistent knowledge model of a software system.

It is progressively enriched as EngineeringDiscovery discovers, analyzes, interprets, and reasons about source code. Every phase contributes additional understanding without replacing earlier findings, allowing the Investigation to evolve from simple discovery into architectural insight, implementation planning, development guidance, and review.

The Investigation is a living model, not a static report. It can be revisited, extended, validated, and refined as new evidence becomes available. Every phase contributes to the same evolving body of engineering knowledge.

---

## Investigation Lifecycle

```text
Discovery
    ↓
Analysis
    ↓
Architecture
    ↓
Planning
    ↓
Development
    ↓
Review
```

Each phase enriches the same Investigation.

---

## Summary

EngineeringDiscovery is not a .NET analysis tool.

EngineeringDiscovery is a language-agnostic engineering reasoning platform.

Language providers discover facts.

Engineering workspaces reason over those facts.

The Investigation is the central artifact that connects every phase of the engineering process.