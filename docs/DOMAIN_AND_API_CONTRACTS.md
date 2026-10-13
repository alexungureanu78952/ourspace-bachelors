# Domain and API contracts

Status: draft implementation contract, 2026-10-13.
REST paths are relative to /api/v1. Implementation must publish machine-readable OpenAPI; this document specifies behavior, not a substitute OpenAPI file.

## Platform service context

The records and endpoints below describe the profile subsystem only. OurSpace also requires identity, social graph, content, feed and private messaging boundaries; see [service ownership and integration contracts](SOCIAL_PLATFORM_ARCHITECTURE.md). Each service owns its domain records, API and integration-event schemas. No shared database model is implied by the common /api/v1 prefix; a trusted entry point routes requests to the owner.

Use stable user/profile IDs across contracts. Model friend requests, accepted friendship/block state, posts/audiences, feed projections, conversations/participants and messages/delivery cursors in their owning services. Define concrete wire schemas before implementing each service. Integrations require authenticated typed clients, versioned event envelopes, outbox/inbox reliability and explicit current-authorization semantics. The profile artifact contract does not grant social API access.

## Records and invariants

| Record           | Required fields and ownership                                                                                                                                          |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Profile          | id, ownerId, slug, displayName, createdAt; profiles module                                                                                                             |
| Revision         | id, profileId, parentRevisionId?, sourceBundle, sourceDigest, origin(manual/ai/repair), createdBy, createdAt; revisions module                                         |
| BuildJob         | id, revisionId, status, runtimeVersion, policyVersion, attemptCount, leaseToken?, leaseExpiresAt?, cancellationRequested, createdAt; jobs module                       |
| BuildAttempt     | id, jobId, attemptNumber, toolchain/image digest, timestamps, outcome, diagnostics, resource usage; jobs module                                                        |
| Artifact         | id, revisionId, successfulBuildAttemptId, sourceDigest, runtimeVersion, policyVersion, dependencyLockDigest, artifactDigest, storageKey, createdAt; publication module |
| ValidationReport | id, buildAttemptId, status, policyVersion, diagnostics, semanticEvaluation?; validation module                                                                         |
| Publication      | profileId, artifactId?, version, updatedAt; publication module                                                                                                         |
| AuditEvent       | id, actorId, action, resourceId, previousArtifactId?, newArtifactId?, correlationId, timestamp; audit module                                                           |

Identifiers are opaque UUIDs. Timestamps are UTC ISO 8601. SHA-256 digests identify canonical bundle bytes or exact artifact bytes, not arbitrary object serialization. Specify canonical UTF-8 encoding, normalized file paths and deterministic manifest serialization in runtime-v1 before hashing is implemented.

Revision source and accepted artifacts are immutable. Job scheduling fixes runtime and policy versions. Results bind to one attempt and its complete input fingerprint. An artifact is eligible only when compilation succeeded, all required validations passed, it is not revoked, and its policy is currently accepted for publication.

## Source bundle v1

The implemented wire format and exact authoring semantics are defined in [RUNTIME_V1_CONTRACT.md](RUNTIME_V1_CONTRACT.md); that document supersedes the earlier draft field sketch below where they differ.

Manifest fields: schemaVersion, runtimeVersion, policyVersion, entryPoint (fixed Profile.tsx).
Files: Profile.tsx and styles.css only. No arbitrary paths, binary files, symlinks, dependencies or build configuration. Limits and import/CSS rules belong to runtime policy.

ProfileData v1: schemaVersion, displayName, bio and sections (stable id, heading, plain-text body). Treat content as data; do not inject it as raw HTML. Link, image and interaction capabilities are deferred until their validation and serving rules are defined.

## Job state machine

queued -> running -> succeeded | failed | rejected | cancelled

- failed: infrastructure/build failure; diagnostics distinguish transient infrastructure from invalid source.
- rejected: compilation may succeed, but required policy validation fails.
- cancellation while queued is immediate; while running, record the request and stop execution.
- a transient failure may schedule another bounded attempt; each attempt is retained.
- an expired lease permits a new claim under the same job, up to the retry budget.
- fencing tokens prevent expired workers from committing results.
- succeeded requires an artifact and passing required validation report committed atomically.
- terminal states do not transition back to running; an explicit user retry creates a new job.

AI generation and repair runs have their own bounded orchestration records; they create revisions and build jobs rather than modifying existing source.

## Creator-facing endpoints

| Method and path                                              | Behavior                                                                                  |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------------------- |
| POST /profiles                                               | Create an owned profile; 201                                                              |
| GET /profiles/{profileId}                                    | Read an owned profile and publication metadata                                            |
| POST /profiles/{profileId}/revisions                         | Validate bundle shape and append an immutable revision; 201                               |
| GET /profiles/{profileId}/revisions                          | List owned revision metadata with cursor pagination                                       |
| GET /profiles/{profileId}/revisions/{revisionId}             | Retrieve owned source                                                                     |
| POST /profiles/{profileId}/revisions/{revisionId}/build-jobs | Schedule build; 202 with job id and status URL                                            |
| GET /build-jobs/{jobId}                                      | Read authorized status, attempt summaries and diagnostics                                 |
| POST /build-jobs/{jobId}/cancellation                        | Request cancellation; idempotent                                                          |
| POST /artifacts/{artifactId}/preview-sessions                | Create short-lived authorized preview access; no credential is passed into generated code |
| PUT /profiles/{profileId}/publication                        | Set artifactId, or null to unpublish; requires If-Match publication version               |
| GET /profiles/{profileId}/publication                        | Return artifact reference and ETag for initial and subsequent updates                     |

Rollback uses the publication endpoint with a previous eligible artifactId. The API verifies artifact/profile ownership, acceptance and expected publication version in one transaction and records audit alongside the update. A new profile has an explicit unpublished publication version.

Public artifact delivery is separate from the authenticated API and must resolve the current publication; private storage paths are never exposed as authorization.

## Cross-cutting API behavior

- Derive actor identity from authentication; ignore client-supplied owner ids.
- Return 401 for unauthenticated access and 404 for resources outside the actor's visibility.
- Use application/problem+json errors with type, title, status, detail, instance, code and correlationId. Never return secrets or raw infrastructure exceptions.
- Use 400 for malformed contracts, 422 for unsupported source-bundle policy, 409 for invalid lifecycle operations, 412 for stale If-Match, 428 for missing If-Match and 429 for rate limits.
- Build diagnostics use code, severity, file?, line?, column?, message and recommendation?; do not expose host paths.
- Mutating POST operations accept an Idempotency-Key scoped to actor and operation. Persist request fingerprint and result atomically; conflicting payload reuse returns 409. Document retention and in-flight behavior in OpenAPI.
- Propagate correlation identifiers; enforce request and source size limits before expensive processing.
- Worker endpoints are internal and authenticated. Claim, heartbeat and completion require job/attempt identifiers plus the fencing token. Result submission is idempotent and cannot change a terminal result.
