# AraIa Development Standards

**Owner:** Business Platform Core repository. **Version:** 1.4. **State:** approved as a documentation standard; shared runtime library/component release is not thereby approved or implied. **Origin:** corporate directives “Development Standards + POL-DOC-001” (2026-11-06), “Diretriz transversal de padronização numérica” (2026-11-08), “Consolidação Funcional e Visual” (2026-11-09), and “Consolidação documental do modelo de acesso único” (2026-11-16).

This catalog defines cross-product interaction, numeric presentation, and documentation expectations. It records what is a standard, what has an implementation, and what has a distributable reusable component. It does not replace product requirements, ADRs, entitlement decisions, accessibility acceptance, or release gates. Rule IDs below are stable; unless a row says otherwise, each is approved as a documented standard, specification version 1.0, originated by the corporate directive dated 2026-11-06, and entered in this document's v1.0 change history. ARS-NUM is specification version 1.0, approved 2026-11-08 and recorded in this document's v1.1 change history.

## Source ownership

| Concern | Official source of truth | Scope and status |
| --- | --- | --- |
| Cross-product interaction and visual standards | This document in `business-platform-core/docs/standards/` | AraIa Smart Navigation, AraIa Smart List, identity and integration principles. Normative documentation; not a shipped component package. |
| Documentation governance | [POL-DOC-001](POL-DOC-001.md) in this repository | Product documentation lifecycle, traceability and AI knowledge constraints. |
| Coding conventions | `business-platform-framework/DEVELOPMENT_STANDARDS.md` | Naming, code organization, programming practices. It is separate from interaction/UI standards. |
| Core application UX decisions and gates | `business-platform-core/docs/architecture/` and its UX/sprint documents | Decisions are individually marked proposed, approved, implemented, technically validated, or human accepted. Existing Sprint 1 UX gate remains pending unless a later explicit decision changes it. |
| AraIa UI implementation snapshot | `business-platform-core-s2-1-ui/src/Business.Platform.Core.Api/wwwroot/` and `Pages/Shared/_AraIaLayout.cshtml` | Tokens, CSS and shell are implementation evidence. ERP asset manifest identifies a working-tree snapshot with `publishedRelease: false`; this is not a published shared library or license to copy unversioned assets. |
| ERP-specific domain rules/contracts/evidence | ERP repository `docs/IMOB_*.md`, API, migrations and tests | ERP owns property behavior. Its current Smart List implementation is product-local; it is not yet a cross-product component. |
| Enterprise/product integration architecture | `erp-imobiliario-enterprise-architecture/architecture/IMOB_ARQ_REFERENCIA.md` and related references | Product boundaries and proposed integration contracts; does not prove external services or APIs are available. |
| Cross-product numeric presentation and transport | This document, **ARS-NUM-001**, in `business-platform-core/docs/standards/` | Approved functional standard and adoption matrix; not a shared runtime library or API contract. Each product owns domain precision, rounding policy, persistence and validation. |
| Cross-product interaction contracts, templates and conformance gate | This document, **ARS-FUNC-001**, **ARS-TPL-001** and **ARS-GATE-001** | Approved documentary standards from the 2026-11-09 consolidation; implementation and product acceptance remain independently evidenced. |

When sources disagree, approved decisions and product-owner instructions take precedence. Derived documents link to the source rather than copying detailed rules.

## Maturity vocabulary

Every material rule and artifact uses one explicit state: **Proposto**, **Aprovado como diretriz**, **Implementado**, **Validado tecnicamente**, **Homologado**, or **Publicado**. State promotion requires its own evidence; a proposal is not implementation, a test is not human acceptance, and a local implementation is not publication. Track specification, implementation and publication versions separately when they differ. Each rule has a stable ID, description, state, version, decision source and change history under POL-DOC-001.

## AraIa Smart Navigation — ARS-NAV

**State:** approved interaction standard. **Reusable component:** not established across products. A local shell/navigation implementation is not by itself proof of a supported shared package.

- **ARS-NAV-01** — Group navigation by user task and domain, with concise labels, minimal icons, and explicit submenu affordance. Preserve a clear current-page indication, organization/workspace context and a skip-to-content path where applicable.
- **ARS-NAV-02** — At mobile widths, provide a keyboard-operable open/close control, visible focus, a dismissible backdrop where used, and predictable focus restoration. Respect browser zoom and reflow rather than forcing a fixed-width canvas.
- **ARS-NAV-03** — Hide or disable entries according to server-issued access context and, when entitlements are operationally available, the relevant contracted capability. This is presentation only: every destination and command must authorize on the server.
- **ARS-NAV-04** — Product-owned destinations remain autonomous within one integrated authenticated experience: users normally enter through the platform's single access and do not need separate product credentials or portals. Navigation handoff uses only an approved route/context and authentication/federation contract; the destination revalidates identity, organization, active authorized establishment where applicable, entitlement/license, active profile and effective operation permission on its backend. No cross-product database access, shared cookies, or duplicated domain operation. See DCSE-01 and the approved integrated-access direction; this rule does not promote the maturity of any technical mechanism.
- **ARS-NAV-05** — Empty, loading, denied, unavailable and stale-session states explain the next safe action without leaking names or data from another organization.

## AraIa Smart List — ARS-LIST

**State:** approved interaction standard. **Reusable component:** not established across products. ERP Smart List is an implemented product-local pattern in `IMOB_UX_02.md`.

- **ARS-LIST-01** — Provide labeled search, field filters, clear/reset, allowlisted sort in both directions, server pagination and contextual actions. Selection scope and consequences must be explicit; do not imply bulk mutation where none exists.
- **ARS-LIST-02** — Apply organization, establishment, permission and entitlement scope before data/count/filter/order/page operations. Bind values as parameters and allowlist sort identifiers; do not filter only the already-paged subset.
- **ARS-LIST-03** — Define empty, loading, no-results, invalid-filter, denied, stale/conflict, error and partial-unavailable states. Preserve entered filters and provide a clear recovery action where safe.
- **ARS-LIST-04** — At narrow widths, transform rows into readable cards or a deliberately labeled scroll region; preserve hierarchy, labels, prices and actions without page-level horizontal overflow. Keep keyboard, touch and assistive-technology operation equivalent.
- **ARS-LIST-05** — Products configure labels, columns, domain filters and authorized actions. Shared presentation must not absorb product rules, SQL, permissions or records.

## AraIa visual identity — ARS-VIS

**State:** approved as a consistency objective; exact production token release remains versioned by the UI source and relevant product gates. **Reusable package:** not established by this document.

- **ARS-VIS-01** — Use the AraIa token source for color, typography, spacing, radius, control size, focus and status colors when an authorized compatible release is available. Do not invent a competing token palette in a consumer product.
- **ARS-VIS-02** — Pair color with text/icon/structure; provide readable contrast, visible focus, semantic labels, responsive reflow, native zoom and reduced-motion behavior where motion exists.
- **ARS-VIS-03** — Keep the source identity assets and their version/hash manifest. A copied working-tree snapshot must be labeled as such; no claim of a published version or compatibility follows from matching hashes.
- **ARS-VIS-04** — A reusable component exists only when its owner, version, public interface, support policy, compatibility range, tests, accessibility criteria, distribution and consumer authorization are documented. Until then, record local adaptation or copied assets as implementation facts, not shared-library reuse.

## Functional interaction contracts — ARS-FUNC

**ARS-FUNC-001 v1.0 — approved documentary standard, 2026-11-09.** Product domains remain authoritative for field meaning, persistence, validation and authorization. This contract standardizes presentation and interaction; it does not create fields, permissions, data sources or shared runtime components.

| Concern | Reusable contract | Product-owned decision / evidence |
| --- | --- | --- |
| Fields and labels | Use a concise, stable label that names the business concept. Associate help text and validation with the field; identify required state in text/semantics, preserve submitted values after recoverable errors, and focus/announce the error summary or first invalid field. | Requiredness, format, bounds, cross-field rules, source, privacy and persistence are defined by the product contract. Do not infer or synthesize data. |
| Small known choices | For a small, mutually exclusive set, use a single-choice group with selectable cards only when each option has a clear text label, keyboard operation, visible focus and an announced selected state. Provide a native radio-equivalent semantic. | Product defines options, defaulting, cardinality and whether a choice is reversible. An icon supplements the text; it never carries meaning alone. |
| Large or changing choices | Use a searchable selector or server-backed lookup, with clear result count, no-result and error states. Do not load an unbounded directory into a static control. | Product defines eligible records, query semantics, sort and scope. Revalidate selected IDs and current eligibility on the server at save time. |
| Search and lists | Apply ARS-LIST: labeled search and filters, reset, allowlisted sorting, deterministic order, server-side scope and pagination, explicit selection scope, and contextual actions. | Product owns query fields, matching rules, actions and data ownership. Apply organization, active establishment, permission and entitlement before count/order/page. |
| Money and periodic values | Apply ARS-NUM-001: locale-aware display, explicit currency/unit, exact domain amount, null distinct from zero, and no display formatter changing stored/calculated values. Show a periodicity only when its source value is present and the period is known. | Product owns source precedence (for example, property versus active commercial announcement), rounding, applicability, period vocabulary and authorization. Never infer a period or use a technical value as commercial copy. |
| Authorization and business context | UI visibility follows the server-issued access context, but is never an authorization boundary. Every read and write revalidates user, product entitlement, organization, active establishment and operation permission at the owning backend. Fail closed on unavailable authorization. | Core owns shared identity, organization/establishment and entitlement contracts; each product owns its domain rules and data. A license alone does not grant an operation. |
| Loading and result states | Define loading, empty, no results, invalid input/filter, denied, stale/conflict, failure and partial-unavailable states. Preserve safe user input and provide an actionable recovery. Avoid leaking other tenants' existence or data. | Product defines retryability, error mapping and recovery; tests/evidence identify the states actually exercised. |
| Navigation and contextual actions | Group by user task. Use direct, bookmarkable destinations for substantial subtasks; keep context on return, provide a clear route to the summary/list, and continue after save. Actions appear only when the flow exists and is authorized. | Product owns routes, panel names, action semantics and workflow completion. Query-string sections are acceptable when consistent with existing routing/accessibility. |

## Presentation templates — ARS-TPL

**ARS-TPL-001 v1.0 — approved documentary standard, 2026-11-09.** These are presentation contracts, not mandatory page rewrites. Reuse an existing product template when it meets the contract; retain the product's data owner, backend and layout identity.

| Template | Minimum presentation contract | Source/reuse boundary |
| --- | --- | --- |
| Search/list | Search/filter region, active criteria, count, ordering, paged results, empty/error states and authorized row actions; mobile rows reflow into labeled cards or a deliberate accessible scroll region. | ARS-LIST; reuse local Smart List controls where compatible. |
| Create/edit | Clear task heading, labeled fields grouped by task, field and summary validation, explicit save/cancel, preserved input after recoverable failure, and conflict handling where versioned. | Product form/domain contract; don't duplicate validation or data model in UI. |
| Read-only consultation | Commercially useful, authorized facts with source-aware unknown/empty representation and a clear route to related operational work. | Product read contract; no hidden mutation through GET. |
| Operational record | Compact real-data summary plus task-specific subtabs/panels; expose the requested panel directly, retain stable links and return path, and resume after save. | Reuse existing navigation behavior (for example, Core profile sections or an Imob customer panel) as a pattern, not as an assumed shared component. |
| Commercial property card | Preserve authorized photo/placeholder, commercial code/title, location, active authorized price and selection/action. Optionally show bedrooms, parking, condominium and IPTU only when present in the existing authorized source. | Reuse the product's property/listing projection and approved SVG symbols. Do not make per-card duplicate queries, expose UUIDs/internal notes/synthetic descriptions, or invent values. For ERP Imob, the approved source precedence is the active announcement matching the displayed purpose, falling back to capture when that announcement amount is null. Preserve explicit zero; keep each amount with its own source period, omit null amounts, do not convert periods, and label an unknown period explicitly when an amount exists. See `IMOB_COM_01.md` for the product rule and evidence. |
| Agenda | Make date range/view, filters, upcoming items, clear time/status, and quick access to authorized details/actions legible at desktop and mobile widths. | Product appointment contract owns lifecycle, time zone, recurrence/conflict semantics and persistence. A calendar connector or map is not implied. |
| Panels and dashboard | Keep each panel's purpose, title, summary, state and actions explicit; use real scoped data and a meaningful empty state. | The intelligent dashboard remains a future transversal direction and is not authorized by this template to implement. |

## Inventory and asset compatibility

| Asset/pattern | Observed source | Maturity and reuse |
| --- | --- | --- |
| Core tokens | `business-platform-core-s2-1-ui/src/Business.Platform.Core.Api/wwwroot/araia-tokens.css` (`--brand-navy` #061638, `--brand-deep` #092654, `--brand-cyan` #00dceb, `--brand-blue` #087cff, `--brand-violet` #9658f5, `--brand-gold` #f5b841; semantic success #197347, warning #885700, danger #b53640; Segoe UI font stack; `--text-*`; `--space-*` 4–48px; `--radius-*`; `--focus`; 44px control height) | Implemented snapshot. Use only through a documented compatible asset version; values are not to be copied into a competing palette. |
| Core components and responsive/accessibility CSS | `.../wwwroot/araia-components.css`, `araia-brand.css`, `araia-ux-final.css` | Core implementation evidence, not a cross-product distributable package. The snapshot has 44px controls, 3px `:focus-visible` outlines in components, reduced-motion handling, and responsive rules including 800px breakpoints; consumers validate their own pages and zoom/reflow behavior. |
| SVG symbol set | `.../wwwroot/araia/icons.svg` (e.g. `i-home`, `i-building`, `i-users`, `i-search`, `i-filter`, `i-check`, `i-file`) | Symbols are available in the snapshot only. Verify exact symbol IDs before use; don't invent IDs or represent a product-specific concept with a misleading symbol. |
| Core navigation/profile sections | `business-platform-core-s2-1-ui/src/Business.Platform.Core.Api/Pages/Shared/_BusinessProfiles.cshtml` and query-string section routes | Implemented Core behavior. May inform a product-local interaction; no shared Razor component/API is established. |
| Imob asset manifest | `erp-imobiliario-platform/docs/ui/araia-assets.manifest.json` | The manifest marks the source as a working-tree snapshot with `publishedRelease: false`. It is not a published library, release, compatibility guarantee or blanket copying permission. |
| Imob Smart List | `erp-imobiliario-platform/backend/src/RealEstate.Web/Pages/Shared/_ListingControls.cshtml`, `wwwroot/listing.css`, `wwwroot/listing.js` | Implemented and technically validated product-local pattern; Monday records desktop visual acceptance, not manual mobile acceptance or all-workflow acceptance. |
| Imob customer commercial panels/cards | `.../Pages/Shared/_CustomerCommercial.cshtml`, `wwwroot/erp.css`, and `wwwroot/cad-icons.svg` | Implemented local templates. The IMOB-03 indicator increment uses the existing projection and tokens plus locally added `bed`, `car`, and `tax-receipt` SVG symbols; `building` is reused for condominium. No shared/published icon package is implied. Human visual acceptance remains pending. |

An asset is cross-product reusable only after the owner, stable version/release, public interface, support/compatibility range, tests, accessibility criteria, distribution and consumer authorization satisfy ARS-VIS-04. A copied asset or matching hash proves file identity only. The current Imob snapshot is not a shared published library.

## Conformance gate — ARS-GATE

**ARS-GATE-001 v1.0 — approved documentary gate, 2026-11-09.** Apply before implementing a new screen or materially new screen flow. For a small correction, check and record only the criteria affected; do not create a separate review ceremony when the answer is already evidenced by the current contract and implementation. An unanswered visual reference is a documented gap requiring an owner decision, not permission to invent a definitive design.

| # | Criterion | Verifiable question/evidence |
| --- | --- | --- |
| 1 | Functional contract | Are actor, purpose, inputs, validation, states, actions, completion and acceptance explicit? |
| 2 | Data ownership | Are producer/source of truth, product boundary, identifiers, scope and transformations known? |
| 3 | Component reuse | Were existing compatible components/services inspected and reuse/adaptation or reason not to reuse recorded? |
| 4 | Approved visual reference | Is the owner-approved reference or applicable documented pattern identified? If absent, is the precise gap submitted for decision? |
| 5 | Security and authorization | Are permission, entitlement, organization, active establishment, server enforcement and negative scope defined? |
| 6 | Functional completeness | Does every visible operational action complete its normal flow, and are unavailable capabilities hidden or clearly non-operational? |
| 7 | Design System/version | Are tokens/assets from an identified compatible version and within its distribution/support status? |
| 8 | Accessibility/responsiveness | Are labels, semantics, keyboard/focus, contrast, zoom, reflow and touch criteria addressed at the relevant sizes? |
| 9 | Evidence/maturity | Are implementation, technical validation, human acceptance and publication stated separately with baseline and evidence? |
| 10 | Regression/test reuse | Are impacted checks selected, prior evidence reused only when applicable, and gaps/not-run work recorded without repeating approved suites? |

The gate is a readiness/conformance check, not an approval shortcut. A satisfied documentary gate does not authorize implementation, external writes, schema changes, publication or human acceptance beyond the task's authorization.

## Integration contracts — ARS-INT

**State:** approved platform boundary principle. Individual contracts remain governed by their own decisions and versions.

- **ARS-INT-01** — Define source of truth, producer/consumer, direction, IDs, permission/entitlement, consent, versioning, idempotency, errors, retries/reconciliation and audit before integration.
- **ARS-INT-02** — Products are autonomous. A product may operate independently; optional connection can add capability without duplicating the other product's responsibility or making its runtime a mandatory dependency absent an approved contract.
- **ARS-INT-03** — Prefer versioned, minimal API/event contracts over shared domain tables or direct database access. No adapter may presume a third-party API, paid service, credential, or license exists.
- **ARS-INT-04** — Consumers degrade safely on optional dependency outage. Suggestions or AI-assisted changes require the caller's existing scope and explicit user action before committing business data.

## Numeric values — ARS-NUM

### ARS-NUM-001 v1.0

**State:** approved functional standard, 2026-11-08. **Implementation:** partial by product; see adoption matrix. **Shared component/API contract:** not approved or released. **Origin:** explicit decision of the product owner, “AraiA — Diretriz transversal de padronização numérica”. This standard defines presentation and interoperability principles; it does not authorize bulk product, database, or API changes.

- Use `pt-BR` initially for users in Brazil: comma decimal separator and period thousands separator. Keep locale-specific formatting at the presentation boundary; APIs and persisted interchange use canonical locale-independent numeric representations under each existing contract.
- Mark each numeric field by domain nature. A field's metadata should identify its kind, display precision, unit/currency where relevant, nullability and any field-specific exception. Do not infer monetary meaning solely from the underlying numeric type or label.
- Defaults are display defaults, not storage scales or calculation rounding rules. Money, area, length and percentage display two decimal places unless an approved field rule says otherwise. Integer quantities display without decimal places. Fractional quantities and coordinates use domain-defined precision and units.
- Preserve null separately from zero. Formatting on edit completion must support natural entry and must not rewrite the canonical value merely to change its appearance or move the caret during typing.
- Select storage and API numeric types to meet domain precision and range. Use decimal/fixed-point representations for exact decimal domains such as monetary amounts; do not convert monetary values through binary `float`/`double`. Integer minor units are suitable only where that product's existing approved contract defines them.
- JSON/API numeric serialization and validation are locale-independent. Decimal separators, grouping marks, rounding, scale limits, units and range validation belong to explicit contracts or domain rules, not client locale. Preserve existing contracts and historical precision during progressive adoption.
- Rounding is owned by the relevant business domain and must be specified per calculation. A display formatter must not silently round the value used for persistence or calculation.
- Share a formatter/component only when its owner, interface, compatibility, validation, accessibility and distribution are approved under ARS-VIS-04. Until then products may implement a local adapter to this standard; do not create parallel formatters within a product or assume cross-product package availability.

| Numeric nature | Default presentation | Appropriate representation and persistence | Integration / validation | Adoption state |
| --- | --- | --- | --- | --- |
| Currency / money | `pt-BR`; two decimals and currency/unit made clear; field-specific exception only by approved rule | Decimal/fixed-point of domain-appropriate precision, or approved integer minor-unit contract; null distinct from zero | Locale-independent decimal contract, explicit currency, scale/range and domain rounding | ERP Imob monetary fields: implemented and technically validated locally under CAD-MONEY-001 v1.0; human acceptance pending. Core: standard documented, product implementation not assessed. |
| Area | `pt-BR`; two decimals by default, with unit (for example m²) | Decimal precision/range set by property domain; retain historic precision | Canonical locale-independent number; validate nonnegative/range/unit per domain | ERP Imob fields retain existing decimal contract; transversal UI adherence beyond money not assessed. Other products not assessed. |
| Length | `pt-BR`; two decimals by default, with unit (for example m) | Decimal precision/range set by domain; no inference/calculation unless approved | Canonical locale-independent number; explicit unit and range | No cross-product implementation assessment. |
| Percentage / rate | `pt-BR`; two decimals by default; identify `%` and distinguish fraction from percentage points in the contract | Decimal or other exact domain representation at calculation precision; do not force stored scale to two | Contract defines whether `3.5` means 3.5%; rounding is defined by the owning domain | ERP Imob commission percentages stay numeric and unmasked; broader adoption not assessed. |
| Quantity / count | Integer without decimals when the concept is discrete; fractional quantities declare unit and precision | Integer for discrete counts; decimal for measured/fractional quantities as defined by domain | Canonical locale-independent representation; validate integral/fractional semantics and bounds | No cross-product implementation assessment. |
| Coordinate | Domain-appropriate visible precision and explicit coordinate system; do not apply currency/measurement mask | Decimal or spatial/geodetic type suitable to accuracy and range, preserving coordinate reference | Contract identifies coordinate order/reference system and canonical serialization | ERP Imob existing decimal handling is preserved; map/provider integration remains a separate dependency. |
| Other numeric types | Explicit per-field metadata; no generic two-decimal assumption | Type selected for range, precision and domain semantics | Versioned contract states units, bounds, nullability and serialization | Product assessment required before adoption. |

### Consumer adoption record

This is a baseline inventory, not a declaration of conformance. No shared formatter package is released by this decision.

| Consumer | Current evidence / maturity | Progressive adoption proposal |
| --- | --- | --- |
| Business Platform Core | ARS-NUM-001 is documented here. No Core UI/API-wide implementation or field inventory was executed in this activity. | Inventory numeric fields and current API types by module; map approved metadata and contract tests before changing screens or contracts. |
| ERP Imobiliário | Existing CAD-MONEY-001 v1.0 implements and technically validates local BRL formatting for monetary fields; human acceptance remains pending. Other kinds need field-by-field assessment. | Retain local implementation and evidence; assess areas, lengths, rates, counts and coordinates separately. Do not mix this work with the open draft-save investigation. |
| ERP Financeiro | Not inspected in this activity; implementation and adherence unknown. | Inventory accounting precision, rounding, currency and integration contracts with Finance domain owners before UI changes. |
| CRM | Not inspected in this activity; implementation and adherence unknown. | Inventory numeric fields and API conventions, then propose only material deltas. |
| GIV | Not inspected in this activity; implementation and adherence unknown. | Inventory numeric fields, units and calculation semantics, then propose only material deltas. |
| Future products | No implementation exists by this record. | Apply ARS-NUM-001 during product specification and define domain-specific exceptions before persistence/API design. |

No changes to product schemas, public API contracts or other products are authorized by this consumer matrix. Product sources remain authoritative for domain-specific rules and evidence.

## Reusable validation matrix

Status is recorded as executed now, prior evidence reused, not run, blocked, or human acceptance needed. Evidence is valid only for its recorded baseline, version, environment and scope.

| Check | General acceptance expectation | Current evidence classification |
| --- | --- | --- |
| Desktop / tablet / mobile | Layout and task completion at agreed widths; no clipped content or page overflow | ERP Edge mobile viewport 390×844 and desktop captures: **prior evidence reused** for its screens. Tablet matrix and physical devices: **not run**. Core full device matrix: **not run**. |
| Keyboard and focus | All navigation/list/form actions reachable; order, announcements and focus restoration are meaningful | ERP listing keyboard selection/navigation tests: **prior evidence reused**. Full navigation, gallery and all forms: **partial coverage / not run**. |
| Accessibility | Semantic name/role/state, error announcements, contrast/focus/zoom and applicable WCAG criteria | ERP Razor/Edge automated checks: **prior evidence reused** for covered cases. Screen readers, independent audit and WCAG conformance: **not run / human validation needed**. Core sprint criteria are documentary proposals where marked so. |
| Permissions and entitlements | Hidden controls plus server-side deny; tenant/unit isolation; entitlement only when operationally authoritative | ERP permission/RLS tests: **prior evidence reused**. Real Core/OIDC/licensing end-to-end: **not run**. |
| Empty, large and paginated lists | Correct empty/no-results states; filters/order before pagination; deterministic ordering; useful loading/error states | ERP PostgreSQL tests over >100 synthetic rows and Edge list checks: **prior evidence reused**. Cross-product Smart List: **not implemented**. |
| Search, filters, sort, selection | Parameterized search, allowlisted sort, clear, selection scope and accessible state | ERP UX-02 test evidence: **prior evidence reused**. Other products: **not run**. |
| Version compatibility | Existing public APIs preserve semantics; new contracts are additive/versioned with negative and regression tests | ERP v1/v2 regression evidence: **prior evidence reused**. Shared cross-product component compatibility: **not applicable until package exists**. |
| Product regressions | Consumers are tested when a shared versioned artifact changes | No shared UI package is distributed; **not run** as a cross-consumer test. |

Do not add counts across overlapping suites as if they were distinct tests. Re-run only for material changes, unresolved gaps or a required gate.

## Change history

| Version | Date | Change | Decision source |
| --- | --- | --- | --- |
| 1.0 | 2026-11-06 | Initial versioned standards catalog, ownership map, maturity rules and validation matrix | Approved corporate directive supplied by product owner |
| 1.1 | 2026-11-08 | Added ARS-NUM-001 v1.0 and consumer adoption baseline; no shared runtime package or product-wide changes authorized | Approved “Diretriz transversal de padronização numérica” |
| 1.2 | 2026-11-09 | Added ARS-FUNC-001, ARS-TPL-001 and ARS-GATE-001; inventoried Core/Imob UI evidence, clarified snapshot compatibility and reconciled IMOB-UX-02/IMOB-03 maturity. Documentation only; no shared component or Sprint 04 execution authorized. | Owner directive “Consolidação Funcional e Visual”; Monday X2-01, IMOB-UX-02 and IMOB-03 evidence |
| 1.3 | 2026-11-09 | Recorded the approved ERP Imob property-card source precedence, null/zero handling and fee-period presentation, with product-local implementation evidence. No cross-product package or Sprint 04 work authorized. | Owner directive “IMOB-03 — Refinamento dos cartões comerciais” |
| 1.4 | 2026-11-16 | Linked the approved single-access experience to ARS-NAV-04, clarifying cumulative access checks and Core/product ownership without selecting or promoting a technical federation mechanism. | Owner directive “Consolidação documental do modelo de acesso único”; DCSE-01 |
