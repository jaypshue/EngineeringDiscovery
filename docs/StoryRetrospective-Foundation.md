# Story Retrospective — Foundation

This retrospective summarizes the foundational implementation work completed through ED-300, ED-301, ED-302, ED-303, and the Platform Stabilization milestone.

## Architectural discoveries

- The Engineering Activity becomes the primary unit of engineering work and ownership for engineering artifacts.
- Presentation must remain projection-only; duplicating domain state in view models creates architectural drift.
- Seeding initial activity state at host startup simplifies early UX and testing while keeping Core authoritative.

## Changes to the Canonical Engineering Model

- Introduced Intent and Observations as first-class engineering artifacts owned by activities.
- Added Hypothesis Space as an activity-owned collection of EngineeringHypothesis objects to represent candidate explanations.
- Added Evidence Requests as an activity-owned collection that specifies what evidence should be collected to reduce uncertainty.

## Product Vision refinements

- EngineOS is explicitly the steward of engineering understanding; humans, AI, and systems contribute distinct responsibilities.
- The product focuses on increasing engineering understanding through information-gain-optimized evidence collection rather than guessing likely outcomes.

## Engineering principles discovered

- Engineering work should prioritize reducing uncertainty (information gain) over selecting the most likely hypothesis.
- The canonical model enables consistent projections across UIs and prevents duplicated business logic in presentation layers.

## Lessons learned

- The startup composition root must be the single source of host wiring; avoid XAML StartupUri for DI-resolved windows.
- Early integration tests must account for project TFMs (WPF requires net*-windows) to avoid build-time reference failures.
- Small, disciplined evolutionary steps (Activity → Observation → Hypothesis → Evidence Request) keep the domain clean and reviewable.

## Recommended roadmap adjustments

- Prioritize ED-304 (Evidence) to capture evidence artifacts and their validation lifecycle.
- Add unit and integration tests for newly introduced domain models and WorkspaceState projections.
- Plan presentation-only acceptance tests to ensure UI projections remain read-only and faithful to Core state.

---

This retrospective will be stored alongside other architecture and process artifacts and should be updated for subsequent milestones.