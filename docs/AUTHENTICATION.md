# Authentication and browser sessions

The approved implementation uses ASP.NET Core Identity for accounts, OpenIddict for OpenID Connect, and a dedicated ASP.NET Core backend for frontend (BFF) for browser sessions. Next.js application pages remain a subsequent task; the current small HTML forms exercise the complete protocol.

## Ownership and testability

Identity owns accounts, password hashes, confirmation/reset tokens and protocol grants in its PostgreSQL database. The BFF owns encrypted session tickets in a separate PostgreSQL database. Profiles validates signed access tokens and enforces ownership through ICurrentUser. Future social services retain their own authorization rules and databases.

The browser receives a Secure, HttpOnly cookie containing a protected session reference. Access and refresh tokens stay in the BFF database, protected with ASP.NET Core Data Protection. Only explicitly implemented profiles routes are forwarded. Generated profiles receive no credentials.

Tests replace email delivery through IAccountEmailSender, and use test-only HTTP transports and signing keys. The browser scenario still runs real account policies, OpenIddict, cookie/CSRF middleware, PostgreSQL and JWT validation. Existing focused tests can inject a current user or test authentication without changing production security.

## Account and session policy

- Public email/password registration; unique email and confirmed email required before sign-in.
- Passwords use Identity hashing and require 12–128 characters, upper/lowercase letters, a digit and a symbol. Five failed sign-ins lock the account for 15 minutes.
- Confirmation and reset links display forms; POST applies the action with antiforgery validation. Registration and recovery return generic eligibility messages.
- Authorization Code flow requires PKCE; the confidential BFF client has exact callback and logout URLs. Established middleware validates state, nonce and protocol tokens.
- Access/ID tokens last five minutes. Identity cookies, BFF sessions and refresh tokens have a 30-minute baseline; sliding session extension is disabled.
- Concurrent refreshes lock the session row in PostgreSQL, so replicas use the latest refresh token. Failed refresh removes that session.
- Password reset changes the Identity security stamp and revokes the account's protocol grants. Existing BFF sessions stop obtaining tokens at renewal. Already issued access tokens can remain valid for five minutes plus the API's 30-second clock skew; reset is not immediate global session termination.
- BFF logout removes the local server session, then asks for confirmation on Identity. Confirmation signs out the identity cookie and revokes the account's protocol grants. Other browser sessions lose renewal access; their existing access tokens retain the same bounded validity.
- Identity and BFF forms require antiforgery tokens; BFF JSON mutations use X-CSRF-TOKEN obtained from /bff/csrf. The protocol token endpoint uses client/protocol validation rather than browser antiforgery.

## Local configuration

Use three HTTPS origins, for example Identity https://localhost:7101, BFF https://localhost:7102, Profiles https://localhost:7103. Trust the local ASP.NET development HTTPS certificate using dotnet dev-certs https --trust. Set ASPNETCORE_ENVIRONMENT=Development in each development terminal. Development signing/encryption certificates must never be production credentials.

Apply each service's migrations before startup, with its ConnectionStrings__Identity, ConnectionStrings__Bff or ConnectionStrings__Profiles variable set:

```powershell
dotnet tool restore
dotnet ef database update --project services/identity/src/OurSpace.Identity.Infrastructure
dotnet ef database update --project services/bff/src/OurSpace.Bff.Infrastructure
dotnet ef database update --project services/profiles/src/OurSpace.Profiles.Infrastructure
```

Identity configuration:

- ConnectionStrings__Identity
- Identity__Issuer=https://localhost:7101
- Identity__BffOrigin=https://localhost:7102
- Identity__ClientSecret: a generated secret of at least 32 characters, also supplied to the BFF
- Email__Host, Email__Port, Email__From
- Email__UseTls=true by default: SMTP STARTTLS is required. Optional Email__Username and Email__Password configure SMTP authentication.
- For a local email catcher only: Email__Host=localhost, Email__Port set to its SMTP port, Email__UseTls=false. Plain SMTP is restricted to Development. Confirmation remains mandatory.

BFF configuration:

- ConnectionStrings__Bff
- Bff__Authority=https://localhost:7101
- Bff__ClientSecret: the same confidential client secret
- Bff__ProfilesOrigin=https://localhost:7103

Profiles configuration:

- ConnectionStrings__Profiles
- Authentication__Authority=https://localhost:7101
- Authentication__Audience=ourspace.profiles

Start each service in its configured terminal using dotnet run --project services/identity/src/OurSpace.Identity.Api --urls https://localhost:7101, and the corresponding BFF/profiles projects and ports. Open Identity /account/register, confirm through the local mailbox, then open BFF /bff/login. The BFF root page includes sign-in/sign-out actions; JSON profile routes are /bff/profiles and /bff/profiles/{id}.

The complete local Docker deployment now includes the HTTPS gateway, real SMTP capture, separate databases, generated credentials/certificates, encrypted persistent key rings, explicit migrations, runtime database roles, cleanup and backup/restore tooling. Follow [LOCAL_DEPLOYMENT.md](LOCAL_DEPLOYMENT.md).

Outside Development/Testing, encrypted persistent Data Protection configuration is required at startup. For host deployment configure DataProtection__KeyPath, DataProtection__Certificate and its CertificatePassword along with the protocol certificates. Local Docker supplies these via mounted configuration secrets. Public hosting, real SMTP delivery, distributed abuse controls and capacity/recovery exercises remain separate operational work.

## Validation evidence

Locally verified on 2026-10-20 with PostgreSQL 18 and headless Edge over HTTPS:

- The backend gate passed 7 unit tests and 16 integration tests, locked dependency restore/auditing, a zero-warning analyzer build and formatting verification.
- The browser scenario covers registration, rejection before email confirmation, confirmation and reset-link replay rejection, real login, CSRF rejection, unsafe login-return rejection, profile creation/owner matching through the real JWT validator, concurrent renewal, password reset, failed renewal after reset, old-password rejection, new-password login, logout and cookie replay rejection. An invalid callback state is rejected. This does not independently test every nonce, PKCE, redirect or lockout attack variant.
- The server decrypts the captured browser cookie and verifies that neither access nor refresh tokens are serialized inside it. A separate PostgreSQL test checks encrypted session payloads, expiration and refusal to recreate a removed session through renewal.
- The runtime quality gate passed its 77 tests. Final authentication-script changes also passed ESLint.

The focused backend browser test uses a captured sender. The separate Docker deployment scenario uses the actual SMTP adapter and Mailpit; see the local deployment guide for its evidence and internet-deployment limits.
