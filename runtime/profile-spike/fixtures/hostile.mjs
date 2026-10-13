export function hostileFixtures(valid) {
  const source = valid.files['Profile.tsx'];
  const sourceCase = (name, code) => ({ name, bundle: { ...valid, files: { ...valid.files, 'Profile.tsx': code } } });
  const cssCase = (name, css) => ({ name, bundle: { ...valid, files: { ...valid.files, 'styles.css': css } } });
  return [
    sourceCase('network call', source.replace('{data.bio}', '{fetch("https://example.invalid/steal")}')),
    sourceCase('computed network access', source.replace('{data.bio}', '{globalThis["fetch"]("/steal")}')),
    sourceCase('cookie access', source.replace('{data.bio}', '{document.cookie}')),
    sourceCase('storage access', source.replace('{data.bio}', '{localStorage.getItem("token")}')),
    sourceCase('infinite loop', 'export default function Profile({ data }) { while (true) {} return <main/>; }'),
    sourceCase('recursive expression', source.replace('{data.bio}', '{Profile({data})}')),
    sourceCase('import execution', 'import fs from "node:fs";\n' + source),
    sourceCase('dynamic import', source.replace('{data.bio}', '{import("node:fs")}')),
    sourceCase('event handler', source.replace('<main ', '<main onClick="steal()" ')),
    sourceCase('spread properties', source.replace('<main ', '<main {...data} ')),
    sourceCase('raw HTML', source.replace('<main ', '<main dangerouslySetInnerHTML="{{__html: data.bio}}" ')),
    sourceCase('iframe', source.replace('<footer>Made with OurSpace</footer>', '<iframe/>')),
    sourceCase('external image', source.replace('<footer>Made with OurSpace</footer>', '<img src="https://example.invalid/steal"/>')),
    sourceCase('script tag', source.replace('<footer>Made with OurSpace</footer>', '<script/>')),
    sourceCase('custom component', source.replace('<footer>Made with OurSpace</footer>', '<Secret/>')),
    sourceCase('malformed JSX', 'export default function Profile({ data }) { return <main><section></main>; }'),
    sourceCase('parameter initializer', 'export default function Profile({ data } = fetch("/steal")) { return <main/>; }'),
    sourceCase('nested too deep', 'export default function Profile({ data }) { return ' + '<div>'.repeat(26) + 'x' + '</div>'.repeat(26) + '; }'),
    sourceCase('too many nodes', 'export default function Profile({ data }) { return <main>' + '<br/>'.repeat(501) + '</main>; }'),
    sourceCase('oversized source', source + ' '.repeat(65537)),
    cssCase('CSS import', '@import "https://example.invalid/style.css";'),
    cssCase('CSS url', '.profile { background: url(https://example.invalid/steal); }'),
    cssCase('escaped CSS URL', String.raw`.profile { background: u\\72l(https://example.invalid/steal); }`),
    cssCase('CSS expression', '.profile { width: expression(alert(1)); }'),
    cssCase('CSS variable escape', '.profile { color: var(--secret); }'),
    cssCase('CSS HTML breakout', '/* </style><script>alert(1)</script> */'),
    { name: 'path traversal', bundle: { ...valid, files: { ...valid.files, '../outside.tsx': 'bad' } } },
    { name: 'unsupported version', bundle: { ...valid, manifest: { ...valid.manifest, runtimeVersion: 'future' } } }
  ];
}

