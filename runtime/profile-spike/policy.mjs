import ts from 'typescript';
import postcss from 'postcss';
import valueParser from 'postcss-value-parser';

export const POLICY = 'spike-v0';
export const LIMITS = Object.freeze({ bundleBytes: 65536, sourceBytes: 32768, cssBytes: 16384, nodes: 500, depth: 24, cssRules: 100, textBytes: 16384 });
const tags = new Set('main header footer section article div span p h1 h2 h3 h4 ul ol li strong em small br hr'.split(' '));
const attributes = new Set(['className', 'id', 'title', 'role', 'aria-label']);
const properties = new Set(('color background background-color padding padding-top padding-bottom padding-left padding-right margin margin-top margin-bottom margin-left margin-right border border-color border-width border-style border-radius display gap row-gap column-gap grid-template-columns grid-template-rows flex-direction flex-wrap align-items justify-content width max-width min-width height min-height max-height font-family font-size font-weight line-height letter-spacing text-align text-decoration box-shadow opacity').split(' '));
const functions = new Set(['rgb', 'rgba', 'hsl', 'hsla', 'min', 'max', 'clamp', 'repeat']);
export class PolicyError extends Error { constructor(code, message) { super(message); this.code = code; } }
const fail = (code, message) => { throw new PolicyError(code, message); };
const bytes = value => Buffer.byteLength(value, 'utf8');
const plain = value => value !== null && typeof value === 'object' && !Array.isArray(value) && Object.getPrototypeOf(value) === Object.prototype;
function exactKeys(value, expected, label) {
  if (!plain(value) || Object.keys(value).sort().join('|') !== [...expected].sort().join('|')) fail('SHAPE', label + ' has unsupported fields');
}
export function validateBundle(bundle) {
  exactKeys(bundle, ['manifest', 'files'], 'Bundle');
  exactKeys(bundle.manifest, ['schemaVersion', 'runtimeVersion', 'entryPoint'], 'Manifest');
  if (bundle.manifest.schemaVersion !== POLICY || bundle.manifest.runtimeVersion !== POLICY || bundle.manifest.entryPoint !== 'Profile.tsx') fail('VERSION', 'Unsupported runtime');
  exactKeys(bundle.files, ['Profile.tsx', 'styles.css'], 'Files');
  const source = bundle.files['Profile.tsx'], css = bundle.files['styles.css'];
  if (typeof source !== 'string' || typeof css !== 'string') fail('SHAPE', 'Source must be text');
  if (bytes(JSON.stringify(bundle)) > LIMITS.bundleBytes || bytes(source) > LIMITS.sourceBytes || bytes(css) > LIMITS.cssBytes) fail('SIZE', 'Source limit exceeded');
  const tree = parseSource(source);
  return { policyVersion: POLICY, tree, css: validateCss(css) };
}
export function validateData(data) {
  exactKeys(data, ['displayName', 'bio'], 'Profile data');
  for (const value of Object.values(data)) if (typeof value !== 'string' || bytes(value) > LIMITS.textBytes) fail('DATA', 'Invalid profile text');
  return data;
}
function parseSource(source) {
  const file = ts.createSourceFile('Profile.tsx', source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  if (file.parseDiagnostics.length) fail('SYNTAX', 'Malformed TSX');
  if (file.statements.length !== 1) fail('MODULE', 'Only a single default Profile function is supported');
  const fn = file.statements[0];
  if (!ts.isFunctionDeclaration(fn) || fn.name?.text !== 'Profile' || fn.asteriskToken || fn.typeParameters || fn.type || fn.modifiers?.length !== 2 ||
      fn.modifiers[0].kind !== ts.SyntaxKind.ExportKeyword || fn.modifiers[1].kind !== ts.SyntaxKind.DefaultKeyword) fail('MODULE', 'Expected export default function Profile');
  const p = fn.parameters;
  if (p.length !== 1 || p[0].type || p[0].initializer || p[0].dotDotDotToken || p[0].modifiers ||
      !ts.isObjectBindingPattern(p[0].name) || p[0].name.elements.length !== 1) fail('PARAMETER', 'Expected { data } parameter');
  const binding = p[0].name.elements[0];
  if (!ts.isIdentifier(binding.name) || binding.name.text !== 'data' || binding.propertyName || binding.initializer || binding.dotDotDotToken) fail('PARAMETER', 'Expected { data } parameter');
  if (!fn.body || fn.body.statements.length !== 1 || !ts.isReturnStatement(fn.body.statements[0])) fail('EXECUTION', 'Only a JSX return is allowed');
  let expression = fn.body.statements[0].expression;
  while (expression && ts.isParenthesizedExpression(expression)) expression = expression.expression;
  let count = 0;
  function visit(node, depth = 0) {
    if (!node || ++count > LIMITS.nodes || depth > LIMITS.depth) fail('COMPLEXITY', 'Tree limit exceeded');
    if (ts.isJsxText(node)) return { kind: 'text', value: node.text.replace(/\s+/g, ' ').trim() };
    if (ts.isJsxExpression(node)) {
      const e = node.expression;
      if (!node.dotDotDotToken && e && ts.isPropertyAccessExpression(e) && !e.questionDotToken && ts.isIdentifier(e.expression) && e.expression.text === 'data' && ['displayName', 'bio'].includes(e.name.text)) return { kind: 'data', field: e.name.text };
      fail('EXPRESSION', 'Only data.displayName and data.bio expressions are allowed');
    }
    if (!ts.isJsxElement(node) && !ts.isJsxSelfClosingElement(node)) fail('ELEMENT', 'Expected declarative JSX element');
    const open = ts.isJsxElement(node) ? node.openingElement : node;
    if (!ts.isIdentifier(open.tagName) || !tags.has(open.tagName.text) || open.typeArguments) fail('TAG', 'Unsupported element');
    const tag = open.tagName.text, props = {};
    if (ts.isJsxElement(node) && node.closingElement.tagName.getText(file) !== tag) fail('TAG', 'Mismatched closing tag');
    for (const attribute of open.attributes.properties) {
      if (!ts.isJsxAttribute(attribute) || !attributes.has(attribute.name.getText(file)) || !attribute.initializer || !ts.isStringLiteral(attribute.initializer)) fail('ATTRIBUTE', 'Only supported string attributes are allowed');
      const key = attribute.name.getText(file);
      if (Object.hasOwn(props, key)) fail('ATTRIBUTE', 'Duplicate attribute');
      props[key] = attribute.initializer.text;
    }
    return { kind: 'element', tag, props, children: ts.isJsxElement(node) ? node.children.map(child => visit(child, depth + 1)).filter(child => child.kind !== 'text' || child.value) : [] };
  }
  const tree = visit(expression);
  if (tree.kind !== 'element') fail('ELEMENT', 'Root must be an element');
  return tree;
}
function validateCss(css) {
  if (/[<>\\\\]/.test(css)) fail('CSS_ENCODING', 'HTML delimiters and CSS escapes are unsupported');
  let root;
  try { root = postcss.parse(css); } catch { fail('CSS_SYNTAX', 'Malformed CSS'); }
  let rules = 0, declarations = 0;
  root.walk(node => {
    if (node.type === 'root' || node.type === 'comment') return;
    if (node.type === 'rule') {
      if (node.parent.type !== 'root' || ++rules > LIMITS.cssRules || node.selector.length > 256 || !/^[a-zA-Z0-9.# ,>+~-]+$/.test(node.selector)) fail('CSS_SELECTOR', 'Unsupported selector');
      return;
    }
    if (node.type !== 'decl' || node.parent.type !== 'rule' || ++declarations > 500 || !properties.has(node.prop) || node.important || node.value.length > 256 || /[\\\\<>]/.test(node.value)) fail('CSS_POLICY', 'Unsupported CSS construct');
    valueParser(node.value).walk(value => {
      if (value.type === 'function' && !functions.has(value.value.toLowerCase())) fail('CSS_FUNCTION', 'Unsupported CSS function');
      if (value.type === 'word' && /url|expression|javascript|https?:|data:/i.test(value.value)) fail('CSS_VALUE', 'Unsupported CSS value');
    });
  });
  return root.toString();
}


