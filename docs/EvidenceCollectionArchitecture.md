# Evidence Collection Architecture

Purpose

This document defines the Evidence Collection subsystem for EngineOS. Evidence Collection is a permanent, first-class capability intended to convert machine-observable engineering artifacts into structured Evidence that can be used during investigation and recovery workflows. This is an architectural specification only — no code changes or domain model edits are included here.

Vision

Evidence Collection is a first-class EngineOS capability. EngineOS continuously observes the engineering environment and converts observable artifacts into structured Evidence. Evidence Collectors exist to eliminate unnecessary manual inspection: whenever the runtime or repository contains the information, EngineOS should collect it automatically instead of asking a human to copy, paste, or transcribe it.

Architectural Principle

Evidence should be collected by EngineOS whenever possible. Human engineers remain responsible for intent and decision-making; they provide context and direction, while EngineOS acquires artifacts that are machine-observable. Collectors are strictly read-only — they observe and produce Evidence but never modify engineering state or enact decisions.

Canonical Workflow

Intent

↓

Observation

↓

Hypothesis

↓

Evidence Request

↓

Evidence Collector

↓

Evidence

↓

Recovered Understanding

↓

Decision

Describe each transition:

- Intent → Observation: The engineer provides a goal or question framing what must be understood.
- Observation → Hypothesis: Observed facts lead to provisional explanations about system behavior.
- Hypothesis → Evidence Request: When uncertainty remains, the engineer (or an automated assistant) issues a request for concrete artifacts to reduce uncertainty.
- Evidence Request → Evidence Collector: A collector capable of observing the requested domain fulfills the request by gathering machine-observable artifacts.
- Evidence → Recovered Understanding: Structured Evidence is reviewed and used to update understanding; this remains a human-guided activity.
- Recovered Understanding → Decision: With sufficient evidence and human judgment, decisions are made.

Evidence Collector

Definition

An Evidence Collector is a specialized EngineOS component that fulfills an Evidence Request by observing a particular engineering environment or artifact set. Collectors are:

- Read-only and non-invasive — they must not change domain state or application behavior.
- Deterministic and auditable — they capture provenance and metadata describing when and how evidence was gathered.
- Structured — they produce Evidence in a machine-friendly format that can be consumed by downstream tools or persisted for later review.

Responsibilities

Evidence Collectors must:

- Observe engineering artifacts (runtime, build, source, VCS, tests, OS, telemetry).
- Gather runtime or static evidence relevant to a request.
- Produce structured Evidence with provenance metadata (timestamps, source, collector id/version, relevant context).
- Preserve provenance and avoid lossy transformations.
- Never make engineering decisions, perform inference beyond basic normalization, or modify engineering state.

Evidence Sources (examples)

- Presentation Layer: Live Visual Tree, control hierarchy, runtime bindings, DataContext values.
- Build: MSBuild outputs, compiler diagnostics, restore and build logs.
- Source: Roslyn syntax and semantic models, code metrics, repository topology.
- Runtime: Exceptions, application logs, ETW, performance counters.
- Version Control: Git history, branch graphs, commit metadata, PR metadata.
- Testing: Unit/integration test runs, assertions, failure traces, coverage reports.
- Operating System: Event logs, process state, file system artifacts.
- Future/Cloud: CI/CD artifacts, container runtime state, cloud telemetry.

First Implementation: Presentation Evidence Collector

Scope

The first (canonical) implementation is the Presentation Evidence Collector. Its role is to capture the presentation-layer evidence that already exists in a running desktop host (WPF) and serialize it into structured Evidence for EngineOS consumption.

Input

- A running WPF application with visual tree and bound presentation view-models.

Collected Information

- Visual tree traversal (control hierarchy and element paths).
- Control metadata (type names, element names where present).
- DataContext types for presentation regions.
- Simple rendered values (e.g., TextBlock.Text) for immediate human-observable strings.
- Projection properties exposed by presentation view-models (selected, read-only projections such as Activity.Title, Activity.Intent, etc.).
- Minimal provenance: collection timestamp, collector identifier, and application identity (process id, window title).

Output

- Presentation Evidence serialized to JSON as an initial artifact format.
- The JSON includes provenance metadata and a deterministic snapshot of the captured values.

Notes

- The initial output format is intentionally pragmatic (JSON) to accelerate delivery and review. Future iterations will migrate to canonical EngineOS Evidence objects and storage models.
- The Presentation Evidence Collector is strictly read-only and does not change application behavior or domain model state.

Design Principles

Evidence Collectors should follow these principles:

✔ Observe — Collectors read what is already observable.

✔ Measure — Collectors capture values quantitatively or deterministically where possible.

✔ Capture — Collectors persist evidence with provenance metadata.

✔ Preserve provenance — All evidence must include context about how and when it was collected.

✘ Decide — Collectors do not make decisions about root cause or remediation.

✘ Infer — Collectors should not perform heavy inference or automatic reasoning; inference belongs to a separate analysis stage.

✘ Recommend — Collector responsibilities stop at producing Evidence.

✘ Modify — Collectors never modify running system or domain state.

Extensibility

Evidence Collection is an extension point in EngineOS. Future collectors should implement a common interface (example below) and register with the EngineOS collection subsystem.

Example collector contract (conceptual)

interface IEvidenceCollector
{
	string Id { get; }
	string Description { get; }
	Task<EvidenceArtifact> CollectAsync(EvidenceRequest request, CancellationToken ct);
}

Pluggable collectors can be implemented for:

- PresentationEvidenceCollector (WPF, Blazor DOM, WebView)
- BuildEvidenceCollector (MSBuild outputs)
- GitEvidenceCollector (repository topology, commits)
- RoslynEvidenceCollector (syntax/semantic analysis)
- RuntimeEvidenceCollector (exceptions, logs)
- TelemetryEvidenceCollector (application telemetry)

Product Vision Impact

Evidence Collection transforms Continuous Recovery from a manual, ad-hoc activity into an automated engineering capability. By collecting machine-observable artifacts, EngineOS reduces the burden on engineers to perform mechanical inspections and increases repeatability, auditability, and the speed of investigations.

EngineOS does not replace engineers. Instead, it equips them with precise, machine-gathered evidence so human judgment can operate at higher leverage.

Guiding Principle

The engineer supplies intent. EngineOS collects evidence. Recovered understanding emerges from evidence, and humans remain responsible for decisions.

Appendix: Implementation Notes (non-normative)

- Start pragmatic: JSON snapshots with clear provenance are acceptable for initial iterations.
- Log snapshot locations and make them discoverable via the EngineOS UI and host logs.
- Add rate-limiting and opt-in policies for collectors that may be expensive or privacy-sensitive.
- Ensure collectors record a stable collector id and version to support backward compatibility of evidence parsers.
- Preserve immutable copies of evidence artifacts for future audits and analysis.

References

- CanonicalEngineeringModel.md
- ProductVision.md


---

Document created as the canonical specification for EngineOS Evidence Collection.

