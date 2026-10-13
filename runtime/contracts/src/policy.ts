export const VERSIONS = Object.freeze({
  source: 'profile-source-v1',
  tree: 'profile-tree-v1',
  data: 'profile-data-v1',
  runtime: 'profile-runtime-v1',
  policy: 'profile-policy-v1',
  artifact: 'profile-artifact-v1',
});
export const LIMITS = Object.freeze({
  bundleBytes: 65536,
  sourceBytes: 32768,
  cssBytes: 16384,
  nodes: 500,
  depth: 24,
  cssRules: 100,
  cssDeclarations: 500,
  textBytes: 16384,
  sections: 20,
  selectorChars: 256,
  valueChars: 256,
});
export const TAGS = [
  'main',
  'header',
  'footer',
  'section',
  'article',
  'div',
  'span',
  'p',
  'h1',
  'h2',
  'h3',
  'h4',
  'ul',
  'ol',
  'li',
  'strong',
  'em',
  'small',
  'br',
  'hr',
] as const;
export const ATTRIBUTES = ['className', 'id', 'title', 'role', 'aria-label'] as const;
export const DATA_FIELDS = ['displayName', 'bio'] as const;
export const CSS_PROPERTIES =
  'color background background-color padding padding-top padding-bottom padding-left padding-right margin margin-top margin-bottom margin-left margin-right border border-color border-width border-style border-radius display gap row-gap column-gap grid-template-columns grid-template-rows flex-direction flex-wrap align-items justify-content width max-width min-width height min-height max-height font-family font-size font-weight line-height letter-spacing text-align text-decoration box-shadow opacity'.split(
    ' ',
  );
export const CSS_FUNCTIONS = [
  'rgb',
  'rgba',
  'hsl',
  'hsla',
  'min',
  'max',
  'clamp',
  'repeat',
] as const;
export const CAPABILITIES = Object.freeze({
  tags: TAGS,
  attributes: ATTRIBUTES,
  dataFields: DATA_FIELDS,
  sectionsBinding: true,
  cssProperties: CSS_PROPERTIES,
  cssFunctions: CSS_FUNCTIONS,
  limits: LIMITS,
});
