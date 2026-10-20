# Authentication implementation plan

Status: stack approved by the user, 2026-10-20. Public registration, mandatory email confirmation, password reset, lockout and a replaceable email sender are also approved.

## Approved architecture

- ASP.NET Core Identity owns accounts, passwords and account-management rules.
- Self-hosted OpenIddict adds the OAuth 2.0/OpenID Connect server in the identity service.
- A dedicated ASP.NET Core BFF owns the browser session and server-side access tokens.
- Next.js provides the application interface. Plan a same-origin application entry point routing browser backend endpoints to the BFF; resolve authenticated server-rendering needs before building Next.js pages.
- Services validate access tokens and independently authorize resources.
- Current-user abstractions and test-only authentication remain supported alongside real end-to-end authentication tests.

A BFF is not a general forwarding endpoint: backend destinations and routes must be explicitly allowed. Generated profiles remain isolated and never receive authentication credentials.

## First end-to-end milestone

A user signs in through the identity service, returns to the BFF, creates/reads their own profile through the profiles API, and signs out.

Use Authorization Code flow with PKCE, state and nonce through established middleware. The browser follows redirects; token exchange occurs server-side. Store access/refresh tokens in a server-side session record rather than putting them in a browser-readable token store or serializing them into the browser session cookie.

The browser receives an opaque, Secure, HttpOnly session cookie with an appropriate SameSite policy. Protect state-changing browser requests against CSRF and use an authenticated, CSRF-protected logout action. Restrict redirect URIs and return paths. Define logout behavior across the BFF and identity session explicitly.

## Approved account-flow decisions

Approved product behavior: public email/password registration, email confirmation before normal sign-in, password reset and lockout.

Recommended delivery adaptation for development: a local email catcher or test email sink behind an email-sending interface. Confirmation remains required; local development does not disable it. A production mail adapter and its provider remain a separate operational decision.

## Implementation order after flow agreement

1. Define account-flow requirements and acceptance criteria, identity/BFF configuration, service audience/scopes and signing/encryption/key-storage boundaries. Verify compatible OpenIddict packages before pinning them.
2. Add OpenIddict persistence and migrations to the identity-owned database; register the trusted BFF client with exact redirect/logout URIs.
3. Implement identity account pages/actions and email-delivery adapter with real account policies.
4. Implement the dedicated BFF's OIDC client, server-side session/token storage, CSRF protection and explicit profiles API adapter.
5. Add a minimal trusted browser entry point for the complete sign-in/profile/sign-out scenario; integrate the full Next.js interface afterward.
6. Exercise real redirects, cookies and service requests in browser tests alongside unit and API tests.

Use development-only certificates/keys for local testing and separate configured production key material. Keep secrets out of source control and test identity handlers out of deployed projects. Decide session lifetime, token lifetime, refresh/revocation, persisted data-protection keys and deployment routing explicitly before claiming production readiness.

## Required acceptance evidence

- Successful sign-in and authorized profile operations through the BFF.
- Invalid credentials and the agreed account-verification rules enforced.
- Access tokens absent from browser-visible application responses and session-cookie payloads.
- Missing/invalid CSRF tokens rejected for state-changing browser actions.
- Invalid callback state/nonce, mismatched redirects and untrusted return paths rejected.
- Anonymous, expired and logged-out sessions cannot perform protected operations.
- Token renewal and failure behavior follow a documented session policy.
- Logout terminates the intended server-side session; cross-session identity logout behavior is tested explicitly.
- The production JWT validator continues to reject wrong audience, issuer, signature and expired tokens.
- Existing ownership, PostgreSQL migration, transactional outbox and runtime regression tests continue passing.

The account and BFF implementations are described in [AUTHENTICATION.md](AUTHENTICATION.md). Production key management, deployment routing, email operations and broader adversarial/capacity testing remain deployment requirements. Acceptance statements above are requirements; consult the concrete tests for evidence of each case.
