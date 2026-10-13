# Profile runtime experiment

This is a runnable experiment, not the platform application or a production sandbox.

It proves a limited TSX/CSS profile can become a bounded JSON tree and render through one trusted React runtime. User TSX is parsed as data and never imported, evaluated or executed. The renderer supports a small element/attribute allowlist and only data.displayName / data.bio expressions. Arbitrary JavaScript, hooks, loops, imports and event handlers are unsupported.

## Run

Requires Node 24 and npm. Browser checks default to installed Microsoft Edge; set PROFILE_BROWSER=chromium to use a Playwright-managed browser after running npx playwright install chromium.

From this directory:

~~~powershell
npm.cmd ci --ignore-scripts
npm.cmd test
npm.cmd run build
npm.cmd run benchmark
npm.cmd run test:browser
npm.cmd run preview
~~~

The preview command prints a local URL and remains running until Ctrl+C. Build outputs, screenshot and JSON measurements are written under artifacts/ (ignored by Git). The HTML artifact requires the HTTP headers in headers.json; opening it as a file does not reproduce the security policy.

## Candidate contract: spike-v0

- Exactly one manifest and exactly Profile.tsx / styles.css; no paths or dependency configuration.
- Exactly one default Profile function with a { data } parameter and a single JSX return.
- Elements: main, header, footer, section, article, div, span, p, h1-h4, ul, ol, li, strong, em, small, br, hr.
- Attributes: className, id, title, role, aria-label; string literals only.
- Profile data: displayName and bio, text only.
- JSX whitespace is collapsed and trimmed; entity syntax is not decoded. This is a layout notation, not full React/JSX semantic compatibility.
- CSS: basic selectors and explicitly allowed layout/type/color declarations; no at-rules, URLs, escapes, custom properties, HTML delimiters or important declarations.
- Limits: 64 KiB serialized bundle, 32 KiB TSX, 16 KiB CSS, 500 tree nodes, depth 24, 100 CSS rules, 500 declarations, 16 KiB per profile data field.

Inspect policy.mjs for the actual enforced allowlists. This experimental contract does not claim compatibility with the planned runtime-v1.

## What the checks demonstrate

Policy tests cover the valid fixture, 28 invalid fixtures, profile-data shape and the closed arbitrary-code execution gate. Browser tests render real React/CSS, display injection-shaped text literally, and test parent DOM access, cookies, storage, network requests, image requests and injected scripts under CSP plus an opaque-origin iframe.

The trusted test probe deliberately attempts prohibited browser operations. It is not user code accepted by the validator.

## Boundaries and limitations

The browser experiment uses two localhost ports: separate origins, but not separate sites. Production must use a separate content site without platform cookies; its framing allowlist must replace frame-ancestors *. Preview authorization is not implemented.

The parser runs locally in this experiment. Byte/tree limits reduce work but do not prove parser CPU/memory safety. A future ingestion worker needs process-level budgets and rate limits. There is no verified server execution sandbox; sandbox.mjs refuses arbitrary-code execution unconditionally.

The shared renderer is built once per experiment run. Test HTML embeds its bytes to simplify CSP; production delivery should use an immutable shared runtime asset with appropriate integrity/CSP/CORS and caching. Current artifacts do not implement that network saving.

The benchmark compares validation, artifact assembly and repeated trusted-renderer bundling. It uses one tiny layout and excludes AI calls, semantic scoring, full program typechecking, worker isolation startup and production load. See ../../docs/RUNTIME_SPIKE_REPORT.md.

