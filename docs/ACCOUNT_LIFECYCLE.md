# Account lifecycle and service consistency

Approved implementation direction, 2026-10-20. This milestone precedes profile revisions and publication.

## Purpose and implementation order

This work supports the stability, security and maintainability of the final social platform. It is not required to demonstrate the existing local registration, sign-in and private-profile flow. Demo readiness does not imply that these final-product controls are complete. Keep the architecture and tests suitable for production implementation; do not introduce temporary security bypasses for the demo.

Implement the remaining work in this order:

1. Identity-owned account states, versioned transitions and tests for the lifecycle rules.
2. The internal eligibility endpoint and separate OAuth client-credentials identities for Profiles and the BFF, with replaceable test adapters.
3. Live eligibility enforcement and invalidation of obsolete sessions/tokens, including reactivation behavior and authority outages.
4. RabbitMQ deployment, transactional Identity outbox dispatch and idempotent consumer inboxes/projections.
5. Operator suspension/reactivation commands and password-confirmed self-service deletion requests.
6. Integrated security, concurrency, outage, restart, replay and recovery verification.

These controls are one part of final-product readiness. The broader roadmap also requires visibility enforcement across social services, moderation, retention, observability, backup/restore and measured performance/recovery behavior. Changes to architecture should follow evidence; avoiding all future refactoring is not a guarantee.

## Delivery status

The first increment implements one active profile per account, enforced by PostgreSQL rather than a check followed by an insert. Duplicate creation returns HTTP 409. Migration preserves existing extra profiles as archived rows, keeping the oldest profile active; equal timestamps are ordered by profile ID. Archived rows are excluded from profile lookup. The migration does not delete profiles or their existing outbox records.

The lifecycle controls, authoritative eligibility endpoint, service credentials, RabbitMQ deployment, Identity outbox dispatch and consumer inboxes described below are approved work **not yet implemented**. Existing authentication behavior remains in effect until that increment is delivered and verified. Do not describe the account suspension/deletion guarantees below as available controls yet.

The first increment passes the backend quality gate (7 unit tests and 18 integration tests, including real browser authentication), warning-free analyzers, ESLint and formatting checks. The PostgreSQL tests cover six concurrent creates and upgrading historical duplicate data with equal timestamps.

The real Docker browser scenario also passes, including duplicate-profile rejection, loading the original profile and session survival through container recreation. The local upgrade preserved all five pre-existing profiles (three active, two archived). A pre-upgrade backup was created under the ignored `.local/backups/2026-10-20T11-24-03.838Z` directory.

Archival resolves historical duplicate profiles; it is separate from account suspension or a deletion request. Archived rows are not automatically reactivated. Rolling the migration back removes the archival marker and makes the historical profiles visible again; rollback is an explicit operator decision, not an automatic recovery step.

## Ownership and relationships

Identity owns accounts and their stable GUID identifiers. Profiles owns profile rows referencing those GUIDs. The BFF owns browser sessions. There are no cross-database foreign keys, joins or application credentials granting access to another service's tables. Each service enforces its local constraints and communicates through authenticated contracts.

Profile cardinality is one active profile per account. A filtered unique index on OwnerId where IsArchived is false enforces this under concurrent requests. The database transaction containing profile creation and its outbox record either commits both or neither.

## Approved lifecycle behavior

| State                      | Sign-in                                            | Private profile operations            | Data                              |
| -------------------------- | -------------------------------------------------- | ------------------------------------- | --------------------------------- |
| Active and email confirmed | Allowed, subject to ordinary authentication checks | Allowed for the owner                 | Retained                          |
| Suspended                  | Blocked                                            | Blocked                               | Retained                          |
| Deletion requested         | Blocked                                            | Blocked; profile hidden from delivery | Retained pending erasure workflow |

Suspension can be reversed by an operator. Deletion requested is terminal for this first milestone; cancellation and permanent erasure require a separate retention and recovery decision. Password reset never reactivates a suspended account. Initial operator controls will use an Identity-owned command, not an unauthenticated administration endpoint. Self-service deletion requires a signed-in account, password confirmation and CSRF protection.

Every eligibility-changing operation increments a monotonically increasing account version. Sessions and user tokens carry that version. Returning an account to Active does not restore sessions issued before suspension. Changes use an expected version to reject concurrent stale operator actions. Repeating an already-applied transition is idempotent.

## Authoritative checks

Profiles and the BFF obtain separate service credentials and use Identity's OAuth client-credentials flow to call a minimal internal account-eligibility endpoint. Browser clients cannot request this service scope. Identity validates the intended audience, scope and calling service; its response contains no email or password data.

Private operations check current eligibility and the caller's account version. Missing accounts, inactive status, unconfirmed email and obsolete versions deny access. An unavailable authority returns a service-unavailable result without granting access or deleting otherwise-valid sessions. Do not cache eligibility until a bounded revocation delay has been explicitly agreed. Service access tokens may be cached until shortly before expiry.

These checks are independent of RabbitMQ delivery. A delayed event cannot authorize an operation for a suspended account. There is no distributed transaction across a status check and a Profiles commit; an operation already in flight may finish as suspension occurs. Consumers and delivery checks must still prevent subsequent exposure. Stronger in-flight cancellation guarantees would require another protocol and explicit research.

## Durable events

Identity commits the account change, grant revocation and an outbox event in its own database transaction. Its dispatcher publishes persistent messages with publisher confirms and marks an outbox row processed only after confirmation. An uncertain acknowledgement can cause redelivery; delivery is at least once.

An account-lifecycle event carries event ID, event type, schema version, producer, account ID, account version, UTC occurrence time, correlation/causation identifiers and the resulting status snapshot. It carries no credentials or unnecessary personal data. Full status snapshots allow consumers to handle version gaps without applying stale transitions.

Profiles and BFF have separate durable queues. Each consumer commits its inbox record and local changes atomically, then acknowledges the broker message. Event IDs deduplicate retries; an account-version cursor prevents older events overwriting newer state. The BFF removes only sessions older than the incoming account version, so a delayed suspension event cannot remove a fresh session issued after reactivation. Profiles keeps a local status projection for visibility decisions; it does not own account status.

Malformed or unsupported events go to a dead-letter queue; transient failures have bounded retries and observable failure state. Recovery must cover replay and projection rebuilding. Broker storage is persistent, but a single local container is not a highly available deployment. Backups must address the relationship between processed outbox rows and broker state; restoring only databases must not silently lose previously dispatched events.

## Deployment and test requirements for the remaining increment

- Pin RabbitMQ and its .NET client; persist broker data and use TLS with the generated local CA. Preserve all existing database passwords, signing keys and data-protection keys when upgrading local configuration.
- Give Identity publish permission and each consumer permission only for its own queue. Keep administration credentials out of application configuration.
- Keep lifecycle rules, eligibility checks and event handlers independently testable. Test fakes replace explicit ports in test hosts; production startup must require real dependencies.
- Exercise suspension, reactivation, deletion requests, old-cookie/token replay, authority failure and version conflicts through real authentication.
- Exercise duplicate/out-of-order events, consumer rollback, publisher uncertainty, poison messages, broker outages/restart and recovery with real PostgreSQL and RabbitMQ.
- Back up the current local deployment before applying migrations. Run the full backend gate and real Docker browser scenario before declaring this milestone complete.

References: [RabbitMQ acknowledgements and publisher confirms](https://www.rabbitmq.com/docs/confirms), [OpenIddict flow selection](https://documentation.openiddict.com/guides/choosing-the-right-flow.html).
