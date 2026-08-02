# Copilot Instructions

## Project Guidelines
- Repository summary metrics are canonical engineering metrics and must not be derived or overwritten from legacy parsing, regexes, UI view models, or presentation-layer structures. New discovery should populate the canonical model only.

## Engineering Concepts
- CurrentTask should be treated as a foundational domain concept representing engineering intent, not as a UI-only feature. 
- Investigation remains the single source of engineering knowledge; CurrentTask must not own or duplicate Investigation data and should remain extensible for future Engineering Context work.