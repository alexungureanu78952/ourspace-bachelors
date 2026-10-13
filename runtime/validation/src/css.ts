import postcss from 'postcss';
import valueParser from 'postcss-value-parser';
import { fail } from '../../contracts/src/diagnostics.js';
import { CSS_FUNCTIONS, CSS_PROPERTIES, LIMITS } from '../../contracts/src/policy.js';
import { utf8Bytes } from './contracts.js';
export function validateCss(css: string): string {
  if (utf8Bytes(css) > LIMITS.cssBytes) fail('SIZE_LIMIT', 'CSS exceeds byte limit', 'styles.css');
  if (/[<>\\\\]/.test(css))
    fail('CSS_ENCODING', 'HTML delimiters and CSS escapes are unsupported', 'styles.css');
  let root: postcss.Root;
  try {
    root = postcss.parse(css);
  } catch {
    fail('CSS_SYNTAX', 'Malformed CSS', 'styles.css');
  }
  let rules = 0,
    declarations = 0;
  root.walk((node) => {
    if (node.type === 'comment') return;
    if (node.type === 'rule') {
      if (
        node.parent?.type !== 'root' ||
        ++rules > LIMITS.cssRules ||
        node.selector.length > LIMITS.selectorChars ||
        !/^[a-zA-Z0-9.# ,+~-]+$/.test(node.selector)
      )
        fail('CSS_SELECTOR', 'Unsupported selector', 'styles.css');
      return;
    }
    if (
      node.type !== 'decl' ||
      node.parent?.type !== 'rule' ||
      ++declarations > LIMITS.cssDeclarations ||
      !CSS_PROPERTIES.includes(node.prop) ||
      node.important ||
      node.value.length > LIMITS.valueChars
    )
      fail('CSS_POLICY', 'Unsupported CSS construct', 'styles.css');
    valueParser(node.value).walk((value) => {
      if (
        value.type === 'function' &&
        !CSS_FUNCTIONS.some((fn) => fn === value.value.toLowerCase())
      )
        fail('CSS_FUNCTION', 'Unsupported CSS function', 'styles.css');
      if (value.type === 'word' && /url|expression|javascript|https?:|data:/i.test(value.value))
        fail('CSS_VALUE', 'Unsupported CSS value', 'styles.css');
    });
  });
  return root.toString();
}
