# Profile runtime-v1 contract

Status: first implemented baseline, 2026-10-13. This freezes the identifiers and documented behavior of v1, not the product's future capabilities. Changes follow ADR-007 and the roadmap's capability-change process.

## Modules and ownership

| Module             | Responsibility                                                   | Allowed local dependencies |
| ------------------ | ---------------------------------------------------------------- | -------------------------- |
| runtime/contracts  | Types, schemas, versions, capability policy and diagnostic shape | No other runtime modules   |
| runtime/authoring  | Constrained JSX to profile tree                                  | Contracts                  |
| runtime/validation | Source/tree/data/CSS checks and validation orchestration         | Contracts, authoring       |
| runtime/renderer   | Trusted tree/data to React elements                              | Contracts                  |
| runtime/artifacts  | Canonical hashing, HTML/headers and artifact metadata            | Contracts, validation      |
| runtime/profile-v1 | Trusted runtime build, fixture composition, preview and tests    | The modules above          |

ESLint enforces the core dependency direction. Renderer code cannot import Node built-ins. JSX parsing does not invoke the renderer or run user code. No production module imports the historical spike.

## Version identifiers

| Contract                 | Identifier                   |
| ------------------------ | ---------------------------- |
| Source schema            | profile-source-v1            |
| Tree schema              | profile-tree-v1              |
| Data schema              | profile-data-v1              |
| Capability policy schema | profile-capability-policy-v1 |
| Capability policy        | profile-policy-v1            |
| Trusted runtime          | profile-runtime-v1           |
| Artifact format          | profile-artifact-v1          |

Artifact metadata records source/tree/data schema, runtime, policy and artifact-format versions. The capability-policy document records its own schema version. Unknown versions fail validation; there is no automatic latest-version fallback.

Generated JSON schemas live in runtime/contracts/schemas. They are exported from Zod definitions. Byte budgets, unique section IDs, tree complexity, supported CSS and other semantic rules additionally require the validation modules; JSON Schema alone is not the full policy.

## Source and authoring

The source bundle has exactly manifest and files. Manifest includes schemaVersion, runtimeVersion, policyVersion and entryPoint=Profile.tsx. Files contains exactly Profile.tsx and styles.css. No dependency or build configuration is accepted.

Supported module grammar:

```jsx
export default function Profile({ data }) {
  return (
    <main className="profile">
      <h1>{data.displayName}</h1>
      <p>{data.bio}</p>
      {data.sections}
    </main>
  );
}
```

Only this function shape and a single JSX return are supported, with optional surrounding parentheses. Imports, calls, general expressions, state/hooks, loops, event handlers, spreads, raw HTML and custom components are rejected. Comments outside the returned tree are insignificant; JSX expression comments are unsupported.

Supported tags, attributes, fields, CSS rules and limits are exported in capabilities.json and policy.ts. Attribute values are string literals. JSX text whitespace collapses to a single space, preserving leading/trailing spaces around nonempty inline text; wholly whitespace nodes are dropped. Strict HTML entities are decoded once and subsequently escaped by React. This is explicit layout-notation behavior, not full JSX compatibility.

## Tree and data

A tree envelope contains schemaVersion and an element root. Nodes are element (tag, props, children), text (value), data (displayName or bio field), or sections (no additional fields).

Profile data contains schemaVersion, nonempty displayName, bio and sections. Each section contains a unique id, nonempty heading and plain-text body. Identifiers are bounded ASCII letters/digits/hyphens. The sections array is required, including when empty.

The controlled sections binding is a capability-extension demonstration. The trusted renderer creates a section, h2 and p for each item; user content remains text. Only one sections binding is allowed in a tree. The normal artifact assembly and metadata interface are unchanged whether the source includes sections or not. Publication business rules have not yet been implemented; the test demonstrates the runtime/artifact boundary only.

The renderer accepts contracts validated by the trusted artifact producer. Its browser payload does not authorize publication or service access.

## Limits and CSS

Defaults: 64 KiB serialized source bundle, 32 KiB TSX, 16 KiB CSS, 500 tree nodes, depth 24, 100 CSS rules, 500 declarations, 20 sections and 16 KiB per text field. Display names and section headings have additional character limits. Authoring also bounds visited syntax nodes. Section expansion is separately bounded by section count.

CSS supports the explicit property and function allowlists, basic selectors and top-level rules. It rejects URLs, imports/other at-rules, escapes, HTML delimiters, custom properties and important declarations. This is a conservative capability policy; it is not a comprehensive CSS conformance or accessibility validator.

HTTP/body-size limits and worker CPU/memory/time budgets remain service responsibilities. The local parser limits and exception diagnostics do not constitute an adversarial execution sandbox.

## Diagnostics

Rejected input throws ContractError with diagnostics containing code, severity=error, message, path and recommendation. Authoring errors include source line/column in the path; schema errors identify the contract path; CSS errors currently identify styles.css without line/column. Validators report the first failure.

The service layer should map this structure into its documented API errors, without exposing internal exceptions or host paths.

## Artifacts and integrity

The trusted build compiles only our renderer and pinned dependencies. Artifact assembly revalidates source/data, generates the tree and validates CSS. Runtime descriptors are trusted server configuration from buildRuntime or a future approved registry, never request parameters. A self-consistent runtime hash is an integrity check, not authorization to run an arbitrary submitted script.

Metadata binds to canonical source, exact runtime bytes, exact HTML bytes and response headers via SHA-256. Canonical JSON sorts object keys lexicographically, preserves array order and encodes UTF-8. Source file text is preserved: CRLF and LF source revisions have distinct digests. Inputs are validated JSON-compatible records; the canonicalizer is not a general-purpose serializer.

Data is escaped for an inline JSON script; React escapes text. CSS rejects delimiters that could close its style block. The runtime uses exact script/style CSP hashes, blocks network capabilities and uses sandbox allow-scripts without same-origin permission. The framing origin must be supplied explicitly as an exact HTTP(S) origin. Production deployment must require HTTPS and separate authenticated/content sites.

Artifact identity must include response policy as well as content: htmlDigest alone is insufficient. Hashes are not digital signatures or publication acceptance reports. A future publication service must also record revision/build identity, validation provenance, revocation and currently accepted policy.

## Compatibility and remaining implementation

spike-v0 remains an experiment with its own implementation and evidence. v1 rejects a v0 manifest; adapting old source is explicit and revalidates it. No existing production artifacts require migration.

Future breaking schema, authoring-semantic, policy or renderer changes require appropriate new identifiers and compatibility fixtures. Preserve approved old runtime artifacts while published content references them; new validation never silently rewrites history.

Unit tests cover supported trees, an extension via sections, hostile syntax/CSS, unknown versions, byte/complexity limits, diagnostics, determinism and injection-shaped data. The real-browser test covers rendering/CSS, parent/cookie/storage/network restrictions and injected scripts. The accessibility scan covers one representative static fixture, not arbitrary user layouts. Local browser checks use two same-site origins and do not prove production site/cookie separation.

Still required: bounded ingestion workers, external runtime asset delivery/cache, production isolation/authorization, persistent revision and publication lifecycle, and the social services. Arbitrary user JavaScript remains unsupported.
