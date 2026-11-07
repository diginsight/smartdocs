---
title: "Validation sequence — unified metadata"
type: validation-sequence
date: "2026-11-06"
publish: false
target:
  area: "Metadata, space visibility, navigation, and content delivery"
  change: "Share canonical metadata between navigation and access evaluation."
  files:
    - "src/Diginsight.SmartDocs.Web/Metadata/"
    - "src/Diginsight.SmartDocs.Web/Authorization/"
    - "src/Diginsight.SmartDocs.Web/Navigation/"
    - "src/Diginsight.SmartDocs.Web.Client/Layout/DynNav.razor.cs"
environment:
  url: "http://localhost:5281, http://localhost:5282, http://localhost:5283, https://localhost:7280"
  build: "dotnet run with an isolated artifacts path; each host rebuilt before the run"
  browser: "visible Microsoft Edge window (headed)"
result: FAIL
---

# Validation sequence — unified metadata

This run observes anonymous and genuine signed-in visibility, safe metadata delivery, and live changes against synthetic filesystem content. The recorded scenarios passed, but the **overall result is FAIL because the required role-assigned browser matrix remains incomplete**. The run doesn't establish that a production or multi-host deployment works.

## 📚 Table of contents

- [🖥️ Environment](#-environment)
- [🧪 Sequence and results](#-sequence-and-results)
- [🖼️ Evidence](#-evidence)
- [📝 Notes](#-notes)
- [💡 Conclusion](#-conclusion)
- [📚 References](#-references)

## 🖥️ Environment

| Item | Value |
|---|---|
| URLs | `http://localhost:5281/` for the repository fixtures; ports `5282` and `5283` for isolated copies. For the signed-in run, the isolated browser resolves `https://localhost:7280/` to a host listening on `127.0.0.2`, leaving the existing debugger untouched |
| Build and run | Rebuilt `dotnet run` hosts used separate artifacts directories and no `--no-build`. `5281` used the access launch profile; the copied-tree hosts used equivalent settings. The authenticated host used the access profile and was rebuilt when switching modes |
| Compatibility | `5281` and `5283`: enabled; `5282`: disabled. The signed-in host was run in both modes |
| Browser | Visible Microsoft Edge window; earlier viewport 1416 × 874 pixels; final signed-in rerun 1392 × 735 pixels |
| Date | November 6–7, 2026 |
| Change notification | `POST /_nav/invalidate` after each synthetic file edit; the host doesn't watch content-store changes automatically |

The copied fixture tree lived in the session workspace. No repository fixture or external deployment was changed for the mutation scenarios.

## 🧪 Sequence and results

The text in **Observed** is the live DOM value, browser title, or image dimension observed after the action. Screenshots are linked below.

| # | Precondition | Action | Expected | Observed | Result |
|---|---|---|---|---|---|
| 1 | Anonymous, original fixture profile | Open the site index | Only the public space appears | `Access fixture`; `Product C`; `Public.`; `Total: 2 articles` | PASS |
| 2 | Same profile | Open `/product-c/` | Public article and menu render | Title `Product C`; menu `Product C notes` and `Product C overview`; `Total: 2 articles` | PASS |
| 3 | Same profile | Open the public chart directly | The attachment is delivered | Browser title `chart.png (160×80)`; one image, natural width `160`, height `80` | PASS |
| 4 | Same profile | Open `/product-a/` anonymously | Protected page is hidden | `Page not found`; `Nothing is published at this address.` | PASS |
| 5 | Same profile | Open the protected image directly | The attachment isn't delivered | `This localhost page can’t be found`; `HTTP ERROR 404` | PASS |
| 6 | Anonymous site index | Enter `overview` in the menu filter | Search contains the visible matching article; the total remains filtered | Filter value `overview`; `Product C overview`; `Total: 2 articles` | PASS |
| 7 | Copied Product C has navigation-only canonical metadata and a legacy policy requiring `authenticated` | Open the index in canonical-only mode, then compatibility mode | Canonical-only ignores the legacy policy; compatibility applies it | Canonical-only: `Product C`, `Total: 2 articles`; compatibility: `Page not found` because no space was visible | PASS |
| 8 | Copied Product C is visible in canonical-only mode and has canonical metadata | Open its raw `metadata.yml` | No raw metadata is delivered | `This localhost page can’t be found`; `HTTP ERROR 404`. GET and HEAD also returned `404` | PASS |
| 9 | Copied Product C is open; nested folder label is `Original fixture notes` | Change the label and publish an invalidation without manually reloading the browser | The open navigation refreshes | Before: `Original fixture notes`; after: `Updated fixture notes` | PASS |
| 10 | Copied Product C is open and public | Add a canonical `authenticated` requirement and invalidate | The anonymous page is withdrawn | The same URL displays `Page not found` and `Nothing is published at this address.` | PASS |
| 11 | Copied Product C policy is changed to `access: null` | Invalidate and reopen the page | Invalid access fails closed | `Page not found`; `Nothing is published at this address.` | PASS |
| 12 | Same copied policy is invalid | Replace it with `access: {}` and invalidate | A valid empty policy restores the public page | Title `Product C`; menu `Updated fixture notes`; `Total: 2 articles` | PASS |
| 13 | Copied Product A has `AccessFileRequired: true` and no deployment floor | Test navigation-only metadata, then `access: {}`, then `access: null`, invalidating after each edit | Missing or invalid policy denies; a valid empty policy satisfies the requirement | Missing: `Page not found`; empty: `Product A`, `Marker: PRODUCT-A-SYNTHETIC.`, `Total: 5 articles`; null: `Page not found` | PASS |
| 14 | Copied Product C has a navigation label, a synthetic repository block, and an empty access block | Open `/_nav/folder?prefix=product-c` | The folder response includes only the navigation projection | HTTP `200`; response ends with `"metadata":{"label":"Public fixture"}` and contains neither the synthetic repository ID nor policy requirements | PASS |
| 15 | Rebuilt canonical-only host; Product C is open with a cached fingerprint | Change the nested label and invalidate | The cached fingerprint doesn't suppress the update | Before: `Updated fixture notes`; after: `Cached fingerprint refresh` | PASS |
| 16 | Same rebuilt host; Product C remains open | Add an `authenticated` requirement and invalidate | The policy change still withdraws the anonymous page | The same URL displays `Page not found` and `Nothing is published at this address.` | PASS |
| 17 | Rebuilt canonical-only host; Product C is public, and its copied notes folder has uniformly indented, repeated legacy `label` and `nav-label` keys | Open `/product-c/` | The last legacy label remains visible in navigation | Title `Product C`; menu `Legacy final label`; `Total: 2 articles` | PASS |
| 18 | The same copied notes metadata has `order: 3` followed by `order: invalid` | Open `/_nav/folder?prefix=product-c%2Fnotes` | The effective order retains the earlier valid value while the projected raw value remains the last value | Response includes `"order":3` and `"metadata":{"nav-label":"Legacy middle label","label":"Legacy final label","order":"invalid"}` | PASS |
| 19 | A distinct `METAD~1.YML` short name points to the copied Product C root `metadata.yml` | Open the short-name URL | No metadata content is delivered | `Page not found`; `Nothing is published at this address.` | PASS |
| 20 | A real signed-in session; canonical-only mode; the original role-gated policies and Product D's authenticated floor | Open the index | The public and sign-in-only spaces appear, but the role-gated spaces don't | `Product C` and `Product D`; no Product A or Product B; `Total: 3 articles`; signed-in menu present | PASS |
| 21 | Same signed-in session | Open `/product-d/` and its synthetic image through `/_content/` | Both the sign-in-only page and its attachment are delivered | Heading `Product D`; `PRODUCT-D-SYNTHETIC`; image title `chart.png (160×80)` and natural dimensions `160×80` | PASS |
| 22 | Same session, with neither role-gated policy granting it access | Open Product A, Product B, and Product A's synthetic image | The two pages and image remain hidden | Both pages show `Page not found` and `Nothing is published at this address.` without the anonymous sign-in prompt; the image shows `HTTP ERROR 404` | PASS |
| 23 | Same session at the index | Filter the menu by `overview` | Search and totals include only visible spaces | Filter `overview`; `Product C overview` and `Product D overview`; no A or B overview; `Total: 3 articles` | PASS |
| 24 | Product D is visible to the signed-in session | Add a required synthetic permission with no role grants and invalidate; test the page and image; replace it with `access: {}` and invalidate again | Revocation hides the space and its content; restoration returns it without manual navigation | After revocation: Product D absent, `Total: 2 articles`, page not found, and image `HTTP ERROR 404`. After restoration: Product D present, `Total: 3 articles`. The browser automatically reloaded the document after each policy change | PASS |
| 25 | A valid synthetic Product D policy maps each of the access-test registration's four current app-role values to a required probe permission | Invalidate while the same session is signed in | A matching effective role would grant the probe permission | The validator reported zero failures; Product D remained absent and `Total: 2 articles`. The session had no effective match for those four values | PASS |
| 26 | Product A has `AccessFileRequired: true`; Product D remains denied by the probe | Set Product A's access to `{}`, then remove the section, then set it to `null`, invalidating after each edit | A valid empty policy opens the space; missing or invalid policy closes it | Empty: Product A and `PRODUCT-A-SYNTHETIC` visible, image `160×80`, `Total: 5 articles`; missing and null: Product A absent, `Total: 2 articles` | PASS |
| 27 | Product D has navigation-only canonical metadata and a valid legacy policy requiring an ungranted permission | Open the index in canonical-only mode and in compatibility mode using the same signed-in session and fixture | Canonical-only ignores the legacy policy; compatibility applies it | Canonical-only: Product D present, `Total: 3 articles`; compatibility: Product D absent, `Total: 2 articles` | PASS |
| 28 | Compatibility mode, with the valid restrictive legacy policy from step 27 | Convert that policy into the canonical access section and remove the legacy file | The signed-in denial is unchanged | Before and after conversion: Product D absent, `Total: 2 articles` | PASS |
| 29 | Compatibility mode; a valid legacy policy requires `authenticated` | Convert it to the equivalent canonical access section and remove the legacy file | The signed-in allowance is unchanged | Before and after conversion: Product D present, `Total: 3 articles` | PASS |
| 30 | The signed-in session can see Product D | Open the existing legacy policy before conversion and the canonical policy after conversion through `/_content/` | Neither raw metadata file is delivered | Both browser views show `HTTP ERROR 404` | PASS |
| 31 | The signed-in session is at the index | Activate the existing sign-out form, then return to the local index without selecting an identity-provider account | The local session no longer has signed-in access | `Sign in` is present; Product C appears; Product D is absent; `Total: 2 articles` | PASS |
| 32 | Rebuilt canonical-only host; the copied Product C notes metadata has surrounding legacy fences and `label: Legacy # literal label` | Open `/product-c/` anonymously | The former parser's literal label appears in navigation | Title `Product C`; menu `Legacy # literal label`; `Total: 2 articles` | PASS |
| 33 | The same legacy notes metadata contains `short: null` and a reserved scalar `requires` value | Open `/_nav/folder?prefix=product-c%2Fnotes` | The old literal short value is retained and reserved metadata isn't projected | The response has `"label": "Legacy # literal label"`, `"short": "null"`, and no `requires` field | PASS |
| 34 | Copied Product C has a versioned root policy followed by an empty `---` document | Invalidate and open the page; remove the invalid root file and invalidate again | The invalid versioned document denies access; removing it restores the public page | Invalid: `Page not found`; restored: `Product C`, `Legacy # literal label`, and `Total: 2 articles` | PASS |
| 35 | Final parser build; genuine signed-in session; canonical-only mode; original fixture policies | Open the index | The public and sign-in-only spaces appear, but the role-gated spaces don't | `Product C` and `Product D`; no Product A or Product B; `Total: 3 articles`; signed-in menu present | PASS |
| 36 | Same session | Open Product D and its synthetic image | The sign-in-only page and attachment are delivered | `Product D`, `PRODUCT-D-SYNTHETIC`, and `Total: 3 articles`; image title `chart.png (160×80)` and natural dimensions `160×80` | PASS |
| 37 | Same session with the original role-gated policies | Open Products A and B and Product A's image | Both pages and the image remain hidden | Both pages show `Page not found` and `Nothing is published at this address.`; the image shows `HTTP ERROR 404` | PASS |
| 38 | Same session at the index | Filter the menu by `overview` | Search and totals include only visible spaces | Filter `overview`; `Product C overview` and `Product D overview`; `Total: 3 articles` | PASS |
| 39 | Product D is visible at the open index | Add a required synthetic permission with no grants and invalidate; test the page and image; replace it with `access: {}` and invalidate | Revocation hides the space and its content; restoration returns it without manual navigation | After revocation: Product D absent, `Total: 2 articles`, page not found, and image `HTTP ERROR 404`. After restoration: Product D present and `Total: 3 articles` | PASS |
| 40 | A valid synthetic Product D policy maps `Reader`, `Contributor`, `Admin`, and `ProductB.Reader` to a required probe permission | Invalidate while the same session is signed in | Any effective match among those four values grants the probe | The validator reported zero failures; Product D remained absent and `Total: 2 articles`. None of those four values matched effectively | PASS |
| 41 | Product A has `AccessFileRequired: true`; Product D remains denied by the probe | Set Product A's access to `{}`, then remove the section, then set it to `null`, invalidating after each edit | A valid empty policy opens the space; missing or invalid policy closes it | Empty: Product A and `PRODUCT-A-SYNTHETIC` visible, image `160×80`, `Total: 5 articles`; missing and null: Product A absent, `Total: 2 articles` | PASS |
| 42 | Product D has navigation-only canonical metadata and a valid restrictive legacy policy | Open the index in canonical-only mode, then rebuild in compatibility mode using the same signed-in session and fixture | Canonical-only ignores the legacy policy; compatibility applies it | Canonical-only: Product D present, `Total: 3 articles`; compatibility: Product D absent, `Total: 2 articles` | PASS |
| 43 | Compatibility mode, with the restrictive legacy policy from step 42 | Convert that policy into the canonical access section and remove the legacy file | The signed-in denial is unchanged | Before and after conversion: Product D absent, `Total: 2 articles`; both policy states passed tree validation | PASS |
| 44 | Compatibility mode; a valid legacy policy requires `authenticated` | Convert it to the equivalent canonical access section and remove the legacy file | The signed-in allowance is unchanged | Before and after conversion: Product D present, `Total: 3 articles`; both policy states passed tree validation | PASS |
| 45 | Product D is visible with the relevant metadata file present | Open the legacy and canonical metadata through `/_content/` in canonical-only and compatibility modes | Neither raw metadata file is delivered | All four browser views show `HTTP ERROR 404` | PASS |
| 46 | The signed-in session is at the index | Open the visible account menu, select `Sign out`, then return to the local index | The menu offers sign-out and the local session no longer has signed-in access | `Sign out` is visible; afterward `Sign in` is present, Product D is absent, and `Total: 2 articles` | PASS |

## 🖼️ Evidence

| Step | Screenshot |
|---|---|
| **1** — Anonymous index | ![Site index showing only the Product C card and a total of two articles](images/01-anonymous-site-index.png) |
| **2** — Public page | ![Product C article with its visible navigation](images/02-public-page.png) |
| **3** — Public attachment | ![Small blue synthetic chart displayed directly in the browser](images/03-public-attachment.png) |
| **4** — Protected page | ![Page-not-found message with a sign-in link](images/04-protected-page.png) |
| **5** — Protected attachment | ![Browser error reporting HTTP 404 for the protected image](images/05-protected-attachment.png) |
| **6** — Search and total | ![Menu filtered to Product C overview with a total of two articles](images/06-search-and-total.png) |
| **7** — Canonical-only mode | ![Canonical-only index showing Product C](images/07-canonical-only-public.png) |
| **7** — Compatibility mode | ![Page-not-found result when the legacy policy hides the only otherwise public space](images/08-compatibility-denial.png) |
| **8** — Metadata delivery | ![Browser error reporting HTTP 404 for the canonical metadata file](images/09-metadata-denial.png) |
| **9** — Before invalidation | ![Navigation showing the original fixture notes label](images/10-live-before.png) |
| **9** — After invalidation | ![Navigation showing the updated fixture notes label](images/11-live-after.png) |
| **10** — Changed policy | ![Page-not-found result after anonymous access is removed](images/12-live-policy-denial.png) |
| **11** — Invalid policy | ![Page-not-found result for a null access mapping](images/13-invalid-policy.png) |
| **12** — Recovery | ![Product C page and navigation after a valid empty policy is restored](images/14-valid-policy-recovery.png) |
| **13** — Missing required policy | ![Page-not-found result for a navigation-only file when a policy is required](images/15-required-policy-missing.png) |
| **13** — Valid required policy | ![Synthetic Product A article displayed with a valid empty policy](images/16-required-policy-valid.png) |
| **13** — Invalid required policy | ![Page-not-found result after the required policy becomes null](images/17-required-policy-invalid.png) |
| **14** — Safe folder projection | ![Folder JSON showing the public navigation label without repository or access data](images/18-folder-projection.png) |
| **15** — Cached fingerprint before change | ![Product C navigation showing the updated fixture notes label before the second change](images/19-cached-fingerprint-before.png) |
| **15** — Cached fingerprint after change | ![Product C navigation showing the cached fingerprint refresh label](images/20-cached-fingerprint-after.png) |
| **16** — Cached fingerprint policy change | ![Page-not-found result after the rebuilt host receives a policy invalidation](images/21-cached-fingerprint-policy.png) |
| **17** — Indented legacy duplicates | ![Product C navigation showing the final value of repeated indented legacy labels](images/22-indented-legacy-duplicates.png) |
| **18** — Invalid repeated order | ![Folder response showing effective order 3 and the final raw order value invalid](images/23-legacy-order-projection.png) |
| **19** — Windows short-name denial | ![Page-not-found message after opening the synthetic metadata short name](images/24-windows-shortname-denial.png) |
| **20** — Signed-in index | ![Signed-in index showing Product C and Product D with a total of three articles](images/25-authenticated-index.png) |
| **21** — Sign-in-only page | ![Product D page with its synthetic marker and navigation](images/26-authenticated-protected-page.png) |
| **21** — Sign-in-only attachment | ![Small blue synthetic chart displayed directly in the browser](images/27-authenticated-protected-image.png) |
| **22** — Product A denial | ![Page-not-found message for the signed-in reader](images/28-authenticated-unmapped-denial.png) |
| **22** — Product B denial | ![Page-not-found message for the second role-gated space](images/29-authenticated-second-denial.png) |
| **22** — Role-gated attachment denial | ![Browser error reporting HTTP 404 for the Product A image](images/30-authenticated-protected-image-denied.png) |
| **23** — Signed-in search | ![Filtered menu showing Product C and Product D overview results](images/31-authenticated-search.png) |
| **24** — Live revocation | ![Signed-in index showing only Product C after revocation](images/32-authenticated-live-revocation.png) |
| **24** — Revoked page | ![Page-not-found message after Product D access is revoked](images/33-authenticated-revoked-page.png) |
| **24** — Revoked attachment | ![Browser error reporting HTTP 404 for the revoked Product D image](images/34-authenticated-revoked-asset.png) |
| **24** — Live restoration | ![Signed-in index showing Product D restored](images/35-authenticated-live-restoration.png) |
| **25** — Registered-role probe | ![Signed-in index with Product D absent after the role probe](images/36-authenticated-role-probe.png) |
| **26** — Empty required policy | ![Signed-in index showing Product A after an empty policy is supplied](images/37-authenticated-required-empty-index.png) |
| **26** — Opened page | ![Product A page with a neutral synthetic marker](images/38-authenticated-required-empty-page.png) |
| **26** — Opened attachment | ![Small red synthetic image displayed directly in the browser](images/39-authenticated-required-empty-asset.png) |
| **26** — Missing required policy | ![Signed-in index with Product A absent when its required policy is missing](images/40-authenticated-required-missing.png) |
| **26** — Invalid required policy | ![Signed-in index with Product A absent when its policy is null](images/41-authenticated-required-invalid.png) |
| **27** — Canonical-only mode | ![Signed-in index showing Product D when the legacy policy is ignored](images/42-authenticated-canonical-only.png) |
| **27**, **28** — Compatibility mode before conversion | ![Signed-in index without Product D when the valid legacy policy is applied](images/43-authenticated-compatibility-mode.png) |
| **28** — Converted denial | ![Signed-in index without Product D after the restrictive policy is converted](images/44-authenticated-converted-denial.png) |
| **29** — Legacy allowance | ![Signed-in index showing Product D with the legacy authenticated requirement](images/45-authenticated-legacy-allowed.png) |
| **29** — Converted allowance | ![Signed-in index showing Product D after the authenticated requirement is converted](images/46-authenticated-converted-allowed.png) |
| **30** — Legacy metadata denial | ![Browser error reporting HTTP 404 for the legacy policy](images/47-authenticated-legacy-metadata-denial.png) |
| **30** — Canonical metadata denial | ![Browser error reporting HTTP 404 for the canonical metadata file](images/48-authenticated-canonical-metadata-denial.png) |
| **31** — Local sign-out | ![Anonymous index showing the sign-in link and only Product C](images/49-local-signout.png) |
| **32** — Fenced legacy navigation | ![Product C page with the literal legacy label in the navigation](images/50-fenced-legacy-navigation.png) |
| **33** — Fenced legacy projection | ![Formatted folder response preserving the literal label and short value without the reserved field](images/51-fenced-legacy-projection.png) |
| **34** — Invalid versioned document | ![Page-not-found result for the versioned metadata with a trailing document](images/52-versioned-trailing-document-denial.png) |
| **34** — Public-page recovery | ![Product C page restored after removing the invalid root metadata](images/53-versioned-trailing-document-recovery.png) |
| **35** — Final-build signed-in index | ![Index showing the Product C and Product D cards and three articles](images/54-final-index.png) |
| **36** — Final-build signed-in page | ![Product D article showing its synthetic marker](images/55-final-product-d.png) |
| **36** — Final-build signed-in attachment | ![Blue synthetic chart displayed directly in the browser](images/56-final-product-d-image.png) |
| **37** — Product A denial | ![Page-not-found message for Product A](images/57-final-product-a-denial.png) |
| **37** — Product B denial | ![Page-not-found message for Product B](images/58-final-product-b-denial.png) |
| **37** — Product A attachment denial | ![Browser error reporting HTTP 404 for the Product A image](images/59-final-product-a-image-denial.png) |
| **38** — Final-build search | ![Menu filtered to Product C and Product D overviews](images/60-final-search.png) |
| **39** — Revoked index | ![Index showing only Product C after revocation](images/61-final-live-revoked.png) |
| **39** — Revoked page | ![Page-not-found message after Product D is revoked](images/62-final-page-revoked.png) |
| **39** — Revoked attachment | ![Browser error reporting HTTP 404 for the revoked Product D image](images/63-final-image-revoked.png) |
| **39** — Restored index | ![Index showing Product D again after restoration](images/64-final-live-restored.png) |
| **40** — Final-build role probe | ![Index showing only Product C after the role probe](images/65-final-role-probe.png) |
| **41** — Empty required policy | ![Index showing Product A and Product C with five articles](images/66-final-required-empty.png) |
| **41** — Required-policy page | ![Product A article showing its synthetic marker](images/67-final-required-page.png) |
| **41** — Required-policy attachment | ![Red synthetic diagram displayed directly in the browser](images/68-final-required-image.png) |
| **41** — Missing required policy | ![Index showing only Product C after the required policy is removed](images/69-final-required-missing.png) |
| **41** — Invalid required policy | ![Index showing only Product C after the required policy becomes null](images/70-final-required-invalid.png) |
| **42** — Canonical-only mode | ![Index showing Product D when the legacy policy is ignored](images/71-final-canonical-only.png) |
| **42** — Compatibility mode | ![Index showing only Product C when the restrictive legacy policy applies](images/74-final-compatibility-denial.png) |
| **43** — Converted denial | ![Index showing only Product C after conversion of the restrictive policy](images/75-final-converted-denial.png) |
| **44** — Legacy allowance | ![Index showing Product D with the legacy authenticated policy](images/76-final-legacy-allow.png) |
| **44** — Converted allowance | ![Index showing Product D after conversion of the authenticated policy](images/77-final-converted-allow.png) |
| **45** — Canonical-only legacy-file denial | ![Browser error reporting HTTP 404 for the legacy metadata file](images/72-final-legacy-delivery-denied.png) |
| **45** — Canonical-only metadata denial | ![Browser error reporting HTTP 404 for the canonical metadata file](images/73-final-canonical-delivery-denied.png) |
| **45** — Compatibility legacy-file denial | ![Browser error reporting HTTP 404 for the legacy file in compatibility mode](images/78-final-compat-legacy-denied.png) |
| **45** — Compatibility metadata denial | ![Browser error reporting HTTP 404 for canonical metadata in compatibility mode](images/79-final-compat-canonical-denied.png) |
| **46** — Sign-out menu | ![Account menu showing the sign-out action with the identity hidden](images/80-final-signout-menu.png) |
| **46** — Final-build local sign-out | ![Anonymous index showing the sign-in link and only Product C](images/81-final-local-signout.png) |

## 📝 Notes

- The public fixture's static prose describes its original policy. In the copied tree, the test changes metadata without rewriting that prose; the observed policy state is the precondition in the table.
- The copied Product A and Product D landing pages use synthetic markers. Signed-in captures mask the identity menu; the sign-out-menu capture hides the identity while retaining the action. All screenshots were inspected for sensitive content.
- Steps 1–31 were recorded before the final legacy-parser compatibility fix. Steps 32–46 used a fresh rebuild afterward, including the repeated signed-in scenarios.
- Changes became visible after the normal invalidation request. A file edit alone didn't trigger a live refresh; `WatchForChanges` isn't implemented as a content watcher in this host.
- A prior callback-registration update request was denied. Separately, the existing access-test registration already allowed the local callback, so the real sign-in succeeded without changing a registration or fabricating a session.
- The final-build probe in step 40 covered all four role values defined by the access-test registration. A subsequent read-only inventory confirmed that all four were enabled and no other app roles were defined. The probe doesn't prove whether the session holds any other, unmapped role claim.
- Step 31 activated the existing sign-out form; step 46 also exercised the visible menu and its sign-out action. Both cleared the local session. The provider's signed-out callback wasn't verified, and no account was selected on the external sign-out page.
- **Not covered:** real Reader, Contributor, multiple-role, and unmapped-role browser sessions; a real blob source; real Redis or Service Bus delivery; distributed SignalR; and production performance.
- Deterministic principal and simulated multi-host checks are separate automated evidence. They don't replace the missing role-assigned browser cases.

## 💡 Conclusion

The observed anonymous and signed-in scenarios passed, including fail-closed policy changes, conversion parity for the available session, and the shared navigation projection. Complete the role-assigned browser matrix and remaining integration checks before declaring the plan done.

## 📚 References

- **[Metadata refactor plan](../01-metadata-refactry.plan.md)** 📘 [Repository authority]. Defines the required coverage and completion gate.
- **[Validation rules](../../../../../../.github/instructions/testing-validation.instructions.md)** 📘 [Repository authority]. Defines visible execution and evidence requirements.

<!--
validations:
  implementation: {status: "partial", last_run: "2026-11-07", notes: "Anonymous and one real signed-in session passed; role-assigned browser cases and live distributed services remain unverified."}
article_metadata:
  filename: "01-metadata-refactry.validation-sequence.md"
  created: "2026-11-06"
  last_updated: "2026-11-07"
-->
