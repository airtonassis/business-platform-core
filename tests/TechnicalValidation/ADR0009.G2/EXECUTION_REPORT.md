# ADR-0009 - G2 execution report

Date: 2026-10-31. Execution authorized by the responsible person in the attached G2 instruction. Formal G2 approval remains HUMAN and pending.

## Scope and outcome

31 deterministic SQL/driver variants PASS, 0 FAIL, 0 BLOCKED, 0 NOT RUN in the selected G2 scope. Evidence: `f31a6617ed85` (30 PASS) plus `6909108391b4` (1 PASS). The 54 original multi-gate protocols are NOT declared complete. No G3/G4, V11 structural, Application/API/OIDC, production, commits, publication or Sprint 1.

The 20 explicitly excluded runner variants comprise 13 DEFERRED (G3) and 7 NOT RUN (approved G1 surface reused or D1 characterization not repeated). G4 requirements are deferred at parent protocol level, not counted as invented new variants. The inherited raw runner results retain 20 BLOQUEADO / 34 NOT_EXECUTED for the 54 parents in the primary run; those are not counts of executed SQL subproofs. `coverage.json` is the G2 variant matrix.

## Environment and guards

Git main and origin/main: 6c957200c436c06a243ac2a1aae57d8ef1efc268. Clean tracked tree at start; only ADR0009.G2 files created.

PostgreSQL 17.11, Docker server 20.10.22, SDK global.json 10.0.401/latestPatch, .NET runtime 10.0.12, Npgsql 10.0.3. Pinned local image sha256:79bd7c99e923138f136f8009d6bffa66e21e9d4fda5c0c561b00fc9c90cfe537; repository digest postgres@sha256:b0f9560a2de083e2cc7382e75f808c7381a32852a7ec49117deedb300e552b24. No pull.

Each run has a unique container, database, label and dedicated network, loopback-only dynamic port, no host/named volume, tmpfs flags for PGDATA/socket/tmp, and random per-run credentials kept in memory. The frozen SQL requires the legacy bpc_adr0009_g1_<run> database name and disposable marker; those identifiers are intentionally reused in NEW containers, while all outputs are separate under ADR0009.G2. No shared container/database was a write target. Network/internal=false in successful runs: Docker 20.10.22 internal networks did not publish the requested port. Network ownership and a single connected run container are checked before schema installation. No claim of an egress firewall.

Runtime logins core_identity/core_tenant/core_platform are distinct from five NOLOGIN exec owners, ddl_owner, isolated postgres observer/fixture operator and bounded g1_probe. Fixture/SET ROLE evidence is not counted as real runtime authorization. D1 residual trust boundary is unchanged.

## Results and security

The real catalog has 12 tables, 40 policies, 78 triggers (including internal FK triggers), 63 constraints and 26 indexes. Policy/trigger/constraint/index records exactly match approved G1 ece79aa930df. All 12 tables have ENABLE/FORCE RLS and ddl_owner ownership; no unsafe runtime/exec role attributes or memberships observed.

Effective grants match D2: exec_identity=46, exec_tenant=66, exec_bootstrap=39, exec_context=12, exec_integrity=15, total 178 column privileges; no excess/missing grants, EXECUTE drift, PUBLIC EXECUTE or unexpected schema CREATE. Eleven real function definitions/owners match approved G1, as do policy/trigger dependencies. The legacy comparator retains its non-approving/graph-UNDETERMINED labels; this execution does not issue a new independent review or reopen the human D2 approval.

Known P2 metadata omission: security.expected.json lists 11 table names and omits identity.identity_links. The table is in the approved IdentityLink model, frozen SQL and approved G1 observed catalog; the same additionalObservedTables entry exists in ece79aa930df. Fresh table set equals approved G1 exactly, and the table has FORCE RLS. No new drift or demonstrated vulnerability. Do not edit the frozen expected file; a future authorized inventory reconciliation may correct the descriptor, preserving history.

Actual tests cover tenant reads/writes and cross-target rejection, absent/mixed/malformed context, physical PID reuse after commit/rollback/SQL exception, savepoint context removal/restoration, persistent synthetic session baseline reset by Npgsql, deferred COMMIT and SET CONSTRAINTS checks, partial-state rollback, native PK/UNIQUE/CHECK/FK/NOT NULL diagnoses, immutable links/organization/consumption, direct Owner protection with two Owners, invitation guards and historical identity consumption. Positive own writes and runtime accept/revoke/bootstrap paths are observed; successful business writes include audit. Replace/replay regression evidence is reused from approved G1 ece79aa930df/F11; no new concurrent replace protocol is executed.

SQLSTATE is not the sole oracle: error origin/message/object/role checks and pre/post read-only observations support negatives. V02 retains distinct RLS 42501 and NOT NULL catalog/native evidence. A zero-row UPDATE is not treated as a validator test.

## Reproduction and selection

Run from repository root; requires the same approved local image and authorized disposable Docker target:

```powershell
python tests/TechnicalValidation/ADR0009.G2/test_selection.py
dotnet restore tests/TechnicalValidation/ADR0009.G2/runtime/G2.Validation.csproj
dotnet build tests/TechnicalValidation/ADR0009.G2/runtime/G2.Validation.csproj -c Release --no-restore
dotnet tests/TechnicalValidation/ADR0009.G2/runtime/bin/Release/net10.0/G2.Validation.dll --execute-g2
python tests/TechnicalValidation/ADR0009.G2/verify_evidence.py <new-run-id>
```

The build generates only ignored obj/G2.Program.g.cs from read-only G1 Program.cs plus the bounded G2 adapter. No copied/edited G1 file is persisted. The selector tests include an injected K01 negative. Full --execute-g2 now selects 31 variants. --remaining was used for the corrected probes; --pool-baseline-only executed only the final pool gap. selection.json is materialized before database creation in each run. Exact source hashes are in g2-source-hashes.json for later runs, including the two final evidence runs. Early exploratory runs lack that additional G2-source hash capture and are historical only.

Build final PASS (0 warnings/errors), selector tests PASS (2), offline verification PASS (272 original hashes, ACL drift, definitions/dependencies, JSON/JSONL, bounded secret-pattern scan, cleanup). Initial generated-code build errors were limited to nullable directives and transaction logging syntax; corrected only in G2 generator before DB tests. No remote CI was executed in this task.

## Preserved failures and setup history

| Run | Observed subproofs | Interpretation |
|---|---|---|
| 3ed87d94f5a1 | {'FAIL': 1} | Native CHECK probe FAIL: new organization ID outside probe context. The failed native diagnosis was not retained before its assertion; cause inferred from SQL/policy and corrected positive setup. Context now matches new ID and diagnostic is captured before assertion. |
| 47bc1a1f4a70 | {'PASS': 3} | Three corrected/native probes PASS. |
| 4ef594640a03 | {'PASS': 2} | Corrected V08/Q12 PASS. |
| 5c56afedf9e5 | {'PASS': 1, 'FAIL': 1} | V08 PASS, Q12 FAIL: title oracle depended on earlier own-write fixture absent in fresh selective run. Corrected to exact A record identity; no policy/grant change. |
| 6909108391b4 | {'PASS': 1} | Additional persistent session baseline reset PASS; reused preceding 30 valid proofs. |
| e76986da515a | {'PASS': 27, 'FAIL': 1} | 27 PASS, V08 FAIL: context cleared before UPDATE, RLS hid anchor; zero-row update queued no validator. Corrected probe requires RETURNING anchor before removing context at COMMIT. |
| f31a6617ed85 | {'PASS': 30} | Final 30-variant regression PASS with source hashes. |
| f5b926a634cd | No subproof started | Setup blocked before SQL installation: internal network exposes no published port; cleaned. |
| fa2eef84ad7a | No subproof started | Same setup issue with recorded NetworkSettings/stack; cleaned. |

FAIL records and conservative stoppedForP0 flags were not overwritten/reclassified. No confirmed security-model P0/P1 remained after diagnosis: fixes changed probe preparation/oracles only, not guards, grants, RLS, approved SQL or G1. Every run has cleanup evidence.

## Coverage matrix

| ID / variant | Result | Evidence run |
|---|---|---|
| F01 / minimal-grants-and-secret-read-denied | PASS | f31a6617ed85 |
| F01 / runtime-direct-dml-denied | PASS | f31a6617ed85 |
| F02 / G2-physical-model-catalog | PASS | f31a6617ed85 |
| F02 / owners-and-runtime-not-bypass | PASS | f31a6617ed85 |
| F03 / public-execute-and-wrong-port-denied | PASS | f31a6617ed85 |
| F04 / no-set-role-or-owner-escalation | PASS | f31a6617ed85 |
| F05 / G1-installed-T-SELF-REC-BOOT-boundaries | PASS | f31a6617ed85 |
| F06 / fixed-path-and-denied-shadow-object | PASS | f31a6617ed85 |
| F09 / invalid-invitation-inserts-issuer-and-chain | PASS | f31a6617ed85 |
| F09 / private-audit-receipts-direct-denials | PASS | f31a6617ed85 |
| H01 / revoked-identity-cannot-accept-other-enterprise | PASS | f31a6617ed85 |
| Q07 / G2-each-context-field-read-write-denials | PASS | f31a6617ed85 |
| Q07 / missing-mismatched-malformed-context | PASS | f31a6617ed85 |
| Q08 / G2-cross-tenant-target-write-and-own-positive | PASS | f31a6617ed85 |
| Q08 / tenant-sql-isolation | PASS | f31a6617ed85 |
| Q11 / npgsql-pool-commit-rollback-exception | PASS | f31a6617ed85 |
| Q12 / G2-savepoint-restores-previous-context | PASS | f31a6617ed85 |
| Q12 / G2-session-baseline-reset | PASS | 6909108391b4 |
| Q12 / savepoint-context-rollback | PASS | f31a6617ed85 |
| V01 / active-account-without-member-commit | PASS | f31a6617ed85 |
| V02 / G2-native-keys-and-checks | PASS | f31a6617ed85 |
| V02 / split-rls-not-null-catalog-and-rollback | PASS | f31a6617ed85 |
| V03 / cross-organization-foreign-key | PASS | f31a6617ed85 |
| V04 / owner-transitions-and-role-promotion-denied | PASS | f31a6617ed85 |
| V04 / partial-revocation-rejected | PASS | f31a6617ed85 |
| V05 / organization-without-owner-at-commit | PASS | f31a6617ed85 |
| V06 / G1-event-coverage-multiple-updates-and-immediate-check | PASS | f31a6617ed85 |
| V07 / immutable-org-and-historical-consumption | PASS | f31a6617ed85 |
| V07 / two-active-Owners-direct-owner-revocation-denied | PASS | f31a6617ed85 |
| V08 / G2-technical-context-components-and-rollback | PASS | f31a6617ed85 |
| V08 / missing-integrity-context-rejects | PASS | f31a6617ed85 |

## Exclusions

| ID / variant | Status | Reason |
|---|---|---|
| Q10 / compromised-credential-characterization-only | NOT RUN | Approved G1 surface reused; not a G2 variant |
| K01 / accept-A-B-two-physical-connections | DEFERRED | G3 concurrency/failure protocol |
| K04 / same-version-two-writers | DEFERRED | G3 concurrency/failure protocol |
| K08 / audit-failure-rolls-back-demo | DEFERRED | G3 concurrency/failure protocol |
| K07 / confirmed-commit-lost-response-bootstrap-replay | DEFERRED | G3 concurrency/failure protocol |
| V13 / real-deadlock-in-dedicated-probe | DEFERRED | G3 concurrency/failure protocol |
| K05 / concurrent-replace-and-authorized-replay | DEFERRED | G3 concurrency/failure protocol |
| K02 / bootstrap-versus-accept-both-orders | DEFERRED | G3 concurrency/failure protocol |
| K03 / revoke-versus-command-both-orders | DEFERRED | G3 concurrency/failure protocol |
| K06 / expiry-revalidated-after-real-lock-wait | DEFERRED | G3 concurrency/failure protocol |
| K09 / Owner-and-self-revocation-denied | DEFERRED | G3 concurrency/failure protocol |
| K10 / pool-after-actual-SQL-abort | DEFERRED | G3 concurrency/failure protocol |
| V11 / revocation-versus-owner-reactivation-after-wait | DEFERRED | G3 concurrency/failure protocol |
| F01 / G1-runtime-table-operation-matrix | NOT RUN | Approved G1 surface reused; not a G2 variant |
| F02 / G1-DDL-TRUNCATE-protections-denied | NOT RUN | Approved G1 surface reused; not a G2 variant |
| F03 / G1-every-ungranted-function-runtime-call | NOT RUN | Approved G1 surface reused; not a G2 variant |
| F04 / G1-SET-ROLE-and-self-grant-denied | NOT RUN | Approved G1 surface reused; not a G2 variant |
| V09 / G1-validators-no-explicit-locks-or-repair | DEFERRED | G3 concurrency/failure protocol |
| F10 / G1-semantic-capability-subtraction-probe | NOT RUN | Approved G1 surface reused; not a G2 variant |
| F11 / G1-reduced-grants-real-runtime-ports-and-replay | NOT RUN | Approved G1 surface reused; not a G2 variant |

Q12 cancellation, uncertain rollback/reset and backend discard fault injection remain G3, as Q12 is G2+G3 in the plan. Successful savepoint characterization does not authorize an Application to continue commands after proof/locks are lost. SELF/REC resolvers, API, OIDC, session-store behavior and recovery remain G4; no parent scenario PASS is inferred from SQL-only evidence.

## Cleanup, preservation and verdict

Each of the nine run containers and its dedicated network was removed by its exact run label/name. All storage was disposable; no global Docker cleanup. Final label queries return no run resources; the preexisting container ID/name inventory is unchanged. Connections/pools closed. Logs remain outside discarded resources.

All 272 G1 original SHA-256 values match originals.sha256.json. No tracked file, frozen ADR, contract, approved G1 history or expectation was modified. D1/D2/D3/D5 remain approved within their existing limits. G3/G4 and Sprint 1 not started.

**Technical verdict: READY FOR HUMAN REVIEW of the executed G2 SQL scope.** Selected coverage is complete (31/31) with reproducible evidence, no confirmed open P0/P1; known P2 descriptor omission noted above. Formal approval of G2 remains pending human decision. No approval of the 54 whole protocols, subsequent gates, production or publication is implied.
