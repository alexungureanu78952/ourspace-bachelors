# Architecture decisions

Status: initial baseline, 2026-10-13. Revisit decisions when evidence changes; record reason, impact and superseding decision.

## ADR-001: Separate platform code from profile code

Decision: Next.js provides the trusted application interface; .NET provides the application API; PostgreSQL persists application data. User profiles target a controlled React runtime rather than arbitrary Next.js projects.

A source bundle contains a manifest, one Profile.tsx module and one styles.css file. Profile.tsx exports a default component receiving a versioned, read-only ProfileData value. It may import only explicitly allowlisted runtime modules. The builder supplies a fixed entry point, dependencies and configuration; creators cannot change install scripts, package versions, build plugins or server code.

The initial runtime disallows external scripts, remote assets, arbitrary network access, browser persistence and platform API calls. Broader capabilities require an explicit contract and threat-model update. React is a rendering interface, not a security boundary: runtime restrictions require static checks plus enforced isolation and browser policy.

Consequence: smaller supported surface and repeatable builds; some designs and interactions will be unsupported. A compatibility spike must verify parsing, CSS policy, bundling and runtime behavior before runtime-v1 is frozen.

## ADR-002: Deploy application API and worker separately

Decision: the first delivery slice begins with a web application, profile application API and independently deployed build/validation worker. The full platform additionally requires independently deployed social graph, content, feed and messaging services, plus an identity boundary, as specified in [social platform architecture](SOCIAL_PLATFORM_ARCHITECTURE.md). Each service separates domain, application, infrastructure and transport responsibilities. Give identity/access, profiles/revisions, generation/jobs and publication explicit internal module ownership. Shared business logic must not depend on worker or web framework details.

The API is the authenticated entry point; a dedicated gateway is unnecessary until routing or operational requirements justify it. The worker processes untrusted inputs outside the API process. AI generation uses a provider adapter and produces the same source bundle as manual editing.

Use one PostgreSQL host initially if useful locally, with service-owned databases/schemas, credentials and migrations. Cross-service SQL access and shared business tables are prohibited. Within a service, modules own their tables. The API owns authoritative application state; workers claim jobs and submit results through an internal authenticated API, rather than modifying unrelated tables. Store artifact payloads behind an artifact-store interface.

A durable database-backed job queue is sufficient initially. Job creation and revision persistence share one transaction. Claims use leases and fencing tokens; expired claims can be retried. A message broker remains optional. The required social services communicate through owned versioned APIs/events; additional service splits beyond these boundaries require a documented need.

Scope clarification: API and worker are only the initial profile subsystem. Friends, posts/feed and private messaging are required product services; baseline messaging is distinct from optional end-to-end encryption. ADR-008 and SOCIAL_PLATFORM_ARCHITECTURE.md govern their integration.

## ADR-003: Immutable revisions and artifact-based publication

Decision: source revisions are immutable. Every build records the revision, source digest, runtime version, dependency-lock digest and policy version. A successful result produces an immutable artifact and artifact digest.

The publication record points to an eligible artifact. Editing creates another revision and never mutates publication. Rollback is a new audited publication change referencing an existing eligible artifact.

Build success and policy acceptance are separate facts. Manual edits, model repairs and retries do not bypass either check. A policy change may make an old artifact ineligible for new publication; emergency revocation can also disable an existing publication.

Consequence: additional storage, but traceable validation, predictable rollback and reproducible experiments.

## ADR-004: Treat isolation as an initial requirement

Decision: execute builds in ephemeral isolated environments without secrets, host filesystem mounts, container-engine access or outbound network. Supply dependencies from a prebuilt trusted image, use a read-only base filesystem, bounded temporary storage, an unprivileged identity and enforced resource/time limits.

The worker orchestrates execution; user source is never loaded into the API or worker host process. A container alone is not assumed to be an adequate adversarial-code sandbox. Select and verify a suitable isolation mechanism during the spike. Unsupported local isolation blocks execution instead of silently falling back to host builds.

Preview and published artifacts use a different site boundary from the authenticated application, with no shared authentication cookies. Browser rendering uses restrictive CSP and a sandboxed iframe without same-origin permission. Start with a trusted preview host fetching the authorized artifact and rendering it without exposing its credential to user code. Define any postMessage protocol with exact sender/source checks and an explicit schema.

Artifact serving must enforce authorization for private previews, safe paths, MIME types, response headers and size limits. Public artifacts receive no platform session. Threat fixtures verify behavior; scanners and semantic scores are additional checks, not isolation guarantees.

## ADR-005: Bound and observe asynchronous work

Decision: jobs have explicit lifecycle states, retry classes, leases and execution budgets. Retries apply only to transient infrastructure failures; invalid source is a terminal result. AI repair creates a new revision and records its parent, prompt/rubric/model configuration and usage metadata.

Define per-run attempt, time, token/cost and output-size budgets before enabling a real provider. Cancellation and lease loss prevent a worker from committing stale results. Correlation identifiers connect API requests, jobs, build attempts and audit events.

Consequence: more lifecycle logic, but controlled costs and recoverable work. Do not create a multi-agent framework until a measured baseline justifies it.

## Decisions still requiring implementation evidence

- Exact supported framework/toolchain versions and lockfiles.
- Isolation backend available on the development and demonstration hosts.
- Runtime-v1 AST/CSS rules, numeric execution limits and malicious-fixture results.
- Identity provider/session implementation and cookie policy.
- Artifact storage and credential-free isolated preview delivery.
- Semantic rubric, benchmark prompts and model budgets.
- Retention, account deletion and backup/restore procedures.

Resolve the first five before declaring the manual publication milestone complete. Resolve remaining deployment and AI policies before their features are enabled.

## ADR-006: Proposed constrained layout artifact after runtime spike

Status: provisional direction, supported by the local spike; runtime-v1 is not frozen.

The [runtime spike report](RUNTIME_SPIKE_REPORT.md) records a supported declarative TSX -> JSON tree -> trusted React renderer path. Source is parsed as a layout notation rather than executed as a React program. A shared renderer can avoid bundling dependencies per profile revision; immutable artifacts still preserve revision/validation/publication invariants.

For the constrained path, replace arbitrary-program compilation with source-policy parsing, typed layout-schema validation and trusted artifact assembly. Keep CSS validation and isolated browser rendering. Do not assume source imports, hooks, loops or event handlers are supported. Unrestricted React remains disabled until a verified execution sandbox exists.

This qualifies ADR-001's code-generation surface: React describes the rendering technology, while the default user capability is a declarative subset. It does not supersede ADR-004's requirements for any future executable-code path. The spike's localhost origins, local parser limits and embedded runtime are experimental; see its remaining gates before treating this as a production runtime.

## ADR-007: Evolve profile capabilities through explicit versioned boundaries

Decision: the implementation must separate authoring adapters, the profile-tree contract, capability policy, validation, trusted rendering and artifact assembly. The spike's supported subset is an initial evidence-backed constraint, not a permanently fixed feature set or a mandatory visual template.

Follow the [flexibility requirements and capability-change process](IMPLEMENTATION_ROADMAP.md#design-requirement-flexibility-as-capabilities-evolve) when adding, removing or changing capabilities. Revisions and publication depend on versioned artifacts and validation provenance, not renderer-specific element rules. Preserve immutable history and deliberate version compatibility; security revocation remains available.

Consequence: local module boundaries and explicit version metadata are required from scaffolding. General plugin systems and speculative service abstractions are not required. Restrictions can evolve with research, but changes must include applicable acceptance evidence before being enabled.

## ADR-008: Complete social platform with independently owned modular services

Decision: friendships, posts/feed and private messaging are required product scope. The profile subsystem is delivered first; it is not the full MVP. Define social graph, content, feed and messaging as separate service boundaries and give each service domain/application/infrastructure/transport modules.

Follow [social platform architecture](SOCIAL_PLATFORM_ARCHITECTURE.md) for authoritative ownership, API/event contracts, outbox/inbox processing, service authentication and visibility enforcement. Shared local infrastructure does not permit direct cross-service database access. Social integrations are hosted in trusted application UI, not privileged generated profile code.

This supersedes ADR-002's earlier suggestion that the API/worker decomposition could describe the full system or that social services were merely optional later splits. Public profile rendering must remain independent of messaging/feed availability. Baseline private messaging is required; end-to-end encryption remains separately evaluated.

## ADR-009: Testable identity and profile-service foundation

Decision (2026-10-20, agreed with the user): use dedicated ASP.NET Core Identity persistence, .NET 10, EF Core/PostgreSQL, stable user IDs and a future HttpOnly browser session through a trusted backend. Application operations receive an ICurrentUser abstraction; production HTTP code adapts validated claims. Test authentication belongs exclusively to test assemblies; authorization and ownership rules are never bypassed.

Profiles and identity own separate databases and migrations. Real PostgreSQL integration tests complement fake-store unit tests. The first delivered behavior is create/read private profiles with server-derived ownership and an atomic outbox record.

The profiles API's JWT validator is a provider-neutral boundary, not a choice of token server or a completed login system. The user subsequently approved OpenIddict and a dedicated .NET BFF in ADR-010; account-flow behavior is now approved in ADR-011. See [backend foundation](BACKEND_FOUNDATION.md) for implemented behavior, validation and remaining gaps.

## ADR-010: Self-hosted OpenIddict and a dedicated .NET BFF

Decision (2026-10-20, explicitly approved by the user): use OpenIddict alongside ASP.NET Core Identity and implement browser sessions in a dedicated ASP.NET Core BFF. Next.js remains the UI. The user accepts the additional implementation/maintenance effort in return for control and learning value.

Tokens remain server-side and services retain their own authorization checks. Preserve the existing test adapters. Follow [the authentication implementation plan](AUTHENTICATION_IMPLEMENTATION_PLAN.md) for the first real sign-in milestone and its acceptance checks. Registration/verification behavior and production operational details are not implicitly approved by this technology decision.

## ADR-011: Confirmed-email accounts and replaceable delivery

Decision (2026-10-20, approved by the user): public email/password registration requires confirmed email before login; support password reset and account lockout. Deliver links through IAccountEmailSender, with SMTP for actual delivery and a captured sender in tests. Keep real account policy enabled in tests. The dedicated BFF stores encrypted session tickets in its own PostgreSQL database; only protected session references reach browser cookies. See [authentication policy and deployment limits](AUTHENTICATION.md).

## ADR-012: Reproducible local Docker deployment

Decision (2026-10-20, approved by the user): deploy locally using Caddy as the HTTPS gateway and Mailpit as the captured mailbox. Persist separate encrypted Identity/BFF key rings and databases; apply schema migrations through a one-shot privileged job and run services with limited database roles. Generate ignored credentials and certificates rather than embedding secrets in images or source. The user explicitly approved disabling SMTP revocation-list checks only for the generated local CA in LocalDocker, because that CA has no CRL service. Keep TLS, hostname and CA-chain validation; reject that setting in production. See [local deployment](LOCAL_DEPLOYMENT.md) for operational scope and verification.
