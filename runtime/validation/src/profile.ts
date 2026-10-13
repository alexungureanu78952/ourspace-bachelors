import { parseJsx } from '../../authoring/src/jsx.js';
import { validateCss } from './css.js';
import { validateData, validateSource, validateTree } from './contracts.js';
import type { ProfileData, ProfileTree, SourceBundle } from '../../contracts/src/schemas.js';
export interface ValidatedProfile {
  source: SourceBundle;
  tree: ProfileTree;
  data: ProfileData;
  css: string;
}
export function validateProfile(source: unknown, data: unknown): ValidatedProfile {
  const bundle = validateSource(source);
  return {
    source: bundle,
    tree: validateTree(parseJsx(bundle.files['Profile.tsx'])),
    data: validateData(data),
    css: validateCss(bundle.files['styles.css']),
  };
}
