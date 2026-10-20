# Implementation roadmap

Baseline: 2026-10-13.

Product scope: a complete social platform with customizable profiles, friends, posts/feed and private messaging. The first profile milestone is one delivery slice, not the complete MVP. Follow [social platform architecture](SOCIAL_PLATFORM_ARCHITECTURE.md) for service ownership, internal modularity, communication and privacy requirements.

## Design requirement: flexibility as capabilities evolve

The project must allow profile capabilities and restrictions to evolve as implementation and research provide evidence. The spike establishes an initial supported subset; its allowlists are not permanent product requirements. A common tree representation does not require every profile to share the same visual structure.

### Boundaries to establish during scaffolding

- Keep authoring inputs separate from the versioned profile tree contract. The constrained JSX adapter translates source into the tree; a future visual editor or structured AI output can target the same contract without changing publication logic.
- Separate tree/schema validation, capability-policy checks, CSS validation, trusted rendering and artifact assembly. Keep these as cohesive modules with explicit inputs and outputs; they need not be separate services.
- Keep capability rules and limits in a versioned policy module. Validators, editor guidance and AI instructions must use the same policy definitions rather than independent copied allowlists. Enforcement remains server-side; client hints are not authorization.
- Keep the shared renderer independent of identity, storage, queue and AI-provider implementations. Profile data crosses an explicit typed boundary and never carries credentials.
- Keep revisions, jobs and publication dependent on artifact metadata and validation outcomes rather than specific JSX tags, CSS rules or renderer internals.
- Introduce abstractions only at these concrete boundaries. Do not build a general plugin framework, dynamic code loading or speculative services.

### Version and compatibility rules

Record source schema, tree schema, runtime, policy and artifact-format versions explicitly. A parser change and a capability-policy change are different changes; neither silently alters an existing artifact.

Published artifacts retain their exact runtime reference and validation provenance. Preserve required supported runtime assets while those artifacts remain eligible. Reinterpretation or migration creates a new revision/artifact and runs the required validation; it never rewrites immutable history. Security revocation can disable unsafe artifacts according to publication policy.

Unknown schema versions and unsupported capabilities fail with explicit diagnostics. Support new and existing versions deliberately; publishing and rollback still require a currently accepted, non-revoked artifact.

### Process for changing a capability

For additions, removals or changed limits:

1. State the user need, proposed behavior and supporting evidence. Identify whether the current restriction is experimental, a product choice or an enforced security boundary.
2. Identify affected schema, parser, renderer, CSS rules, asset delivery, browser policy and validation components.
3. Evaluate security, accessibility, performance/cost and compatibility where relevant. Define acceptance criteria and representative supported/hostile fixtures.
4. Update the versioned contract/policy and the relevant implementation modules together. Add migration behavior only if existing content requires it.
5. Record results and the decision in an ADR and update requirements and editor/AI guidance. Enable the capability only after its required checks pass.

Research may justify relaxing, extending or tightening restrictions. Flexibility means controlled, reviewable changes; it does not permit bypassing checks.

### Architecture acceptance before runtime-v1 is adopted

- Demonstrate that one representative added capability can be implemented through the contract, policy and renderer boundaries without changing revision storage, job lifecycle or publication business rules. Use a real intended capability, not a generic extension framework.
- Verify that existing fixtures keep their documented behavior under their recorded versions.
- Verify that unsupported capabilities and unknown versions are rejected with actionable diagnostics.
- Document which changes require a new policy version, schema version, runtime artifact or content migration.

## 0. Review the baseline

Read MVP_SPECIFICATION.md, ARCHITECTURE_DECISIONS.md and DOMAIN_AND_API_CONTRACTS.md together. They narrow the original thesis plan into an executable first milestone. Open decisions are tracked explicitly; none should be interpreted as verified security controls.

## 1. Prove the runtime and isolation boundary

Create a minimal fixed Profile.tsx/styles.css fixture, a trusted runtime entry point and a versioned source policy. Compare available isolation mechanisms against ADR-004. Record successful build and render evidence plus hostile-fixture results.

Exit: the supported source surface, enforced limits and build/preview isolation are demonstrated. Stop and revise the design if the host cannot enforce them. No LLM integration or public deployment is needed.

## 2. Scaffold the deployable application

Proposed monorepo:

- apps/web: trusted Next.js interface.
- services/profiles: .NET profile/revision/publication API; independently owned data.
- services/identity: authentication and account lifecycle boundary.
- services/social-graph: friend requests, friendships and blocking.
- services/content: posts and audience ownership.
- services/feed: timeline projections and authorized feed queries.
- services/messaging: conversations, message persistence and real-time delivery.
- services/worker: isolated-build orchestration and validation.
- runtime/contracts: versioned profile-tree, source and capability-policy contracts.
- runtime/authoring: constrained JSX-to-tree adapter, independent of the renderer.
- runtime/validation: schema, capability and CSS validation modules.
- runtime/renderer: shared trusted React renderer with versioned artifacts.
- runtime/profile-spike: retained experiment and fixtures; production modules are extracted deliberately.
- contracts: service-owned OpenAPI, integration event schemas and profile-runtime schemas; no shared domain entities.
- infra: reproducible local services and deployment configuration.
- tests: integration, contract, browser and security fixtures.
- docs: requirements, ADRs, threat model and acceptance evidence.

Implement services in the sequence below, with explicit application interfaces and infrastructure adapters from their first use. Define API/event ownership and outbox/inbox conventions during scaffolding; do not create empty placeholder services. Social graph, content, feed and messaging are required separate service boundaries, not optional future extraction.

Pin compatible toolchains after checking their official support and compatibility information. Add CI build, meaningful critical-path tests, dependency checks and reproducible local startup. Avoid empty service shells and generic abstractions without a concrete consumer.

## 3. Deliver the manual publication milestone

Implement authentication and ownership, PostgreSQL migrations, revisions, durable jobs, worker leases, artifact persistence, validation, preview, publication concurrency and audit. Build only the UI needed to exercise these behaviors.

Record evidence against AUTH-01 through PUB-03 and AUD-01. Include duplicate delivery, lease expiry, stale worker completion and stale publication tests.

## 4. Add AI through the existing lifecycle

Implement one generation provider adapter, structured source output, a recorded benchmark and semantic rubric. Repairs create child revisions and use the same build/validation pipeline. Enforce budgets and stopping criteria; compare initial generation with bounded repair on the same scenarios.

## 5. Deliver the social platform services

Implement the social graph service first (friend requests, accepted friendships, removal and blocking), then content ownership and post audiences. Build the feed service as a durable projection with current visibility enforcement. Implement private direct messaging as an independent service with membership checks, persisted history, deduplicated sends and authenticated real-time delivery/reconnect.

Each service separates domain rules, application use cases, persistence/integration adapters and HTTP/event entry points. Use synchronous contracts for authoritative authorization and asynchronous events for propagation. Record API/event versions and verify contract compatibility before integration.

Exit: GRAPH-01/02, POST-01, FEED-01, MSG-01/02 and INT-01 pass. Include stale-event privacy tests, blocked/unfriended sender scenarios, nonparticipant access, duplicate delivery, projection rebuild, partial failure and independent service restart. Social features stay in trusted application UI; the generated profile runtime never receives privileged social credentials.

## 6. Complete the thesis MVP and evaluate

Complete integrated acceptance across profiles, friendships, feed and private messaging. Improve editor UX, history, accessibility, moderation, backup/restore, retention and telemetry. Run functional, security, performance and recovery experiments. Document failures, costs and limitations.

Select optional research extensions only after the baseline is measured. Any requirement or architecture change records its rationale, affected contracts and updated acceptance evidence.

## Immediate next coding task

The runtime and first backend foundation are implemented; see [RUNTIME_V1_CONTRACT.md](RUNTIME_V1_CONTRACT.md) and [BACKEND_FOUNDATION.md](BACKEND_FOUNDATION.md). OpenIddict and a dedicated .NET BFF are approved. The approved account flows and BFF session implementation are documented in [AUTHENTICATION.md](AUTHENTICATION.md). Next implement the remaining [account lifecycle and service consistency](ACCOUNT_LIFECYCLE.md) controls in their documented order, starting with Identity's state model and versioned transitions. This is final-product foundation work and does not block demonstrating the existing local account/profile flow. After verification, implement owned profile revisions and their API contracts, then durable validation jobs, private preview, publication and rollback. Consult the user before making new product or architecture decisions. Follow social-service integration contracts from the start; arbitrary user JavaScript remains disabled.

## Runtime experiment results

The [runtime spike](RUNTIME_SPIKE_REPORT.md) demonstrates a constrained layout rendered through a shared React runtime, with 31 policy tests and one browser integration test passing. Arbitrary-code server isolation is unverified and remains disabled.

The spike informed the runtime-v1 baseline implemented below. The next work is the authenticated manual-publication flow, including bounded ingestion workers and private preview authorization. Neither the spike nor runtime-v1 completes the arbitrary-code sandbox gate or the full manual-publication milestone.

## Runtime-v1 implementation baseline

The runtime now separates typed schemas/policy, constrained JSX parsing, validation, React rendering and versioned artifact assembly. The sections capability demonstrates extension through those boundaries. Root quality checks enforce strict typing, module import rules, formatting, generated-schema consistency, security/contract fixtures, spike regression and browser/accessibility behavior. See [the contract](RUNTIME_V1_CONTRACT.md) for exact semantics and limits. This is not completion of the authenticated publication or social platform milestone.

## Backend foundation delivered

The identity EF store/migrations and modular profiles create/read API now exist. ICurrentUser and test-only authentication enable fast tests without disabling ownership checks. Separate PostgreSQL databases, initial migrations and atomic outbox persistence are covered by the backend quality gate. Account forms, OpenIddict and a dedicated BFF now provide the authentication slice. Next.js application pages and the publication lifecycle remain incomplete; do not mark the full manual-publication milestone complete.

## Local deployment foundation

The approved local-only deployment uses Caddy HTTPS, Mailpit STARTTLS capture, separate PostgreSQL stores, explicit migrations and limited runtime roles, encrypted persistent key rings, readiness checks, cleanup and backup/restore tooling. See [LOCAL_DEPLOYMENT.md](LOCAL_DEPLOYMENT.md). Next.js and internet hosting remain separate work.

The immediate next milestone is [account lifecycle and service consistency](ACCOUNT_LIFECYCLE.md): one active profile per account, staged suspension/deletion, authenticated authoritative account checks, and reliable RabbitMQ events with outbox/inbox processing. The document distinguishes implemented constraints from pending controls. Complete and verify this milestone before proceeding to owned profile revisions and the validation/publication lifecycle.
