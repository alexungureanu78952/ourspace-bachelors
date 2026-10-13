import { z } from 'zod';
import { ATTRIBUTES, CAPABILITIES, DATA_FIELDS, LIMITS, TAGS, VERSIONS } from './policy.js';
export type ProfileNode =
  | { kind: 'text'; value: string }
  | { kind: 'data'; field: (typeof DATA_FIELDS)[number] }
  | { kind: 'sections' }
  | {
      kind: 'element';
      tag: (typeof TAGS)[number];
      props: z.infer<typeof propsSchema>;
      children: ProfileNode[];
    };
export const propsSchema = z.strictObject({
  className: z.string().max(256).optional(),
  id: z.string().max(128).optional(),
  title: z.string().max(1024).optional(),
  role: z.enum(['main', 'region', 'article', 'list', 'listitem', 'presentation']).optional(),
  'aria-label': z.string().max(1024).optional(),
});
export const nodeSchema: z.ZodType<ProfileNode> = z.lazy(() =>
  z.discriminatedUnion('kind', [
    z.strictObject({ kind: z.literal('text'), value: z.string().max(LIMITS.textBytes) }),
    z.strictObject({ kind: z.literal('data'), field: z.enum(DATA_FIELDS) }),
    z.strictObject({ kind: z.literal('sections') }),
    elementSchema,
  ]),
);
export const elementSchema = z.strictObject({
  kind: z.literal('element'),
  tag: z.enum(TAGS),
  props: propsSchema,
  children: z.array(nodeSchema).max(LIMITS.nodes),
});
export const treeSchema = z.strictObject({
  schemaVersion: z.literal(VERSIONS.tree),
  root: elementSchema,
});
export const dataSchema = z.strictObject({
  schemaVersion: z.literal(VERSIONS.data),
  displayName: z.string().min(1).max(200),
  bio: z.string().max(LIMITS.textBytes),
  sections: z
    .array(
      z.strictObject({
        id: z.string().regex(/^[a-zA-Z0-9-]{1,64}$/),
        heading: z.string().min(1).max(200),
        body: z.string().max(LIMITS.textBytes),
      }),
    )
    .max(LIMITS.sections),
});
export const sourceSchema = z.strictObject({
  manifest: z.strictObject({
    schemaVersion: z.literal(VERSIONS.source),
    runtimeVersion: z.literal(VERSIONS.runtime),
    policyVersion: z.literal(VERSIONS.policy),
    entryPoint: z.literal('Profile.tsx'),
  }),
  files: z.strictObject({
    'Profile.tsx': z.string().max(LIMITS.sourceBytes),
    'styles.css': z.string().max(LIMITS.cssBytes),
  }),
});
export const runtimeDescriptorSchema = z.strictObject({
  runtimeVersion: z.literal(VERSIONS.runtime),
  policyVersion: z.literal(VERSIONS.policy),
  treeSchemaVersion: z.literal(VERSIONS.tree),
  code: z.string().max(1000000),
  sha256: z.string().regex(/^[a-f0-9]{64}$/),
});
export const artifactMetadataSchema = z.strictObject({
  artifactFormatVersion: z.literal(VERSIONS.artifact),
  sourceSchemaVersion: z.literal(VERSIONS.source),
  treeSchemaVersion: z.literal(VERSIONS.tree),
  dataSchemaVersion: z.literal(VERSIONS.data),
  runtimeVersion: z.literal(VERSIONS.runtime),
  policyVersion: z.literal(VERSIONS.policy),
  sourceDigest: z.string().regex(/^[a-f0-9]{64}$/),
  runtimeDigest: z.string().regex(/^[a-f0-9]{64}$/),
  htmlDigest: z.string().regex(/^[a-f0-9]{64}$/),
  headersDigest: z.string().regex(/^[a-f0-9]{64}$/),
});
export type SourceBundle = z.infer<typeof sourceSchema>;
export type ProfileTree = z.infer<typeof treeSchema>;
export type ProfileData = z.infer<typeof dataSchema>;
export type RuntimeDescriptor = z.infer<typeof runtimeDescriptorSchema>;
export type ArtifactMetadata = z.infer<typeof artifactMetadataSchema>;

export const capabilityPolicySchema = z.strictObject({
  schemaVersion: z.literal('profile-capability-policy-v1'),
  policyVersion: z.literal(VERSIONS.policy),
  tags: z.array(z.enum(TAGS)).min(1),
  attributes: z.array(z.enum(ATTRIBUTES)),
  dataFields: z.array(z.enum(DATA_FIELDS)),
  sectionsBinding: z.literal(true),
  cssProperties: z.array(z.string().min(1)),
  cssFunctions: z.array(z.string().min(1)),
  limits: z.strictObject({
    bundleBytes: z.number().int().positive(),
    sourceBytes: z.number().int().positive(),
    cssBytes: z.number().int().positive(),
    nodes: z.number().int().positive(),
    depth: z.number().int().positive(),
    cssRules: z.number().int().positive(),
    cssDeclarations: z.number().int().positive(),
    textBytes: z.number().int().positive(),
    sections: z.number().int().positive(),
    selectorChars: z.number().int().positive(),
    valueChars: z.number().int().positive(),
  }),
});
export type CapabilityPolicy = z.infer<typeof capabilityPolicySchema>;
export const capabilityPolicy: CapabilityPolicy = capabilityPolicySchema.parse({
  schemaVersion: 'profile-capability-policy-v1',
  policyVersion: VERSIONS.policy,
  ...CAPABILITIES,
});

export const SCHEMAS = {
  policy: capabilityPolicySchema,
  source: sourceSchema,
  tree: treeSchema,
  data: dataSchema,
  runtime: runtimeDescriptorSchema,
  artifact: artifactMetadataSchema,
};
