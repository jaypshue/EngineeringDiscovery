## What is an Investigation?

An Investigation is the persistent engineering knowledge model of a software system.

It is progressively enriched as engineers and automated workflows discover, analyze, interpret, design, implement, and review software.

Rather than producing isolated documents or disconnected analyses, an Investigation preserves both factual observations and engineering reasoning, allowing every phase of the software lifecycle to build upon a shared understanding.

An Investigation evolves through increasing levels of engineering abstraction. It begins with objective observations about a software system and progressively develops into engineering reasoning, architectural understanding, implementation planning, development guidance, and review.

EngineeringDiscovery transforms software observations into engineering understanding. Each phase enriches the Investigation with additional knowledge, allowing software observations to evolve into actionable engineering guidance and verified implementation.

The Investigation is the single source of truth for engineering knowledge throughout the software lifecycle.

## Architectural Principle

Every feature should either enrich the Investigation or consume the Investigation.

Features that do neither are likely outside the scope of EngineeringDiscovery.

## What knowledge does it contain?

An Investigation captures the engineering knowledge required to understand, evolve, and validate a software system.

The Investigation grows throughout the software lifecycle, accumulating knowledge rather than replacing it.

Examples include:

### Identity

- Investigation identifier
- Investigation name
- Repository
- Branch
- Commit
- Date created
- Current phase
- Current status

---

### Repository Knowledge

- Projects
- Project types
- Solution structure
- Assemblies
- External dependencies

---

### Code Model

- Namespaces
- Types
- Members
- Relationships
- Inheritance
- Composition

---

### Technology Knowledge

- Languages
- Frameworks
- Platforms
- Libraries
- Runtime environments

---

### Architectural Knowledge

- Layers
- Boundaries
- Architectural patterns
- Dependency relationships
- Coupling
- Cohesion

---

### Engineering Reasoning

- Findings
- Decisions
- Assumptions
- Questions
- Risks
- Technical debt
- Constraints

---

### Planning Knowledge

- Recommendations
- Proposed improvements
- Implementation tasks
- Priorities
- Estimated impact

---

### Development Knowledge

- Work completed
- Changes applied
- Implementation notes
- Validation evidence

---

### Review Knowledge

- Review findings
- Verification results
- Outstanding issues
- Approval status

## What knowledge is transient (InvestigationContext)?

InvestigationContext contains the transient execution state required to conduct an Investigation.

Unlike the Investigation itself, the InvestigationContext exists only while an Investigation is being performed. It contains information that is useful to the investigation process but is not itself engineering knowledge.

Examples include:

### Execution State

- Current Investigation
- Current phase
- Current InvestigationStep
- Execution status
- Progress

### Source Access

- Loaded solution
- Loaded projects
- Roslyn compilation
- Syntax trees
- Semantic models
- Symbol cache

### Processing State

- Temporary lookup tables
- Graphs under construction
- Working collections
- Intermediate calculations
- Performance metrics
- Diagnostics
- Cancellation state

When an Investigation completes, the InvestigationContext may be discarded without losing engineering knowledge.

---

## What knowledge is persistent (Investigation)?

The Investigation contains the persistent engineering knowledge produced throughout the software lifecycle.

Unlike the InvestigationContext, every item contained within an Investigation represents knowledge that may be valuable to future engineering activities.

Persistent knowledge includes:

- Repository identity
- Software structure
- Technologies
- Architectural understanding
- Engineering findings
- Decisions
- Assumptions
- Risks
- Questions
- Technical debt
- Recommendations
- Implementation guidance
- Review outcomes

The Investigation is intended to survive long after the analysis process has completed. It represents the accumulated engineering understanding of the software system.

---

## Which workspace owns which knowledge?

No workspace owns the Investigation.

The Investigation is shared engineering knowledge.

Each workspace is responsible for enriching specific portions of the Investigation while consuming knowledge produced by previous workspaces.

Typical responsibilities include:

### Discovery Workspace

Produces:

- Repository knowledge
- Code model
- Technology observations

Consumes:

- Repository identity

---

### Architecture Workspace

Produces:

- Architectural understanding
- Layer definitions
- Architectural findings
- Architectural recommendations

Consumes:

- Repository knowledge
- Code model
- Technology knowledge

---

### Planning Workspace

Produces:

- Implementation plans
- Tasks
- Priorities
- Dependencies
- Estimates

Consumes:

- Architecture
- Findings
- Risks
- Decisions

---

### Development Workspace

Produces:

- Implementation evidence
- Development notes
- Progress
- Validation

Consumes:

- Plans
- Tasks
- Recommendations

---

### Review Workspace

Produces:

- Review findings
- Verification
- Outstanding issues
- Approval status

Consumes:

- Implementation evidence
- Architecture
- Planning
- Development knowledge

Each workspace contributes to the same Investigation rather than maintaining independent representations of the software system.

---

## Which phase creates or updates each part of the Investigation?

Each phase enriches the Investigation by contributing additional engineering knowledge.

| Phase | Primary Contribution |
|--------|----------------------|
| Discovery | Repository knowledge, code model, technologies |
| Analysis | Dependencies, layers, engineering findings |
| Architecture | Architectural understanding, patterns, recommendations |
| Planning | Tasks, priorities, implementation strategy |
| Development | Implementation evidence, progress, validation |
| Review | Verification, approvals, remaining issues |

Knowledge is cumulative.

Later phases build upon earlier phases without replacing them.

---

## What questions can an Investigation answer?

An Investigation exists to answer engineering questions.

Examples include:

### Discovery

- What projects exist?
- What technologies are used?
- How is the solution organized?
- What code exists?

## Engineering Knowledge

Engineering knowledge is represented by Investigation Artifacts.

Artifacts are the persistent outputs produced throughout an Investigation.

Examples include:

- Findings
- Decisions
- Questions
- Risks
- Technical Debt
- Recommendations
- Constraints

Every artifact contributes to the evolving engineering understanding captured by the Investigation.

## Observation Enrichment Principle

An observation is enriched only when a downstream capability requires information that cannot be obtained efficiently from the existing observation model.

### Analysis

- How do the components depend upon one another?
- Where are architectural boundaries?
- What engineering risks exist?
- What technical debt has been identified?

### Architecture

- Is the architecture consistent?
- Which patterns are present?
- What improvements are recommended?
- Why were architectural decisions made?

### Planning

- What should be implemented next?
- What work depends on other work?
- What is the expected impact of each change?

### Development

- What has been implemented?
- Why was it implemented?
- What evidence supports the implementation?

### Review

- Was the intended outcome achieved?
- What remains unresolved?
- What recommendations remain outstanding?

Ultimately, an Investigation should answer not only **what** a software system is, but **why** it is that way, **how** it should evolve, and **whether** those changes achieved their intended outcome.