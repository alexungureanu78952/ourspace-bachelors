# Social platform scope and service integration

Status: product baseline, 2026-10-13. This document is normative for service boundaries; wire schemas and technology choices still require implementation review.

## Product and delivery scope

OurSpace is a complete social platform with customizable profiles, friendships, posts/feed and private messaging. These are required product capabilities, delivered incrementally. The profile-runtime spike and manual-publication milestone validate one subsystem; neither is the complete product MVP. End-to-end encryption, advanced feed ranking, group chat and notifications beyond baseline message delivery are separate scope decisions.

## Service ownership

| Service | Authoritative data and responsibilities | Communication |
|---|---|---|
| Identity | Stable user IDs, authentication, account status and service credentials | Authenticated identity/status contracts; account lifecycle events |
| Profiles | Public identity presentation, profile privacy, revisions, validation jobs and publication references | Profile/publishing queries; publication and visibility-change events |
| Social graph | Friend requests, accepted friendships, removal and blocking | Relationship/interaction authorization queries; relationship-change events |
| Content | Posts, author ownership, audience and deletion | Authorized post queries; post creation/change/deletion events |
| Feed | Per-viewer timeline projections, ordering, cursors and rebuild checkpoints | Consumes graph/content/profile events; checks current visibility before returning content |
| Messaging | Direct conversations, participant membership, messages, ordering, delivery/read cursors and connection authorization | Checks graph/account eligibility; participant-only APIs and authenticated real-time delivery |
| Generation/validation worker | Bounded generation and artifact-processing attempts | Internal job claim/result contracts; no direct social-service data access |

Each social domain above is an independently deployable service boundary. One PostgreSQL host may support local development, but each service owns its database/schema, migrations and credentials. No cross-service SQL joins, foreign keys or direct table writes. Cross-service references use stable IDs; each owner defines deletion/tombstone semantics.

The trusted web application composes these capabilities through authenticated APIs, optionally behind an entry-point/BFF. A browser route boundary does not establish service authorization. Start with explicit routing; choose gateway tooling only when needed.

## Internal modularity required in every service

- Domain: entities, value types and business rules; no transport, database, broker or other service implementation dependencies.
- Application: use cases, transaction boundaries, resource authorization and interfaces for required collaborators.
- Infrastructure: persistence, outbox/inbox, typed service clients, broker and real-time adapters.
- API/consumers: validated HTTP requests, internal requests and event handlers invoking application use cases.
- Contracts: owned API/event schemas, documented compatibility and contract fixtures.

Physical project count can follow actual complexity. Preserve dependency direction without creating empty layers. Share transport conventions, not a shared domain model or a library of every service's database entities. Consumers reference documented contracts rather than importing producer implementations.

## Synchronous and asynchronous integration

Use synchronous requests for user commands, authoritative reads and current authorization decisions. Use asynchronous integration events for propagation and feed projections. Do not turn service-to-service calls into a chain on every profile render.

An event envelope contains eventId, eventType, schemaVersion, occurredAt, producer, aggregateId, aggregateVersion, correlationId, causationId and a minimal payload. Actor identity is included only when necessary; no credentials or private-message bodies in general integration events. Recipient-specific message delivery stays inside the authorized messaging boundary.

A service commits its state and outbox event in one local transaction. Publishers deliver at least once. Consumers use an inbox/deduplication record and apply their local change atomically. Acknowledge only after durable processing. Define ordering per aggregate, stale-version handling, bounded retries, dead-letter handling, replay and reconciliation. There is no assumed global transaction or exactly-once delivery.

Candidate event families:
- AccountStatusChanged / AccountDeletionRequested.
- ProfilePublished / ProfileVisibilityChanged.
- FriendRequestCreated / FriendshipEstablished / FriendshipRemoved / UserBlocked / UserUnblocked.
- PostCreated / PostAudienceChanged / PostDeleted.

Names and payloads are provisional; schema review must define aggregate ordering and authorization implications. Message content must not be broadcast through these events.

Typed clients use service authentication, scoped authorization, correlation IDs, deadlines and bounded retries for safe/idempotent operations. Avoid forwarding user tokens indiscriminately. A service authenticates both the caller and any user context relevant to the operation.

## Privacy and consistency

Feed projections can be eventually consistent for appearance and ordering. Privacy decisions cannot rely solely on a stale projection. Before serving an item, verify its current audience, relationship and account eligibility through authoritative contracts or a documented versioned authorization mechanism with equivalent revocation guarantees. If eligibility cannot be established, withhold the item. Apply the same rule to cached pages and pagination.

Default baseline: a direct conversation is established and a new message is accepted only between active, accepted friends with no block in either direction. Unfriending or blocking prevents new sends; existing participants retain their prior history unless deletion/moderation policy requires otherwise. Nonparticipant history access is always denied. Broader message-request rules are a future explicit product change.

The social graph service owns the combined relationship/block eligibility decision; messaging must not reconstruct it from unrelated event copies. Specify the authorization decision's consistency point and concurrent block/send behavior before implementation. Membership, friendship and block checks apply to HTTP and real-time operations, reconnect and subsequent sends; an authenticated socket alone grants no conversation access.

Private posts cannot be baked into publicly served profile artifacts. Initially show friends, feed and messaging controls in trusted application UI around the isolated profile. Future profile widgets receive only explicitly approved data/actions through a validated host protocol; generated layouts never receive session tokens or direct unrestricted social API access.

## Integration acceptance

- Friendship acceptance makes eligible friend posts appear in the viewer's feed.
- Friendship removal, block, post privacy change and deletion stop unauthorized visibility even when projections lag.
- Only conversation participants read history; only currently eligible senders create/deliver new messages.
- Duplicate or out-of-order events do not duplicate feed items or reverse newer state.
- A service restart resumes durable integration work; a feed projection can be rebuilt from documented owner snapshots/events.
- Identity/account deletion propagates through a documented workflow with service-owned retention, acknowledgment and retry behavior.
- Messaging/feed outages do not prevent rendering an already published public profile.
- Internal endpoints and real-time subscriptions reject unauthorized identities and resource access.
- Contract compatibility is checked when producers or consumers change.
- Cross-service workflows have correlation IDs and explicit partial-failure behavior.

## Decisions to resolve before social service implementation

Define post audiences and editing semantics, friend-request conflict behavior, block/send race handling, account deletion/history retention, message size/rate limits, pagination guarantees, outbox/broker/replay transport and concrete latency/availability targets. Record these in owned contracts and ADRs. Baseline private messaging is required; end-to-end encryption remains a separately evaluated extension.
