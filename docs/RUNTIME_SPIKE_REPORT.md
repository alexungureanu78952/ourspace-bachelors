# Runtime spike: evidence and decision

Date: 2026-10-13. Status: constrained-layout experiment passed; arbitrary-code sandbox milestone remains open.

## Product context

OurSpace is a complete social platform including friends, posts/feed and private messaging. This report measures only its customizable-profile runtime. It provides no evidence for social-service throughput, privacy or reliability. The implementation plan includes these required services and their [integration boundaries](SOCIAL_PLATFORM_ARCHITECTURE.md).

## Finding

The measured fixture does not support the claim that a custom React profile is inherently too expensive to serve. Builds happen on revision changes; visits can serve immutable artifacts. This experiment instead supports avoiding a full dependency bundle on every edit: parse a declarative layout once and render it with one versioned shared React runtime.

This is a narrower source format, not a demonstrated implementation of arbitrary user React. AI generation, semantic evaluation and production workloads are not measured.

## Implemented

See [runtime experiment](../runtime/profile-spike/README.md).

- A TSX AST allowlist converts a default Profile function into a bounded JSON tree. Supplied code is never imported or executed.
- A CSS parser applies an explicit property/function/selector policy.
- A trusted React renderer displays the tree and escaped profile text.
- Artifacts include a documented CSP requiring exact trusted script/style hashes, blocked network capabilities and sandboxed execution.
- A local preview server and a real-browser test serve artifacts with required headers.
- An unconditional gate refuses arbitrary user-JavaScript execution.

The experimental contract is spike-v0. It is deliberately not named runtime-v1: whitespace/entity semantics, section bindings, typed schemas and final capability decisions remain unresolved.

## Reproducible measurements

Node v24.11.1; Windows; AMD Ryzen 7 5700U; 16 logical CPUs; 15.3 GiB RAM. One 309-byte TSX fixture with distinct revision text, five warmups and 30 sequential samples. Browser checks completed before this benchmark run.

| Operation | Median | p95 |
|---|---:|---:|
| Parse/validate source and profile data | 1.078 ms | 1.466 ms |
| Assemble shared-runtime artifact, including validation | 1.799 ms | 2.383 ms |
| Bundle a profile-specific trusted React renderer | 74.200 ms | 78.429 ms |

Shared runtime build: 116.412 ms on the first build in the process; not a cold-machine/cache measurement.
Shared runtime: 222,994 bytes, 69,188 bytes gzipped.
Per-profile JSON plus CSS: 1,042 bytes.
Profile-specific JavaScript comparison: 223,562 bytes, excluding CSS/HTML.

The standalone comparison bundles the trusted renderer with validated JSON, not arbitrary React programs. Its times do not include validation and therefore are not an end-to-end comparison. Shared-runtime assembly does include validation and hashing the embedded runtime. No throughput/capacity claim follows from these timings.

The runtime is built once and reused in memory. HTML currently embeds its bytes; runtime cache separation and CDN delivery are not implemented. The 1 KB measurement is the variable payload, not the total downloaded artifact.

Raw evidence: [benchmark](evidence/profile-spike-benchmark.json), [browser results](evidence/profile-spike-browser.json), [environment and dependency versions](evidence/profile-spike-environment.json).

## Acceptance results

31 policy tests passed: one valid-layout case, 28 hostile/unsupported fixtures, profile-data validation and fail-closed execution gate.
One browser integration test passed in Microsoft Edge 155.0.4283.45.

The browser rendered the expected text and CSS. HTML/script-shaped profile data stayed literal text. The fixed adversarial probe could not read parent DOM, document cookies or localStorage; relative and external fetches failed; image and injected-script attempts were blocked. The content server observed only /profile and /probe requests.

These assertions cover the tested paths. They are not a general security proof.

## Remaining risks and incomplete gates

- Docker client is available but the engine was unreachable. No isolated arbitrary-code execution was attempted or verified.
- The local browser test uses different ports on one host, which are different origins but the same site. Separate production content sites and cookie policies still need testing.
- Local parser execution has input/tree limits, not a proven CPU/memory sandbox. Worker budgets, cancellation and adversarial parser stress tests remain required.
- There is no authentication, private preview authorization, database, durable queue, publication pointer, rollback or model integration yet.
- The test CSP permits framing from any ancestor. Production must restrict trusted preview/application ancestors.
- Only Edge was exercised. Mobile behavior, browser compatibility, accessibility and resource-exhaustion behavior remain open.
- One small fixture cannot establish complex-layout latency or behavior under sustained concurrent edits.

## Proposed implementation direction

Use a versioned declarative layout tree plus constrained CSS as the profile artifact, rendered by one shared trusted React runtime. Keep constrained JSX as an optional authoring input that compiles into that tree. Treat arbitrary JavaScript and user dependencies as future capabilities requiring separate threat and cost evaluation.

Preserve the existing revision, validation and artifact-publication lifecycle. Its invariants apply equally to a JSON/CSS artifact and a compiled JavaScript artifact.

Before freezing runtime-v1: define and validate a typed tree schema, support the agreed profile-data fields, settle authoring semantics, add boundary/complexity fixtures, run bounded ingestion workers and implement external immutable runtime delivery. The first full application milestone still requires authenticated revision -> validation -> isolated preview -> publication -> rollback.
