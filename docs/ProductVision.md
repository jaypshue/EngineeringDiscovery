# Product Vision

> *"EngineOS should eventually become capable of improving software projects using the same engineering process that was used to build EngineOS itself."*

---

# What is EngineOS?

EngineOS is the operating system for software engineering.

It does not replace IDEs.

It does not replace source control.

It does not replace AI implementation agents.

It coordinates them.

EngineOS provides the shared engineering workspace where humans, implementation agents, engineering systems, and repositories collaborate throughout the lifetime of a software project.

Evidence Collection is a first-class capability of EngineOS. Human engineers should not be required to manually inspect engineering artifacts that EngineOS can observe directly.

---

# Mission

EngineOS exists to help software engineers understand, improve, and continuously evolve software systems through a repeatable engineering workflow.

It is not a static analysis tool.

It is not an AI coding assistant.

It is an Engineering Intelligence Platform.

Its objective is not to replace engineers.

Its objective is to help engineers make consistently better engineering decisions.

---

# EngineOS Design Principles

- Conversation is not the product.
- Conversation is transient. Engineering understanding is durable.
- The Engineering Model is the system of record.
- Every interaction should improve the Engineering Model.
- AI context must be visible before implementation.
- Engineering Packages are first-class artifacts.
- Engineering intent should be reviewed before implementation.
- The IDE is an attached workspace, not an external application.
- EngineOS orchestrates engineering. Implementation agents perform specialized work.
- Make engineering state observable, not AI internals.
- Prevent context mistakes before they happen.
- Every feature must help an engineer make a better decision.
- EngineOS is the workspace where engineering intent is created, refined, and delivered to implementation agents.

---

# Engineering Package

The Engineering Package is the canonical implementation artifact produced from an engineering investigation.

Conversation explores ideas.

The Engineering Model accumulates understanding.

The Engineering Package captures implementation intent.

The Engineering Package is continuously synthesized from the current engineering investigation and remains reviewable until approved for implementation.

Before implementation, the engineer reviews the package to ensure the correct repository context, architectural decisions, evidence, and constraints are included.

The package is then submitted to an implementation agent (GitHub Copilot today, other agents in the future).

The implementation agent is replaceable.

The Engineering Package is not.

---

# Implementation Agents

EngineOS does not generate code directly.

EngineOS prepares Engineering Packages for implementation agents.

An implementation agent is any AI system capable of consuming an Engineering Package and producing engineering work.

Examples include:

- GitHub Copilot
- ChatGPT
- Claude Code
- Future compatible implementation agents

EngineOS is responsible for:

- engineering context
- investigations
- evidence
- architectural decisions
- Engineering Package generation
- validation
- response incorporation

Implementation agents are responsible for:

- producing code
- explaining implementations
- proposing designs
- making code modifications

EngineOS is designed for professional engineering workflows where an implementation agent is available.

EngineOS intentionally remains implementation-agent agnostic so the engineering workflow outlives any particular model or vendor.

---

# The Steward Principle

**EngineOS is the steward of the engineering workspace.**

Human engineers and implementation agents contribute observations, implementations, reviews, and decisions.

Engineering systems contribute evidence.

EngineOS validates, records, and maintains the canonical engineering state.

---

# Engineering Responsibilities

Successful engineering requires four distinct responsibilities.

## Human Engineers contribute intent.

Humans decide:

- what should be built
- business priorities
- tradeoffs
- product direction
- final approval

Humans remain responsible for engineering decisions.

---

## Implementation Agents contribute specialized work.

Implementation agents assist by providing:

- implementation guidance
- architectural analysis
- engineering reviews
- explanations
- recommendations
- code generation

Implementation agents accelerate engineering.

They do not own engineering.

---

## Engineering Systems contribute evidence.

Systems continuously provide objective facts.

Examples include:

- Git
- Visual Studio
- Build
- Unit Tests
- Playwright
- CI/CD
- Performance Benchmarks
- Repository Providers

Systems do not make decisions.

They provide evidence.

---

## EngineOS contributes understanding.

EngineOS continuously maintains:

- engineering context
- engineering history
- engineering state
- engineering memory
- engineering recommendations

EngineOS coordinates engineering.

---

# Engineering Principle #1

Engineers do not need help reading code.

They need help understanding systems.

---

# Engineering Principle #2

Understanding precedes implementation.

Implementation without understanding creates technical debt.

---

# Engineering Principle #3

Engineering decisions require evidence.

Recommendations become engineering state only after sufficient evidence has been collected.

---

# Engineering Principle #4

Engineering intent should be reviewed before implementation.

Conversation develops understanding.

Engineering Packages capture implementation intent.

Implementation begins only after engineering review.

---

# The Shared Engineering Workspace

The shared workspace is the canonical representation of an engineering effort.

It remembers what humans should not have to remember.

Examples include:

- current milestone
- current task
- engineering roadmap
- architecture decisions
- engineering journal
- repository understanding
- implementation progress
- Engineering Packages
- investigation history
- test coverage
- verification status
- engineering evidence

The workspace belongs to EngineOS.

Participants contribute to it.

EngineOS maintains it.

---

# Attached Workspaces

EngineOS may attach to one or more engineering environments.

Examples include:

- Visual Studio
- Source Repositories
- Build Systems
- CI/CD
- Test Infrastructure

Attached workspaces contribute engineering evidence and engineering context.

EngineOS remains the steward of the engineering state.

---

# Engineering Investigations

Engineering work is organized into investigations.

Investigations accumulate:

- evidence
- engineering understanding
- architectural decisions
- Engineering Packages

An investigation concludes when sufficient evidence exists to support engineering decisions.

The Engineering Model retains the resulting understanding.

---

# Engineering Events

Engineering progresses through events.

Examples include:

- Milestone Started
- Task Selected
- Architecture Reviewed
- Evidence Submitted
- Tests Passed
- Screenshot Verified
- Commit Created
- Milestone Completed

The engineering workspace is the current projection of those events.

---

# Evidence

Engineering confidence comes from evidence.

Examples include:

- successful build
- passing tests
- Playwright verification
- screenshots
- architectural review
- human approval

Work is not considered complete simply because it has been implemented.

Work becomes engineering knowledge after it has been verified.

---

# The Engineering Model

EngineOS builds an objective representation of a software system.

The model contains engineering facts.

Examples include:

- projects
- namespaces
- types
- dependencies
- relationships
- metrics
- architecture
- repository structure

Engineering reasoning is performed against the Engineering Model rather than directly against source code.

---

# Continuous Recovery Principle

Continuous Recovery proceeds by requesting the single highest-value piece of evidence that will most increase engineering understanding.

---

# Continuous Learning

Every completed milestone increases future engineering understanding.

Every engineering review improves future recommendations.

Every verified implementation improves the Engineering Model.

Every completed investigation strengthens future engineering decisions.

The engineering process becomes continuously more intelligent.

---

# Core Workflow

EngineOS supports two equal starting points.

## Discover Existing Software

Discover the engineering behind an existing system.

Repository

↓

Engineering Discovery

↓

Engineering Model

↓

Assessment

↓

Roadmap

↓

Implementation

↓

Updated Engineering Model

---

## Begin Engineering Workflow

Discover the engineering workflow behind EngineOS—and use it to build your own.

Vision

↓

Architecture

↓

Milestones

↓

Engineering Investigation

↓

Engineering Package

↓

Implementation

↓

Evidence

↓

Engineering State

↓

Continuous Improvement

---

# Long-Term Vision

EngineOS should eventually guide software projects through the same engineering process used to build EngineOS itself.

The platform should become capable of:

- understanding a software system
- assessing engineering health
- generating roadmaps
- coordinating implementation agents
- validating engineering evidence
- maintaining engineering memory
- continuously improving engineering understanding

Every project benefits from the experience accumulated across previous projects.

---

# Success

EngineOS succeeds when engineers can confidently answer:

- What is this system?
- Why was it built this way?
- What should happen next?
- Why is that the next priority?
- What evidence supports this decision?
- How does today's work improve tomorrow's engineering understanding?

The ultimate goal is not faster coding.

The ultimate goal is better engineering.