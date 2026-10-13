# OurSpace AI: MVP specification

Status: initial implementation baseline, 2026-10-13.
Source: ../PLAN_LUCRARE_LICENTA.md.
These requirements guide implementation; they do not claim that the design or security controls have been experimentally verified.

## Product outcome

OurSpace is a complete social platform with customizable profiles, friendships, posts/feed and private messaging. The profile flow below is the first delivery slice; the complete product MVP includes the social capabilities defined in [service scope and integration](SOCIAL_PLATFORM_ARCHITECTURE.md).

An authenticated creator describes a profile, receives editable React and CSS source, previews an isolated build, and publishes an accepted artifact. Earlier accepted artifacts remain available for rollback.

## Scope

Required: authentication, resource ownership checks, profiles, immutable revisions, manual source editing, asynchronous builds, isolated preview, compilation and policy checks, bounded AI generation and repair, semantic evaluation, publication, rollback, audit, reproducible local startup, CI and operational telemetry.

Required for the complete product MVP: friend requests/acceptance/removal, blocking, post creation with explicit audiences, an authorized feed, private direct conversations, persisted message history and authenticated message delivery. These are delivered after the profile foundation, as separate services with explicit communication contracts.

Optional extensions: follows, comments/reactions, group chat, advanced feed ranking, end-to-end message encryption, multi-agent generation, predictive ML, custom domains, user-installed dependencies, arbitrary Next.js applications, Kubernetes and artifact signing. Baseline private messaging is required even if end-to-end encryption is deferred.

## Roles and permissions

- Visitor: reads published profiles only.
- Creator/member: manages their own profiles and publication, friend requests, friendships, posts and authorized private conversations.
- Administrator: performs explicitly authorized moderation actions, with an audit trail.
- Worker: uses a narrowly scoped service identity to process jobs and report results.

Resource ownership is checked server-side for every protected operation. Preview access is private by default. Administrative actions do not imply unrestricted execution of user code.

## Functional requirements and acceptance evidence

| ID | Requirement | Acceptance evidence |
|---|---|---|
| AUTH-01 | Authenticate creators and enforce ownership | Anonymous requests fail; creator B cannot read private resources or mutate creator A's resources |
| REV-01 | Save immutable source revisions | Editing produces a new revision; old source remains unchanged |
| JOB-01 | Build revisions asynchronously | Creation returns a job identifier; progress and final results are retrievable |
| BUILD-01 | Compile only the supported runtime contract | Valid fixture builds; unsupported imports and malformed source fail with diagnostics |
| ISO-01 | Isolate build and browser execution | Hostile fixtures cannot obtain platform secrets, contact prohibited network destinations or read the application session |
| VAL-01 | Bind validation to exact build inputs | Changed source, runtime or policy requires a new evaluation |
| PREVIEW-01 | Preview accepted builds privately | An authorized creator sees the selected artifact; another creator cannot request its private preview |
| PUB-01 | Publish only eligible artifacts | Pending, failed, rejected and obsolete-policy builds are refused |
| PUB-02 | Preserve publication during edits and failures | A failed build leaves the published artifact unchanged |
| PUB-03 | Roll back to an eligible previous artifact | Publication points to the selected existing artifact without regenerating it |
| AI-01 | Generate source through a provider adapter | Provider output is validated against the same source contract as manual edits |
| AI-02 | Bound automatic corrections | Attempts, elapsed time, output size and cost budgets terminate the run with an explicit reason |
| SEM-01 | Evaluate prompt coverage | A versioned rubric produces criterion results and an explanatory score for benchmark scenarios |
| AUD-01 | Audit sensitive state changes | Publication, rollback and moderation record actor, target, time and correlation identifier |
| OPS-01 | Run and diagnose the system reproducibly | Documented local startup, health checks, correlated logs and CI build/tests succeed |

Security checks and compilation gate publication. Semantic scoring is advisory initially: an LLM score does not establish security or correctness.

## Full social MVP acceptance

| ID | Requirement | Acceptance evidence |
|---|---|---|
| GRAPH-01 | Request, accept and remove friendships | Only the intended recipient accepts; duplicate/concurrent requests have documented outcomes |
| GRAPH-02 | Block users and enforce interaction restrictions | Blocking prevents new sends and unauthorized content access despite stale projections |
| POST-01 | Create, list and delete owned audience-controlled posts | Ownership and current audience are enforced server-side |
| FEED-01 | Read a paginated authorized feed | Eligible friend posts appear; removal, deletion and privacy changes cannot leak stale content |
| MSG-01 | Create direct conversations and persist messages | Active accepted friends can exchange messages; nonparticipants cannot read history |
| MSG-02 | Authenticate real-time delivery and reconnect | Membership and current send eligibility apply to subscriptions and sends; retry does not duplicate messages |
| INT-01 | Integrate independently deployed services reliably | Outbox/inbox, replay, contract checks and outage scenarios preserve owned state and privacy |

The complete MVP gate includes these requirements, profile/AI requirements and operational acceptance. Completing the first milestone alone does not satisfy it. See [social service architecture](SOCIAL_PLATFORM_ARCHITECTURE.md) for ownership and privacy semantics.

## Nonfunctional baseline

- User source is untrusted, including manually entered and AI-produced source.
- Generated code has no access to application cookies, secrets or privileged APIs.
- Builds have finite CPU, memory, process, output-size and elapsed-time limits; exact enforced values belong to the versioned runtime policy.
- API operations do not wait for builds or model calls to complete.
- Duplicate delivery and worker restart must not publish an artifact twice or corrupt state.
- Publication uses concurrency control; stale client updates receive a conflict.
- Secrets and full prompts/source are excluded from routine logs.
- Source, artifacts and diagnostics have explicit retention policies before public deployment.
- Performance targets are measured and recorded during the first milestone, before being promoted to acceptance thresholds.

## First implementation milestone: manual source to publication

1. Authenticate a creator and create a profile.
2. Submit a fixed sample React/CSS bundle as an immutable revision.
3. Queue and execute its isolated build and validation.
4. Preview the resulting eligible artifact.
5. Publish it, submit a failing revision, and verify publication remains intact.
6. Publish a second accepted artifact, then roll back to the first.

Required negative scenarios: cross-owner access, forbidden imports, network requests, traversal paths, infinite-loop/resource exhaustion, oversized output, stale publication updates, repeated job delivery and interrupted workers.

Exit gate: requirements AUTH-01 through PUB-03 and AUD-01 have recorded evidence in a reproducible local environment. AI requirements follow this milestone. This is a development gate, not a claim of production security.
