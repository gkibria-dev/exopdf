# Agentic Development Workflow

This document describes the end-to-end development workflow for this project.
It is written for two audiences: **human developers of any experience level** and
**AI coding agents** (such as Claude Code). Both should follow this workflow without
deviation.

The central principle: **documentation is written before code**. Requirements,
decisions, and plans are living artifacts in this repository — not afterthoughts.

---

## The workflow at a glance

```
branch → docs → plan → approve → implement → test → review → update architecture → commit → PR
```

Every step is required. Steps may loop (e.g. implement → review → implement) but
none are skipped.

---

## Step 1 — Create a working branch

All work happens on a dedicated branch. Never commit to `main` directly.

```bash
git checkout -b feature/<short-description>
git checkout -b fix/<short-description>
git checkout -b refactor/<short-description>
```

---

## Step 2 — Update documentation

Before any code is written, update or create the relevant documents. This is done
**conversationally** — discuss with the agent, let it draft, review and correct.

| Document | When to update |
|---|---|
| `docs/requirements.md` | Adding or changing any feature or operation |
| `docs/adr/NNN-title.md` | Any significant architectural or library decision |
| `docs/architecture.md` | If solution structure or layer responsibilities change |

**For bug fixes:** update `docs/requirements.md` only if the bug exposed a gap in
the specification (behaviour that was never defined). If the bug was simply a
wrong implementation of existing requirements, skip this step.

**For routine additions** that follow an established pattern (e.g. a new PDF
operation identical in structure to existing ones): no ADR is needed. Only write
an ADR when a future developer or agent would reasonably question *why* a
particular choice was made.

### Changing requirements mid-flight

Requirements evolve while work is in progress. Keep them traceable:

- Every requirement has a stable ID (for example `DUI-17`). IDs are never
  reused or renumbered.
- A requirement that is dropped is marked **Withdrawn**, not deleted, so plans,
  tests and PRs that cite it stay valid.
- Commit each logical requirements change on its own, with a message that names
  the IDs, for example `docs: DUI-17 promote reordering from Later to Should`.
- Review the rendered Markdown (editor preview or the pull request). Use
  `git log -p docs/requirements.md` to see how a requirement changed over time.
- If `docs/requirements.md` changes after a plan is approved, run
  `git diff <requirements-commit> -- docs/requirements.md` using the commit
  recorded in the plan, update the plan, and get it approved again before
  continuing.

---

## Step 3 — Create and approve a plan

Ask the agent to create an implementation plan. The plan is saved as a file:

```
docs/plans/YYYY-MM-DD-short-description.md
```

### Why plans are kept permanently

Plans are the primary record of **why something was implemented a certain way**.
They capture alternatives that were considered, trade-offs that were accepted, and
the reasoning behind the chosen approach. This context does not appear in code,
tests, commit messages, or ADRs. Without the plan, that reasoning is lost.

### Plan file template

```markdown
# Plan: <title>

**Date:** YYYY-MM-DD
**Branch:** <branch name>
**Related requirements:** <requirement IDs or section in requirements.md>
**Requirements commit:** <short hash of the last commit to docs/requirements.md>

## Goal
One sentence: what this plan achieves.

## Context
What drove this work. Any constraints, deadlines, or prior decisions that shaped
the approach.

## Alternatives considered
| Option | Reason rejected |
|--------|----------------|
| ...    | ...            |

## Chosen approach
Description of the approach and why it was selected over the alternatives.

## Steps
1. ...
2. ...

## Out of scope
What this plan explicitly does not cover.
```

### Approval gate

Review the plan. Request changes if needed. The agent updates the plan until it
is approved. **Do not proceed to Step 4 without explicit approval.**

---

## Step 4 — Implement

The agent implements according to the approved plan. Code review may happen
iteratively during implementation — this is expected and normal.

---

## Step 5 — Run tests

Run the full test suite. All tests must pass before proceeding.

```bash
dotnet test
```

If new behaviour was added, write tests for it before running the suite.

---

## Step 6 — Code review and security review

Run both reviews and address all findings before proceeding.

```
/review
/security-review
```

Iterate — fix findings, re-run — until both pass cleanly.

---

## Step 7 — Update architecture doc

If the implementation changed anything structural — project layout, dependency
direction, new layers, new patterns — update `docs/architecture.md` to reflect
the current state. The architecture doc must always describe the codebase as it
is now, not as it was.

If nothing structural changed, skip this step.

---

## Step 8 — Commit and open PR

The agent commits all changes (code, docs, plan) and opens a pull request. The PR
description must reference the plan file.

---

## Notes for AI agents

- At the start of every session, read `CLAUDE.md`, `docs/requirements.md`,
  `docs/architecture.md`, and any ADRs relevant to the task before asking
  clarifying questions. The answers are likely already there.
- The plan in `docs/plans/` for this branch is your implementation contract.
  Do not deviate from an approved plan without flagging it and getting approval.
- Do not commit directly to `main`.
- Do not proceed past Step 3 without explicit plan approval from the developer.
- When opening a PR, link to the plan file in the PR body.
- When in doubt about scope, consult `docs/requirements.md` — especially the
  **non-goals** section.
