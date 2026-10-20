# Local Docker deployment

This deploys the current identity, BFF and profiles services on your machine. It does not publish the platform online or supply the future Next.js interface.

## Start and use

Requirements: Docker Engine using Linux containers, Docker Compose, .NET 10 SDK (10.0.204 or a compatible newer feature band), Node 24 and npm dependencies installed.

```powershell
npm ci --ignore-scripts
npm run local:up
npm run local:status
```

Startup generates random passwords, client credentials, a local CA and separate protocol/Data Protection certificates under ignored .local/deployment/. Initialization is idempotent; existing credentials are retained. The app images are built from source. A one-shot migration container applies each service's migrations before application startup and grants limited runtime roles.

- Application/BFF: https://app.localhost:7443
- Accounts/registration: https://identity.localhost:7443/account/register
- Local mailbox: http://localhost:8025
- Profiles readiness: https://profiles.localhost:7443/health/ready

The .localhost names resolve to loopback in the browser; Docker aliases resolve those same names to the HTTPS gateway inside the deployment. No hosts-file edit is required. Gateway and mailbox ports bind only to loopback. Database ports 55432/55433/55434 also bind only loopback for developer tooling. The fixed private Docker subnet is 172.29.249.0/24; change both Compose and the generated trusted-proxy configuration if that subnet conflicts with another local network.

Caddy terminates HTTPS and forwards to unexposed HTTP services on the private Docker network. Only its explicit IP is trusted for forwarded scheme/client-IP headers. Access logging is disabled to keep authorization codes, state and email tokens out of request logs.

Mailpit captures actual SMTP messages and does not forward them externally. The identity adapter requires STARTTLS, validates the local certificate and retains mandatory confirmation. The generated private CA has no revocation-list service, so Email:CheckCertificateRevocation=false is approved only in the LocalDocker environment. Production startup rejects that setting; SMTP hostname and CA-chain validation stay enabled. Read the confirmation/reset links in the mailbox.

## Live demo

1. Register at the accounts URL and open the confirmation email in Mailpit. Follow the link and press Continue.
2. Open the application and sign in. Enter a display name under Create a private profile and press Create profile.
3. The page displays the saved name, profile ID, owner account and creation time. Copy the profile ID for later use.
4. Press Load profile or refresh the result page to retrieve the same stored profile. To demonstrate denial, try a nonexistent ID or another account's profile ID; both are unavailable.
5. Sign out and sign back in, then paste the saved ID to load the profile again. Password recovery is available from the signed-out page, with its email captured in Mailpit.

These are ordinary HTML forms using the existing server-side authentication and ownership rules. Profile creation requires a CSRF token. The current profile data is a display name and ownership metadata; the customizable profile editor and Next.js interface remain later milestones.

## Browser certificate trust

The local CA is intentionally not installed in your operating system automatically. To trust it for your own Windows account, review .local/deployment/ca.crt and optionally run:

```powershell
certutil -user -addstore Root .local/deployment/ca.crt
```

This grants browser trust to certificates issued by that local CA. Keep its private key private. Without importing it, your browser displays a local-certificate warning. The automated browser test accepts only its local test environment's untrusted HTTPS chain; the .NET services and SMTP adapter validate certificates normally using the mounted CA. No certificate-validation bypass exists in application code.

## Persistence and security configuration

Each database has its own volume and owner/migration credentials. Running apps use separate non-superuser roles with data privileges, no schema creation and no migration-history writes. Owner credentials are mounted only into the migration job and corresponding database. Secrets are mounted as per-service files rather than baked into images.

Identity and BFF have separate persistent key-ring volumes, application names and encryption certificates. Key XML is encrypted at rest. Container recreation retains cookies, confirmation/reset tokens and encrypted session tickets. Losing either the key ring or its certificate can invalidate sessions and outstanding account links. Back them up together.

Application containers run as the non-root app user with read-only root filesystems, dropped capabilities and temporary /tmp filesystems. Images are pinned by digest. The Docker SDK is 10.0.402; the host baseline allows compatible .NET 10 feature-band roll-forward.

The BFF deletes expired session rows every five minutes. Identity uses OpenIddict's pruning APIs hourly for eligible records older than one day. Cleanup failures log a generic warning and retry at the next interval; expired credentials remain rejected even before physical deletion.

Health checks run against service-owned database readiness; Compose waits for dependencies and successful migration completion. These checks are availability probes, not a complete security or migration audit. Application logs and gateway logs are bounded; inspect failures with docker compose -f infra/compose.yml logs --tail 100 SERVICE. Avoid enabling verbose protocol, SQL-parameter or request-body logging.

## Verify the deployment

```powershell
npm run test:deployment
```

Install a Playwright browser first if necessary: Windows uses Edge; Linux uses npx playwright install --with-deps chromium. The scenario creates uniquely named local test accounts and messages. It verifies real SMTP confirmation/reset, HTTPS redirects, CSRF, JWT-backed profile operations, logout, and session/confirmation survival across forced container recreation. It also inspects persisted key encryption and runtime-role privilege flags. No test-only endpoint or fake login is deployed.

The separate npm run check and backend gate remain required; the Docker smoke test complements them. CI runs the Docker deployment and restore verification in a dedicated job.

## Backup, restore verification and stopping

```powershell
npm run local:backup
npm run local:verify-backup -- .local/backups/BACKUP_DIRECTORY
npm run local:stop
```

Backup briefly stops the three application services, dumps their databases, archives both key-ring volumes, and copies the required ignored secrets/certificates into a timestamped directory. It restarts applications in a finally block. These are sensitive local development backups, not encrypted off-machine archives; protect the directory and do not commit it. Mailpit messages are disposable and excluded.

Restore verification creates randomly named scratch databases, restores each dump, queries the main service table, and drops only those scratch databases. Existing service databases are untouched. This proves the database dumps restore; a complete lost-host recovery also requires restoring the matching key archives and secrets.

For actual recovery, stop applications, restore secrets from the chosen backup, restore each dump to its matching empty service database using pg_restore, and restore the corresponding key tar archive to its named key volume before starting applications. Never mix snapshots from different backup directories. Review commands against the intended local project before replacing any existing volume or database.

Stopping retains volumes. Do not use down -v as a routine shutdown: it deletes local data and keys. Certificate lifetime is one year (local CA two years); preserve old decryption keys during rotation. Data Protection supports PreviousCertificates for decryption overlap using the configured certificate password. Protocol signing/encryption rotation and internet deployment require their own reviewed key-overlap procedure.

## Scope before internet deployment

This is a single-machine local deployment. Public hosting still needs real DNS/TLS, an SMTP delivery provider, external secret/backup storage and recovery drills, alerting, capacity tests, and distributed abuse controls if replicated. Current fixed-window limits are per identity process. Email sends are synchronous: failures surface as failed requests and users can retry registration/recovery; durable email delivery is separate work. No production hosting was configured or claimed.

Implementation references: [ASP.NET Core Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0), [trusted forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0), [Mailpit SMTP configuration](https://mailpit.axllent.org/docs/configuration/smtp/).

## Verification record

Verified locally on 2026-10-20: all services healthy, migration job exited successfully, full Docker browser scenario passed with actual STARTTLS email, confirmation links and BFF sessions survived forced container recreation, persisted key XML was encrypted, and runtime roles had no superuser/database/role creation flags. The backend gate passed 23 tests with zero build warnings; the runtime gate passed 77 tests. Database backups restored successfully into isolated scratch databases. Public SMTP delivery, internet hosting, key rotation and lost-host recovery have not been exercised.
