# Engineering Discovery Pipeline

EngineeringDiscovery transforms a software repository into engineering knowledge through a sequence of deterministic stages.

Each stage has a single responsibility.

Each stage produces a canonical engineering artifact.

Each canonical artifact has exactly one producer.

Downstream stages consume these artifacts rather than reconstructing them.

---

## Discovery Pipeline

```
Repository
    │
    ▼
Repository Provider
    │
    ▼
CompilationContext
    │
    ▼
Discovery
    │
    ▼
TypeObservations
    │
    ▼
Observation Normalization
    │
    ▼
Normalized Engineering Model
    │
    ▼
Graph Population
    │
    ▼
RepositoryRelationshipGraph
    │
    ▼
Repository Metrics
    │
    ▼
Engineering Rules
    │
    ▼
Engineering Findings
    │
    ▼
Presentation
```

---

# Stage Responsibilities

## Repository Provider

**Purpose**

Translate repository-specific technologies into a language-neutral engineering representation.

Examples:

- C#
- Java
- Kotlin
- TypeScript
- Python

**Produces**

CompilationContext

---

## Discovery

**Purpose**

Extract objective engineering facts from the CompilationContext.

Discovery never performs engineering analysis.

It simply discovers what exists.

**Produces**

TypeObservations

---

## Observation Normalization

**Purpose**

Canonicalize engineering facts.

Normalization removes ambiguity without introducing interpretation.

Examples:

- canonical names
- normalized accessibility
- deterministic identifiers

**Produces**

Normalized Engineering Model

---

## Graph Population

**Purpose**

Construct the canonical RepositoryRelationshipGraph.

This stage owns all engineering relationships.

Examples:

- inheritance
- dependencies
- implementation
- containment

No other component constructs relationships.

**Produces**

RepositoryRelationshipGraph

---

## Repository Metrics

**Purpose**

Compute objective engineering measurements from the canonical graph.

Examples:

- Fan-In
- Fan-Out
- Inheritance Depth
- Derived Type Count
- Repository Totals

Metrics are measurements.

They are not engineering judgments.

**Produces**

RepositoryMetrics

---

## Engineering Rules

**Purpose**

Interpret the Engineering Model.

Rules combine engineering facts and metrics to identify opportunities, risks, and recommendations.

Rules never rediscover repository facts.

Rules never compute repository metrics.

**Produces**

Engineering Findings

---

## Presentation

**Purpose**

Present engineering knowledge to users.

Presentation does not compute engineering facts.

It visualizes and explains the outputs of previous stages.

---

# Canonical Engineering Artifacts

| Artifact | Producer | Purpose |
|-----------|----------|---------|
| Repository | User | Canonical project input |
| CompilationContext | Repository Provider | Language-neutral representation of a project |
| TypeObservation | Discovery | Objective engineering facts |
| Normalized Engineering Model | Observation Normalization | Canonical engineering facts |
| RepositoryRelationshipGraph | GraphPopulationEnricher | Canonical repository topology |
| RepositoryMetrics | RepositoryMetricsEnricher | Objective engineering measurements |
| Engineering Findings | Engineering Rules | Engineering interpretation |
| Presentation Artifacts | Presentation | User-facing engineering guidance |

---

# Architectural Principles

## Repository First

EngineeringDiscovery reasons about repositories, not IDEs.

The repository root is the canonical input.

---

## Single Responsibility

Every stage owns one responsibility.

Responsibilities are never duplicated.

---

## Single Producer

Every canonical engineering artifact has exactly one producer.

There should never be competing implementations.

---

## Single Source of Truth

Every engineering fact exists in exactly one canonical location.

Consumers query the canonical artifact rather than recomputing information.

---

## Deterministic Pipeline

Running the pipeline multiple times against the same repository should produce the same Engineering Model.

---

## Language Neutral

The Engineering Model contains engineering concepts rather than language-specific compiler constructs.

Repository Providers perform language translation.

---

## Progressive Knowledge

Each stage enriches the understanding produced by the previous stage.

Knowledge only flows forward through the pipeline.

No stage reaches backward to rediscover information.

---

# Vision

The Engineering Discovery Pipeline is the core of the platform.

Every future capability—including architecture visualization, impact analysis, roadmap generation, engineering guidance, AI-assisted implementation, and continuous repository improvement—should build upon these canonical engineering artifacts rather than introducing new sources of truth.