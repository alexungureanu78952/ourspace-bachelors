import { createElement } from 'react';
import type { ReactNode } from 'react';
import type { ProfileData, ProfileNode, ProfileTree } from '../../contracts/src/schemas.js';
// This renderer accepts validated contracts from a trusted producer, not arbitrary browser input.
export function renderProfile(tree: ProfileTree, data: ProfileData): ReactNode {
  function render(node: ProfileNode, key: string): ReactNode {
    switch (node.kind) {
      case 'text':
        return node.value;
      case 'data':
        return data[node.field];
      case 'sections':
        return data.sections.map((section) =>
          createElement(
            'section',
            { key: section.id, className: 'ourspace-section' },
            createElement('h2', null, section.heading),
            createElement('p', null, section.body),
          ),
        );
      case 'element':
        return createElement(
          node.tag,
          { ...node.props, key },
          ...node.children.map((child, index) => render(child, key + '.' + String(index))),
        );
    }
  }
  return render(tree.root, 'root');
}
