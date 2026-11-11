---
title: "Other signals — authentication for custom documentation profiles"
date: "2026-11-10"
categories: [signals, authentication]
description: "The less defined remainder of activities this work item surfaced outside its goals."
publish: false
---

# Other signals — authentication for custom documentation profiles

These activities surfaced in this work item but were **never in scope** for its goals. They're less defined than those on the [signals page](04-signals.md): each is low relevance with an open landing. Each record is self-contained, so someone reading it without this conversation can act on it as written.

📖 Record shape, kinds, sweep, and priority derivation: [signal-capture](../../../../../.github/skills/signal-capture/SKILL.md)

## 📡 Signals

This signal was added on November 10, 2026, while investigating the [additional-fix plan](04-smartdocs-rebac-metadata-additionalfix.plan.md). That plan's goal is RBAC conformance across every source a SmartDocs configuration profile reads.

| Order | Id | Kind | Relevance | Actionability | Target | Existing landing | State |
|---|---|---|---|---|---|---|---|
| 1 | `SIG-6-stage-static-host` | `divergent-commitment` | low | open | The device-API stage environment | none found | `pending` |

### `SIG-6-stage-static-host` — a static documentation host shares the devec4 reader registration outside any SmartDocs configuration

- **Kind** — `divergent-commitment`.
- **Goal** — every host that signs readers in through the devec4 reader registration is either a declared SmartDocs deployment with an overlay, or deliberately documented as a separate site with its own access rule.
- **Scope** — on November 10, 2026, the registration listed sign-in and sign-out callbacks for a web app in the device-API stage environment. That app isn't a SmartDocs host. Its site root holds static documentation folders for the device-API, dispatcher, desktop-client, event-adapter, model-editor, and telemetry-gateway repositories. It also holds a folder whose name marks it as the desktop-client documentation's internal companion. App Service Authentication guards the whole site, and an anonymous request is refused with HTTP 401. Its latest deployment was on November 4, 2026. No SmartDocs overlay, script, or workflow references it.
- **Why it matters** — SmartDocs per-space policies don't apply there. Any reader the platform admits sees every folder, the internal one included. Its content and access rule can drift from the SmartDocs spaces it duplicates, unnoticed, because no SmartDocs check reaches it.
- **Target** — the owner of the device-API stage environment. The host and its folders are named in the devec4 alias registry.
- **Existing landing** — none found.
- **State** — `pending`.
- **Relevance** — `low`. Platform authentication still keeps anonymous readers out, so nothing degrades while it waits.
- **Actionability** — `open`. The owner must first decide whether the site should keep running, become a SmartDocs space, or carry its own documented access rule.

## 📚 References

- **[Additional-fix plan](04-smartdocs-rebac-metadata-additionalfix.plan.md)** 📘 [Repository documentation]. The investigation that surfaced this signal.
- **[Signals](04-signals.md)** 📘 [Repository documentation]. The primary page, holding the relevant or actionable signals of this work item.

### Internal sources

Not resolvable from this repository; listed for provenance.

- **`src/docs/_aliases/devec4.aliases.internal.md`** — resolves *the stage static documentation host* and the devec4 role names used above.

<!--
article_metadata:
  filename: "05-other-signals.md"
  created: "2026-11-10"
  last_updated: "2026-11-10"
-->
