import type { z } from 'zod';
import { fail } from '../../contracts/src/diagnostics.js';
import { LIMITS } from '../../contracts/src/policy.js';
import { dataSchema, sourceSchema, treeSchema } from '../../contracts/src/schemas.js';
import type {
  ProfileNode,
  ProfileData,
  ProfileTree,
  SourceBundle,
} from '../../contracts/src/schemas.js';
export function parseContract<T>(schema: z.ZodType<T>, value: unknown): T {
  const result = schema.safeParse(value);
  if (!result.success) {
    const issue = result.error.issues[0];
    fail(
      'CONTRACT_INVALID',
      issue?.message ?? 'Invalid contract',
      '$.' + (issue?.path.map(String).join('.') ?? ''),
    );
  }
  return result.data;
}
export const utf8Bytes = (value: string): number => Buffer.byteLength(value, 'utf8');
export function validateSource(value: unknown): SourceBundle {
  const bundle = parseContract(sourceSchema, value);
  if (
    utf8Bytes(JSON.stringify(bundle)) > LIMITS.bundleBytes ||
    utf8Bytes(bundle.files['Profile.tsx']) > LIMITS.sourceBytes ||
    utf8Bytes(bundle.files['styles.css']) > LIMITS.cssBytes
  )
    fail('SIZE_LIMIT', 'Source bundle exceeds byte limits');
  return bundle;
}
export function validateData(value: unknown): ProfileData {
  const data = parseContract(dataSchema, value);
  const ids = new Set<string>();
  for (const section of data.sections) {
    if (ids.has(section.id)) fail('SECTION_DUPLICATE', 'Section identifiers must be unique');
    ids.add(section.id);
  }
  for (const text of [
    data.displayName,
    data.bio,
    ...data.sections.flatMap((section) => [section.heading, section.body]),
  ]) {
    if (utf8Bytes(text) > LIMITS.textBytes) fail('SIZE_LIMIT', 'Profile text exceeds byte limit');
  }
  return data;
}
export function validateTree(value: unknown): ProfileTree {
  // Bound object depth/count before invoking the recursive schema, including cycles.
  const stack: { value: unknown; depth: number }[] = [{ value, depth: 0 }];
  const seen = new Set<object>();
  let visited = 0;
  while (stack.length) {
    const item = stack.pop();
    if (!item) break;
    if (item.depth > LIMITS.depth * 3 + 6 || ++visited > LIMITS.nodes * 12)
      fail('TREE_COMPLEXITY', 'Input structure exceeds limits');
    if (item.value !== null && typeof item.value === 'object') {
      if (seen.has(item.value))
        fail('TREE_CYCLE', 'Cyclic/shared object references are unsupported');
      seen.add(item.value);
      for (const child of Object.values(item.value))
        stack.push({ value: child, depth: item.depth + 1 });
    }
  }
  const tree = parseContract(treeSchema, value);
  const nodes: { node: ProfileNode; depth: number }[] = [{ node: tree.root, depth: 0 }];
  let count = 0,
    sectionBindings = 0;
  while (nodes.length) {
    const item = nodes.pop();
    if (!item) break;
    if (++count > LIMITS.nodes || item.depth > LIMITS.depth)
      fail('TREE_COMPLEXITY', 'Tree exceeds limits');
    if (item.node.kind === 'sections' && ++sectionBindings > 1)
      fail('SECTIONS_DUPLICATE', 'Only one sections binding is supported');
    if (item.node.kind === 'text' && utf8Bytes(item.node.value) > LIMITS.textBytes)
      fail('SIZE_LIMIT', 'Tree text exceeds byte limit');
    if (item.node.kind === 'element') {
      if (['br', 'hr'].includes(item.node.tag) && item.node.children.length)
        fail('VOID_CHILDREN', 'Void elements cannot contain children');
      for (const child of item.node.children) nodes.push({ node: child, depth: item.depth + 1 });
    }
  }
  if (tree.root.kind !== 'element') fail('TREE_ROOT', 'Root must be an element');
  return tree;
}
