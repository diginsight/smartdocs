# Content classification and split — procedure

> Included by documentation and issue-analysis artifacts. Run this **before** writing any document.
> Rules: 📖 `.github/instructions/content-classification.instructions.md` · Model: 📖 `.copilot/context/05.00-content-classification/00-classification-and-split-model.md`

## Step 1 — resolve the repository declaration

Read `repository.metadata.yml` at the repository root.

| Outcome | Action |
|---|---|
| `visibility: private` | **Stop here.** Write one document with real names. No companion, no sync. |
| `visibility: public` | Continue to step 2. |
| file absent, or field missing/unrecognised | Treat as **public**. Continue to step 2. |

## Step 2 — resolve the internal peer

Resolve `internal_peer` in this order, first hit wins:

1. the environment variable named by `path_env`, when set
2. `default_relative_path`, resolved from the repository root

Verify the resolved path exists and is a git repository.

**If it cannot be resolved and `on_missing: fail-closed`: stop and report.** Do not continue into the public document — a document written now will either strand internal facts publicly or silently lose them.

## Step 3 — fetch any existing companion

For each document you are about to modify, compute the companion path by parity:

```text
<repo-relative path of the public file>, with .md replaced by .internal.md
```

Look for it under the internal peer at that exact path.

- **Found** — read it. It is the base you extend, not something to recreate.
- **Not found** — proceed; you are creating the first companion.
- **Fetch failed** (no access, unreadable, peer unresolved) — **abort**. Never treat a failed read as "not found": recreating from scratch and syncing will overwrite a richer original.
- **Local and remote both exist and differ** — stop and surface the divergence. Do not overwrite.

## Step 4 — classify every fact before writing it

For each fact you intend to record, test it against the four classes:

| Class | Trigger |
|---|---|
| `credential` | any secret value, or a prefix distinctive enough to identify one |
| `personal-data` | anything identifying a person, including real values in sample data |
| `exploit-enabling` | a weakness stated precisely enough for a reader to act on |
| `internal-surface` | internal hostnames, private endpoints, management URLs, tenant/subscription identifiers, resource names, address ranges |

Exempt: anything listed under `deliberately_public` in `repository.metadata.yml`.

**When in doubt, classify sensitive.**

For every sensitive fact, choose the **role name** the public document will use — readable (`the docs app service`), never an opaque token (`RESOURCE_1`). Record the role-to-identifier mapping in the registry named by `internal_peer.alias_registry` in `repository.metadata.yml`, **before** using the alias publicly.

## Step 5 — write the internal companion first

Order is not stylistic. A failure between the two writes must never leave internal facts in the public tree.

The companion must be **complete**, not a list of redactions:

- real identifiers and the **as-built diagrams** with real names
- the **commands as executed**, including those that failed and why
- **verified state** with the date observed, so a later reader knows what to re-check
- **corrections** to earlier internal statements, stated as corrections

Frontmatter: `classification: internal` and `publish: false`.

## Step 6 — write the public document

Use only role names.

**Put the companion pointer in the References section, at the end.** Never in a heading, the table of contents, or ahead of the substantive content. Most readers cannot open the peer; for them an early pointer is a closed door that makes the document read as an abridgement of something held elsewhere. It is provenance, like a source citation, so it goes where provenance goes — as a **backticked path, not a link**:

```markdown
### Internal sources

Not resolvable from this repository; listed for provenance.

- **`<same path>/<name>.internal.md`** — <what it adds>
- **`<alias registry path>`** — resolves the role names used above
```

In the body, add **at most one short note**, and only where withholding changes how a passage reads — typically that resources are named by role, so the reader knows the convention rather than suspecting an omission. It must not name the peer, repeat the path, or list companions: none of that is actionable for a reader without access.

Where several pages have companions, put the per-page table in the References section of the **entry-point page only**.

**Reader-independence test.** Read the finished page as someone with no peer access. It must be a complete account, not a document that keeps gesturing at what it is not saying.

## Step 7 — verify before declaring done

- [ ] Scan every public file changed for each sensitive class — expected result **zero**
- [ ] Companion exists at the path-parallel location and is complete
- [ ] Every alias used publicly resolves in the registry
- [ ] Captured images checked for in-image disclosure (address bars, window titles, terminal prompts)
- [ ] Companion pointer present, formatted as a backticked path, and located **in the References section** — not in a heading, the table of contents, or before the substantive content
- [ ] **Reader independence**: read straight through by someone with no peer access, the public document is a complete account
- [ ] Public document survives the correction test: *would fixing an internal identifier require editing this public file?* If yes, an identifier leaked into it.

## Step 8 — report the split

State plainly: which files are public, which are internal, where the internal ones were written, and any fact you classified sensitive that the reader might have expected to find publicly.
