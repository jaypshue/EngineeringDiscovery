EngineOS Screen & Interaction Model --- Draft 1

Purpose

This companion document describes the major EngineOS screens, theirpurpose, and how users move between them. The UX Contract describesproduct principles; this document describes the visible experience andinteractions.

1. High-Level Experience Map

Public Site

FREE RANGE → PROJECT DISCOVERY

CORPORATE → ANCHOR

Both ultimately enter the same engineering workflow:

Project Discovery ─┐ ├→ Engineering Model → Engineering Work Anchor────────────┘

The application itself should not reproduce the public-site choicescreen.

2. Public Home

Purpose

Explain EngineOS to a new visitor and provide enough context to choose astarting path.

Primary choices

Free Range --- "I want to build something new."

Leads toward Project Discovery.

Corporate --- "I already have software and want EngineOS tounderstand it."

Leads toward Anchor.

Boundary

This screen belongs to the public site. It should not be reused as theapplication's internal welcome screen.

3. Project Discovery

Purpose

Start an engineering effort from an idea.

Entry

Public Site → Free Range, or application launch with Project Discoveryselected.

First experience

The user should be able to say what they want to build. The experienceshould feel conversational rather than like filling out a project setupform.

Conceptual progression

Start Building → Project Discovery → Understand the idea → Establishinvestigation → Engineering Model → Engineering Work

The internal state machine may evolve without changing the basic userexperience.

4. Anchor

Purpose

Begin with existing software and give EngineOS enough evidence tounderstand it.

Entry

Public Site → Corporate, or application launch with Anchor selected.

First experience

Choose or drop a repository.

Show the selected repository path.

Show observed evidence.

Tell the user what EngineOS knows.

Make the next action obvious.

Repository selection

Supported interactions: Browse and Drag and drop.

The selected repository path should represent the user's actualselection as accurately as the host environment allows.

Evidence

Evidence should be presented as facts.

Examples: - repository folder exists - .git directory present - numberof .csproj files found - number of pom.xml files found - number ofGradle files found - solution files found

Avoid presenting a label such as ".NET repository" when the evidenceonly establishes that C# project files were found.

5. Anchor --- Repository States

Conceptual flow:

Empty → Repository Selected → Evidence Gathering → Evidence Available →Ready → Import → Anchored Repository

A failure may branch from selection or evidence gathering:

Repository Selected → Unable to analyze → Explain problem → Chooseanother repository

This describes the user-visible experience, not the internal statemachine.

6. Multiple Repositories

EngineOS may contain multiple imported repositories.

Importing another repository does not replace the previous repository.

Repository A Repository B Repository C

Each repository retains its own engineering context.

Repository selection and active-context UX are intentionally separateconcerns.

7. Resume Existing Work

If the user has already been working in EngineOS, startup should favorcontinuity.

Launch → Existing workspace? → Yes → Resume → Last useful engineeringcontext

If there is no existing workspace, the UX model does not mandate adefault entry mode.

Public-site choices map to explicit application entry intents:

Free Range → Project Discovery

Corporate → Anchor

A direct application launch with no persisted workspace and no explicitentry intent is undefined by this model and must not be assumed toimply a specific entry mode.

Hosts may provide explicit intent via navigation or configuration; theUX model does not invent a default.

8. Screen-to-Screen Rule

A navigation action should answer: "Why am I going here?"

Examples:

Start Building → Project Discovery

Import Repository → Anchor

Continue Existing Work → Existing engineering context

Do not route users through an intermediate welcome screen unless thatscreen has a specific product purpose.

9. Review Checklist

Before implementing a screen, a human reviewer should be able to answer:

What is this screen for?

Who is it for?

What action brought the user here?

What should the user do next?

What information does the user need?

What states can the screen represent?

Where does each major action go?

What should never appear here?

What existing workflow does this screen reuse?

If these questions cannot be answered, the screen is not sufficientlyspecified for implementation.

10. Relationship to Implementation

This model intentionally does not specify Razor component names, CSSclasses, JavaScript APIs, Blazor routing details, persistenceimplementation, service names, or exact pixel measurements.

Those are implementation decisions.

The UX model defines the intended experience. Implementation shouldconform to the model, not redefine the product accidentally.