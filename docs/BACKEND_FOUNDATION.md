# Backend foundation

Status: initial implementation slice, 2026-10-20. This is not the full authentication or publication milestone.

## Agreed decisions

- .NET 10 services, EF Core and PostgreSQL.
- Dedicated ASP.NET Core Identity service with its own database; profiles owns a separate database.
- Browser authentication uses an HttpOnly session through the dedicated BFF.
- Application operations depend on ICurrentUser, not cookies, tokens or HttpContext.
- Unit tests supply a fake current user; API integration tests register a test-only authentication handler. Real ownership rules stay enabled.
- The user approved OpenIddict and a dedicated ASP.NET Core BFF; public registration, email confirmation and password reset are also approved.

The profiles API validates JWTs issued by the identity service. OpenIddict supplies the protocol server; the separate BFF manages browser cookies and server-side tokens. See [AUTHENTICATION.md](AUTHENTICATION.md) for account flows and configuration.

## What is implemented

Identity contains IdentityUserAccount with stable GUID identifiers, an EF context, migrations and shared identity policy registration. ASP.NET Core Identity handles hashing. The configured baseline requires unique email, a 12-character minimum password and lockout settings. Registration, confirmed-email login, recovery, SMTP delivery, OpenIddict issuance and BFF sessions are implemented as the next authentication slice.

Profiles separates Domain, Application, Infrastructure and API projects. Domain validates names and ownership. Application creates/retrieves profiles through IProfileStore and ICurrentUser. Infrastructure persists through EF Core; API adapts authentication and HTTP.

Endpoints:

- POST /api/v1/profiles/ creates an owned private profile; 201 and Location.
- GET /api/v1/profiles/{id} returns that profile to its owner only.
- Anonymous requests receive 401; authenticated principals without a usable nonempty GUID sub receive 403.
- Another owner's profile and an absent profile both return 404.
- The owner is derived from authentication; submitted ownerId cannot choose another owner.
- Invalid names receive 400 Problem Details.
- /health/live and /health/ready are anonymous probes; readiness checks database connectivity, not complete migration/application readiness.

The profiles API rejects missing/insecure issuer configuration at startup. JWT validation requires the configured issuer/audience, a trusted signature, lifetime and HTTPS metadata. A 30-second clock skew is configured. No test header, development bypass or fake authentication switch exists in production code.

Profile creation and a minimal outbox record commit atomically. The record includes event ID/type/schema, producer, aggregate ID/version, correlation ID, optional causation ID and a minimal profile/owner payload. Root commands have no event causation ID yet. This is an internal foundation record, not a public feed signal. Dispatch, consumer inboxes, event replay and complete wire-contract review are not implemented.

## Configuration and running

The services read configuration from environment variables or standard .NET configuration providers. Do not commit credentials.

Profiles:

- ConnectionStrings__Profiles: service-owned PostgreSQL connection.
- Authentication__Authority: the identity issuer's HTTPS URL.
- Authentication__Audience: intended API audience.

Identity:

- ConnectionStrings__Identity: its separately owned PostgreSQL connection.

Use dotnet run --project services/profiles/src/OurSpace.Profiles.Api or the corresponding identity API project after configuration. There is deliberately no working fake-login development endpoint.

The runnable Docker deployment is defined in infra/compose.yml and documented in [LOCAL_DEPLOYMENT.md](LOCAL_DEPLOYMENT.md). It generates ignored credentials, mounts per-service secrets, uses pinned images, runs migrations separately, and starts HTTPS routing and a STARTTLS local mailbox. Database ports bind only loopback. Use npm run local:up; environment-supplied database passwords are no longer required by Compose.

Migrations are explicit, not applied on application startup:

```powershell
dotnet tool restore
dotnet ef database update --project services/profiles/src/OurSpace.Profiles.Infrastructure
dotnet ef database update --project services/identity/src/OurSpace.Identity.Infrastructure
dotnet ef database update --project services/bff/src/OurSpace.Bff.Infrastructure
```

These commands read the corresponding ConnectionStrings__Profiles / ConnectionStrings__Identity / ConnectionStrings__Bff variable through the design-time factories. Without it, the factories use localhost database/user names without a password; they do not connect during migration generation. Apply migrations only against the intended service-owned database, using an appropriate migration identity. Production migration review/privilege separation remains required.

## Tests and quality

The portable backend gate is npm run check:backend. It requires OURSPACE_TEST_POSTGRES and performs locked restore/dependency auditing, a warning-free analyzer build, formatting verification, unit tests and real PostgreSQL integration tests. No database test is silently skipped.

The test connection must point to a dedicated loopback PostgreSQL instance with Database=ourspace_test_admin and permission to create/drop databases. Tests create only random ourspace_test_<guid> databases, use separate databases for identity/profiles/BFF, apply actual migrations and remove those generated databases afterward. Never supply production credentials.

Windows, with PostgreSQL 18 binaries installed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-backend-local.ps1
```

This script creates a separate password-protected cluster under ignored .local/, chooses a free loopback port, runs the entire backend gate, shuts down the cluster and removes only its verified temporary directory. The command's execution-policy override applies to that invocation. It does not reconfigure the installed PostgreSQL service. It needs Node, the installed PostgreSQL initdb/pg_ctl/createdb tools and a Playwright browser. The Windows default is Edge; elsewhere install Chromium with npx playwright install chromium.

CI uses the same gate with an ephemeral PostgreSQL service. npm run check continues to check the TypeScript runtime; npm run check:all runs both gates and requires the test database environment.

Coverage includes:

- Domain name/identifier boundaries, current-user ownership and anonymous rejection.
- Owner API access, foreign-user denial, persistence through a new API host and rejected owner spoofing.
- Concurrent profile creation yields one success and conflicts for the other requests; the migration preserves historical duplicates, deterministically selects the active profile and hides archived rows from lookup.
- Test authentication confined to the test host; production JWT handling ignores the test header.
- Genuine RSA-signed test tokens accepted by the real JWT validator; wrong audience, expired tokens and untrusted signatures rejected.
- Insecure issuer configuration rejected.
- Actual migrations applied repeatedly and checked against EF models.
- Profile/outbox transaction rollback on failed persistence.
- Real Identity password hashing, password checks, minimum length and duplicate-email rejection using the production identity registration.

Test signing keys and static issuer metadata live in the test assembly only. The additional authentication browser scenario exercises the real self-hosted issuer, account forms, login/logout, cookies, CSRF, token renewal and password reset. Email delivery is captured through a test-only adapter.

Packages and the EF tool are pinned; NuGet lockfiles support --locked-mode. Recommended analyzers and warnings-as-errors apply to handwritten code. EF migration files are marked generated in EditorConfig. Use dotnet format OurSpace.slnx before committing C# changes. Keep the installed SDK/runtime security patches current; global.json currently records the available 10.0.204 SDK with compatible .NET 10 feature-band roll-forward; container SDK/runtime images are pinned by digest.

## Remaining work and next decisions

The approved immediate milestone is [account lifecycle and service consistency](ACCOUNT_LIFECYCLE.md), including staged suspension/deletion, service authentication and RabbitMQ. Its delivery status is tracked explicitly. One active profile per account is now enforced by a filtered unique index; pre-existing duplicates are preserved as archived profiles and excluded from profile lookup. Creating another active profile returns HTTP 409.

OpenIddict and a dedicated ASP.NET Core BFF are now approved. The approved account flows are implemented; follow [AUTHENTICATION.md](AUTHENTICATION.md) for their configuration and operational limits. Profile revisions, durable validation jobs, private preview, publication, rollback, API idempotency, resource/rate limits and complete error/audit conventions follow this foundation. Social graph, content, feed and messaging remain separate required services.

References: [ASP.NET Core integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), [JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [EF migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/).
