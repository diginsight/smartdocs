---
title: "Content classification and the public/internal split"
description: "How authored content is separated into public and internal parts — the alias-first authoring model, the sibling .internal.md convention, the two-repository destination, internal completeness, and the repository-visibility conditional that switches the whole model off"
domain: "content-classification"
goal: "Let an author or an agent produce a document that is safe to publish and complete for an authorized reader, by deciding classification at the moment of writing rather than by redacting afterwards"
scope:
  covers:
    - "The four principles B, C, D and E and what each one is responsible for"
    - "The sensitive-material classes that trigger a split"
    - "How a public document refers to its internal companion"
    - "Path parity between a public repository and its internal peer"
    - "Why the split applies to intermediate folders such as _validation and _analysis"
    - "The repository-visibility conditional and its fail-safe default"
  excludes:
    - "The MUST/NEVER rules an artifact is held to (see content-classification.instructions.md)"
    - "The step-by-step split procedure (see .github/prompt-snippets/content-classification-and-split.md)"
    - "Evidence dossier structure for the autonomous streams (see 10.00-application-development/02-evidence-dossier-schema.md)"
    - "Runtime access control in the rendering application"
boundaries:
  - "NEVER write a sensitive value into a public document and plan to remove it later"
  - "NEVER treat `publish: false` as a disclosure control — it suppresses navigation, not access"
  - "NEVER let a failed read of the internal peer degrade into starting from scratch"
rationales:
  - "Redaction is a review step that runs after the value already exists in a file and in an editor's undo history; classification at authoring time removes the value's opportunity to exist"
  - "Role names keep a public document readable and reviewable, which opaque tokens do not — a reader can follow 'the docs app service' but not 'RESOURCE_1'"
  - "Path parity turns companion lookup into a name transformation, so no index can drift out of date"
  - "A private repository pays only cost for the split, so the model must be switchable rather than universal"
---

# Content classification and the public/internal split

## 🎯 What this model is for

A repository that is public will be read by people who were never considered when a document was written. The purpose of this model is to make that safe **without making the documentation worse** — because a document stripped of its specifics stops being useful, and a team that finds the rules unusable will route around them.

The model therefore does two jobs at once: it keeps identifying detail out of the public tree, and it keeps that detail available, in full, to a reader who is entitled to it.

---

## 🧱 The four principles

Each principle owns one failure and is useless alone. Together they are ordered from the strongest control to the weakest, and the weakest is deliberately last.

| | Principle | Owns | Failure it prevents |
|---|---|---|---|
| **D** | **Alias-first — public by construction** | how a public document is written | the sensitive value existing in a public file at all |
| **B** | **Sibling `.internal.md` plus gitignore** | how the two parts are told apart | a mis-targeted write being committed publicly |
| **C** | **Two-repository mirror** | where internal content lives | the internal part being exposed by the public repository |
| **E** | **Internal completeness** | what the internal part must contain | the split destroying the information it was meant to protect |

### D — alias-first

A public document names things by **role**: *the docs app service*, *the shared app subnet*, *the content storage account*. The identifying string is never typed into the public file.

Role names, not opaque tokens. `the docs app service` reads; `RESOURCE_1` does not. A document a reviewer cannot follow will not be reviewed properly, and an unreviewable document is its own risk.

The alternative — write the real value, then redact before publishing — is the trap this principle exists to prevent. By the time redaction runs, the value is in the file, in the editor's undo buffer, and often in a commit. Redaction converts a disclosure into a cleanup problem. Classification at authoring time means there is nothing to clean up.

> **The test that matters:** a factual correction to an internal identifier should require **no change to the public document**. If it does require one, the public document was carrying the identifier and principle D was not applied.

### B — sibling `.internal.md` plus gitignore

Internal content is written to a file named for its public sibling with `.internal.md` in place of `.md`, and the public repository's `.gitignore` excludes that pattern.

The filename is the classification signal because it **cannot be forgotten**. Frontmatter can be omitted; a name cannot. The same signal drives the ignore rule, the sync destination and — once it exists — the runtime access check, so local safety and published safety agree by construction rather than by discipline.

Non-Markdown internal assets cannot carry a suffix meaningfully. They go in an `_internal/` folder, which is ignored by the same rule set.

> **B is a backstop, not the control.** It catches a mistake; it does not prevent one. When B is what stopped a disclosure, D failed and that is worth noticing.

### C — two-repository mirror

Internal content is versioned in the paired private repository, at the **identical repository-relative path**, resolved through the declaration in `repository.metadata.yml`.

```text
public repo     src/docs/90.00-issues/202608/20260817.01-deployfail/03-incident-b.md
internal peer   src/docs/90.00-issues/202608/20260817.01-deployfail/03-incident-b.internal.md
```

Path parity is the whole economy of the model: a companion is found by transforming a filename, so there is no index to maintain and none to drift. It also means the two documents keep the same position in the reading order, which is what makes them usable as a pair.

Authoring still happens **in one place** — both files are written side by side in the public working tree, where the ignore rule keeps the internal one unversioned. The move to the peer is a separate, deterministic step.

### E — internal completeness

The internal companion is not a list of redactions. It is the document a colleague with full access would want: real names, real diagrams, the commands as executed, the verified state.

This principle exists because the other three are subtractive, and a purely subtractive process ends with information destroyed rather than relocated. E is what makes the split lossless.

A companion is complete when an authorized reader needs nothing else to act — in particular:

- the **as-built diagrams** with real identifiers, not only the aliased versions
- the **commands as executed**, including the ones that failed and why
- **verification state** with the date it was observed, so a later reader knows what to re-check
- the **corrections** to earlier internal statements, stated as corrections rather than silently applied

---

## 🔍 What triggers a split

The classification test is owned by 📖 `10.00-application-development/03-evidence-access-policy.md` and is reused here unchanged, so the autonomous streams and hand-written documents apply one taxonomy rather than two.

| Class | Examples |
|---|---|
| `credential` | keys, connection strings, tokens, certificates, any secret value or its distinguishing prefix |
| `personal-data` | anything identifying a person, including sample rows that happen to carry real values |
| `exploit-enabling` | an unpatched weakness stated precisely enough to be actionable by a reader |
| `internal-surface` | internal hostnames, private endpoints, management URLs, tenant or subscription identifiers, resource names and address ranges |

When in doubt, classify sensitive. An over-classified fact costs one redirection; an under-classified one is a disclosure.

Values listed under `deliberately_public` in `repository.metadata.yml` are exempt. Aliasing them costs readability and buys nothing.

---

## 🔗 How the public document points at its companion

A public document **may and should** state that a companion exists and what it adds. That is a signpost, not a disclosure: it reveals the existence of internal detail, which is already obvious from the aliasing, and it is what makes the pair usable.

What a public document must never do is **quote** internal content or reproduce an identifier in the act of pointing at it.

Until the rendering application enforces authorization, the pointer is written as a **backticked path rather than a Markdown link**, because the target does not resolve in the public repository and a link would fail every link check. Once access control exists and both parts are served from one content store, the pointer becomes a real link that is simply absent for an unauthorized reader.

---

## 📁 Intermediate folders are in scope

`_validation`, `_analysis`, `_evidence` and their peers are **versioned in the public repository**. A folder-name convention does not make a file unpublished, and `publish: false` does not make it unreachable — it suppresses navigation, not access.

These folders are in fact the more likely leak, because they hold raw material: captured output, screenshots that include a browser address bar, command transcripts, connection errors quoting a host.

The same four principles apply to every file in them, with one addition specific to captures: **an image cannot be aliased after the fact**. A screenshot showing an internal hostname must be re-captured or cropped, never published with the expectation that nobody will read it. Where the capture cannot be made safe, it belongs in `_internal/`.

---

## 🔀 The repository-visibility conditional

In a private repository this entire model is overhead. There is no public surface, so aliasing costs readability and buys nothing, and a second repository adds a synchronisation step protecting content from an audience that does not exist.

An artifact therefore reads `repository.metadata.yml` **first** and behaves accordingly:

| `visibility` | Behaviour |
|---|---|
| `public` | full model — classify, alias, split, write the companion to the peer |
| `private` | no split — real names, one document, no companion, no sync |
| absent or unrecognised | treat as **`public`** |

The default is the point. Guessing "private" wrongly discloses; guessing "public" wrongly costs some aliasing that was not needed. The asymmetry decides the default.

Opening a repository later is a deliberate piece of work, not a flag change. Flipping `visibility` to `public` does not retro-classify what is already there.

---

## ⚠️ What this model does not do

Stating the limits plainly, because each of these has been mistaken for protection:

- **`publish: false` is not access control.** It removes an item from navigation. Raw content endpoints serve it regardless.
- **A `.gitignore` rule is silent.** An ignored file never appears in `git status`, and `git clean -xdf` deletes it without warning. Internal drafts held only in a public working tree are one routine command from destruction — which is why the move to the peer must be a deliberate step with a verified outcome, not a habit.
- **Classification does not survive a copy.** Content pasted from an internal companion into a public document carries none of its protection.

---

## References

- **📖** `.github/instructions/content-classification.instructions.md` — the enforceable rules
- **📖** `.github/prompt-snippets/content-classification-and-split.md` — the procedure artifacts run
- **📖** `10.00-application-development/03-evidence-access-policy.md` — the sensitive-material taxonomy this file reuses
- **📖** `10.00-application-development/02-evidence-dossier-schema.md` — the equivalent split inside evidence dossiers
- **📖** `repository.metadata.yml` — visibility and internal-peer declaration for this repository
