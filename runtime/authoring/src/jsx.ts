import ts from 'typescript';
import { decodeHTMLStrict } from 'entities';
import { fail } from '../../contracts/src/diagnostics.js';
import { ATTRIBUTES, DATA_FIELDS, LIMITS, TAGS, VERSIONS } from '../../contracts/src/policy.js';
import { propsSchema } from '../../contracts/src/schemas.js';
import type { ProfileNode, ProfileTree } from '../../contracts/src/schemas.js';
export function parseJsx(source: string): ProfileTree {
  if (Buffer.byteLength(source, 'utf8') > LIMITS.sourceBytes)
    fail('SIZE_LIMIT', 'TSX exceeds byte limit', 'Profile.tsx');
  let file: ts.SourceFile;
  try {
    file = ts.createSourceFile(
      'Profile.tsx',
      source,
      ts.ScriptTarget.Latest,
      true,
      ts.ScriptKind.TSX,
    );
  } catch {
    fail('SOURCE_SYNTAX', 'Unable to parse source within parser limits', 'Profile.tsx');
  }
  // TypeScript 6 parser diagnostics are exposed at runtime but omitted from SourceFile's public type.
  const diagnostics = (file as ts.SourceFile & { parseDiagnostics: readonly ts.Diagnostic[] })
    .parseDiagnostics;
  if (diagnostics.length) fail('SOURCE_SYNTAX', 'Malformed TSX', 'Profile.tsx');
  const reject = (code: string, message: string, node: ts.Node): never => {
    const location = file.getLineAndCharacterOfPosition(node.getStart(file));
    return fail(
      code,
      message,
      'Profile.tsx:' + String(location.line + 1) + ':' + String(location.character + 1),
    );
  };
  const fn = file.statements[0];
  if (file.statements.length !== 1 || !fn || !ts.isFunctionDeclaration(fn))
    fail('SOURCE_MODULE', 'Only a default Profile function is supported', 'Profile.tsx');
  if (
    fn.name?.text !== 'Profile' ||
    fn.asteriskToken ||
    fn.typeParameters ||
    fn.type ||
    fn.modifiers?.length !== 2 ||
    fn.modifiers[0]?.kind !== ts.SyntaxKind.ExportKeyword ||
    fn.modifiers[1]?.kind !== ts.SyntaxKind.DefaultKeyword
  )
    reject('SOURCE_MODULE', 'Expected export default function Profile', fn);
  const parameter = fn.parameters[0];
  if (
    fn.parameters.length !== 1 ||
    !parameter ||
    parameter.type ||
    parameter.initializer ||
    parameter.questionToken ||
    parameter.dotDotDotToken ||
    parameter.modifiers ||
    !ts.isObjectBindingPattern(parameter.name) ||
    parameter.name.elements.length !== 1
  )
    return reject('SOURCE_PARAMETER', 'Expected { data } parameter', fn);
  const binding = parameter.name.elements[0];
  if (
    !binding ||
    !ts.isIdentifier(binding.name) ||
    binding.name.text !== 'data' ||
    binding.propertyName ||
    binding.initializer ||
    binding.dotDotDotToken
  )
    reject('SOURCE_PARAMETER', 'Expected { data } parameter', parameter);
  const statement = fn.body?.statements[0];
  if (
    fn.body?.statements.length !== 1 ||
    !statement ||
    !ts.isReturnStatement(statement) ||
    !statement.expression
  )
    return reject('SOURCE_EXECUTION', 'Only a JSX return is allowed', fn);
  let expression = statement.expression;
  while (ts.isParenthesizedExpression(expression)) expression = expression.expression;
  let count = 0;
  function visit(node: ts.Node, depth = 0): ProfileNode {
    if (++count > LIMITS.nodes || depth > LIMITS.depth)
      reject('TREE_COMPLEXITY', 'Tree exceeds limits', node);
    if (ts.isJsxText(node))
      return { kind: 'text', value: decodeHTMLStrict(node.text.replace(/\s+/g, ' ')) };
    if (ts.isJsxExpression(node)) {
      const e = node.expression;
      if (
        !node.dotDotDotToken &&
        e &&
        ts.isPropertyAccessExpression(e) &&
        !e.questionDotToken &&
        ts.isIdentifier(e.expression) &&
        e.expression.text === 'data'
      ) {
        if (e.name.text === 'sections') return { kind: 'sections' };
        const field = DATA_FIELDS.find((field) => field === e.name.text);
        if (field) return { kind: 'data', field };
      }
      return reject('SOURCE_EXPRESSION', 'Only supported data bindings are allowed', node);
    }
    if (!ts.isJsxElement(node) && !ts.isJsxSelfClosingElement(node))
      return reject('SOURCE_ELEMENT', 'Expected a supported JSX element', node);
    const open = ts.isJsxElement(node) ? node.openingElement : node;
    const tag = ts.isIdentifier(open.tagName)
      ? TAGS.find((tag) => tag === open.tagName.getText(file))
      : undefined;
    if (!tag || open.typeArguments) return reject('SOURCE_TAG', 'Unsupported element', open);
    if (ts.isJsxElement(node) && node.closingElement.tagName.getText(file) !== tag)
      reject('SOURCE_TAG', 'Mismatched closing element', node);
    const props: Partial<Record<(typeof ATTRIBUTES)[number], string | undefined>> = {};
    for (const attribute of open.attributes.properties) {
      if (
        !ts.isJsxAttribute(attribute) ||
        !attribute.initializer ||
        !ts.isStringLiteral(attribute.initializer)
      )
        return reject(
          'SOURCE_ATTRIBUTE',
          'Only supported string attributes are allowed',
          attribute,
        );
      const key = ATTRIBUTES.find((key) => key === attribute.name.getText(file));
      if (!key || Object.hasOwn(props, key))
        return reject('SOURCE_ATTRIBUTE', 'Unsupported or duplicate attribute', attribute);
      props[key] = decodeHTMLStrict(attribute.initializer.text);
    }
    const children = ts.isJsxElement(node)
      ? node.children
          .map((child) => visit(child, depth + 1))
          .filter((child) => child.kind !== 'text' || child.value.trim())
      : [];
    const parsedProps = propsSchema.safeParse(props);
    if (!parsedProps.success)
      return reject('SOURCE_ATTRIBUTE', 'Unsupported attribute value', open);
    return { kind: 'element', tag, props: parsedProps.data, children };
  }
  const root = visit(expression);
  if (root.kind !== 'element') fail('TREE_ROOT', 'Root must be an element', 'Profile.tsx');
  return { schemaVersion: VERSIONS.tree, root };
}
