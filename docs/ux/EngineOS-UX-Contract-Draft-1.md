EngineOS UX Contract --- Draft 1

Purpose

This is the human-readable product UX contract for EngineOS. It definesthe product boundary, major entry modes, startup behavior, and UIprinciples. It is intentionally not an implementation specification.

1. Product Boundary

EngineOS has two distinct surfaces.

Public Site

The public site explains EngineOS and helps a new visitor understand theproduct. It may contain Home, Vision, Corporate, Free Range, Learn, andRoadmap.

Application

The application is the actual engineering workspace. It is not a secondmarketing site and should not contain a duplicate welcome page orproduct explanation.

Principle: The public site explains EngineOS. The application is wherethe user works.

2. Two Entry Modes

Project Discovery Mode

User intent: "I want to build something."

Public-site entry: Free Range

Application entry: Start Building

Initial experience: a conversation-led project discovery workflow.

Anchor Mode

User intent: "I want EngineOS to understand this software."

Public-site entry: Corporate

Application entry: Import Repository

Initial experience: repository selection and evidence gathering.

3. Important Product Principle

Corporate and Free Range are not application identities. They describetwo common ways users enter EngineOS.

A corporate developer can use Project Discovery. A solo developer canuse Anchor.

Once inside the application, both become the same EngineOS engineeringworkflow.

4. Application Startup

The application should behave like a professional desktop engineeringtool.

It should not ask "What is EngineOS?" and should not show a marketingwelcome page.

Conceptually:

New Project Discovery request → Project Discovery Mode

New Anchor request → Anchor Mode

Existing workspace → Resume existing workspace

Public-site entry mapping should be preserved: Free Range → ProjectDiscovery intent; Corporate → Anchor intent.

Direct application launch (/app) with no persisted workspace and noexplicit entry intent is undefined by this contract.

The product must not invent a default entry mode for that undefinedlaunch; behavior should be determined only by an explicit host-providedintent or configuration.

The application should eventually reopen where the user left off.

5. Shared Workflow

Project Discovery and Anchor are two entry paths into one engineeringsystem, not separate products.

Project Discovery → Engineering Model → Engineering Work

Anchor → Engineering Model → Engineering Work

The exact convergence point can evolve without changing the basic userexperience.

6. Screen Design Principles

Every screen should have one primary job.

A screen should make the user's next useful action obvious.

Do not add UI merely because the application has an underlyingcapability. Do not duplicate a workflow in multiple places. Prefer clearpurpose, obvious next action, visible current state, minimal competingcontrols, and conversation when conversation is the appropriateinteraction.

7. UI Invariants

The public site and the application are different experiences.

The application is the engineering workspace, not a marketing page.

Project Discovery and Anchor are entry modes into the same product.

Corporate and Free Range are not application identities.

Existing repository import is a first-class workflow.

Importing a repository is append-only; an import must not silentlyreplace an existing repository.

A selected repository path should be preserved accurately.

Observed repository facts must not be presented as strongerconclusions than the evidence supports.

Existing workspace state should take precedence over a genericstartup screen.

A user should not need to understand EngineOS's internalarchitecture to begin working.

8. Design Review Rule

When implementation conflicts with this contract, do not automaticallypreserve the existing UI.

First identify whether the implementation is wrong, the UX contract iswrong, or the requirement is ambiguous.

Existing UI is evidence of what currently exists. It is notautomatically evidence of what the product should be.