# OurSpace

OurSpace is a social platform with customizable AI-assisted profiles, friendships, posts/feed and private messaging. Development starts with the profile runtime; the first identity/profile backend foundation is now implemented, while the complete application and social services remain in development.

## Current implementation

The constrained runtime-v1 accepts declarative JSX and limited CSS, validates them into a versioned profile tree and renders through trusted React code. It supports display name, bio and controlled profile sections. Supplied JavaScript is never executed.

See the [runtime contract](docs/RUNTIME_V1_CONTRACT.md), [implementation roadmap](docs/IMPLEMENTATION_ROADMAP.md) and [social service architecture](docs/SOCIAL_PLATFORM_ARCHITECTURE.md).

## Requirements and commands

Use Node 24 (the local validation environment uses 24.11.1) and npm. From the repository root:

```powershell
npm.cmd ci --ignore-scripts
npm.cmd run check
npm.cmd run build
npm.cmd run preview
```

On Linux/macOS use npm instead of npm.cmd. The browser test uses installed Microsoft Edge on Windows and Playwright Chromium elsewhere. To use managed Chromium on Windows:

```powershell
npx.cmd playwright install chromium
$env:PROFILE_BROWSER = 'chromium'
npm.cmd run check
```

On Linux CI installs Chromium with npx playwright install --with-deps chromium. The preview command prints an ephemeral local URL; Ctrl+C stops it. It serves a fixture, not an authenticated application.

## Quality standards

- Strict TypeScript, unchecked-index protection, exact optional properties and unused-code checks.
- ESLint with type-aware rules, zero warnings and enforced module import boundaries.
- Prettier, EditorConfig and LF normalization.
- Generated JSON schemas/capability snapshot checked for drift.
- Unit/contract/security fixtures, spike regression and real-browser checks.
- axe-core accessibility checks on the equivalent rendered fixture outside the opaque sandbox.
- Pinned dependencies and a lockfile; CI runs npm audit --audit-level=high.

Run npm run format for intentional formatting, npm run lint for lint only, and npm run schemas:generate after an intentional contract/policy change. Review generated changes and version implications before committing. npm run check validates without updating schemas. Build/test commands write ignored .build/ and artifact outputs.

The historical spike, its lockfile and recorded evidence are preserved: runtime/profile-spike and docs/evidence are excluded from automatic reformatting. Spike source is excluded from the new lint/typecheck scope; its policy tests are included in the root quality gate. New production modules do not import the spike.

The [CI workflow](.github/workflows/quality.yml) runs on pushes and pull requests. Branch-protection enforcement must be configured in repository settings; adding a workflow alone does not require passing checks before merge.

## Boundaries

Modules under runtime/contracts, authoring, validation, renderer and artifacts are intentionally separate. runtime/profile-v1 contains composition, fixtures, build/preview tools and tests. These are code boundaries, not five deployable services.

The current runtime embeds its trusted JavaScript in artifacts. External shared-asset caching, bounded ingestion workers, private preview authorization, persistence and publication services remain future implementation. No arbitrary-code execution sandbox is enabled.

Tool references: [TypeScript compiler API](https://github.com/microsoft/TypeScript/wiki/Using-the-Compiler-API), [typed ESLint](https://typescript-eslint.io/getting-started/typed-linting/), [Prettier CLI](https://prettier.io/docs/cli), [Playwright browsers](https://playwright.dev/docs/browsers), [Zod schemas](https://zod.dev/api).

## Backend foundation

.NET 10 identity/profile services, PostgreSQL migrations, ownership checks and test-only authentication are described in [BACKEND_FOUNDATION.md](docs/BACKEND_FOUNDATION.md). The approved account flows and BFF browser sessions are implemented; see [AUTHENTICATION.md](docs/AUTHENTICATION.md) for setup and remaining deployment work. Run npm run check:backend with a dedicated test database, or use the disposable Windows PostgreSQL runner documented there. Backend CI is separate from the runtime quality job.

## Local Docker deployment

Run npm run local:up to generate ignored local credentials and start PostgreSQL, identity, profiles, the BFF, Caddy HTTPS and Mailpit. See [LOCAL_DEPLOYMENT.md](docs/LOCAL_DEPLOYMENT.md) for URLs, certificate trust, deployment checks and backup/restore commands. npm run local:stop retains data and keys.
