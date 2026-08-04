# Canonical Engineering Model

This document summarizes the current canonical engineering model implemented in EngineOS through ED-303.

The implemented investigation workflow (current):

Engineering Workspace
↓
Engineering Activity
↓
Intent
↓
Observation
↓
Hypothesis
↓
Evidence Request

Notes
- The model above represents the current implementation and should not include future concepts such as Evidence, Recovered Understanding beyond simple strings, Recommendations, or AI-driven inference.
- EngineeringActivity owns the domain collections: Intent, Observations, HypothesisSpace, EvidenceRequests, and RecoveredUnderstanding (strings).
- Hypotheses are simple domain objects (EngineeringHypothesis) with Id, Description, Status (Active, Eliminated, Confirmed), Confidence, CreatedUtc, UpdatedUtc.
- Evidence Requests are simple domain objects (EngineeringEvidenceRequest) with Id, CreatedUtc, Target, Reason, ExpectedInformationGain, ExpectedConfidenceIncrease.
- Presentation projects workspace state via WorkspaceState and ViewModels; presentation does not own canonical state.

This document aligns with the current codebase and the completed foundation stories ED-300 through ED-303.