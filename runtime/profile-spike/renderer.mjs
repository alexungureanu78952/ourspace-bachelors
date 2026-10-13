import React from 'react';
export function renderNode(node, data, key = 'root') {
  if (node.kind === 'text') return node.value;
  if (node.kind === 'data') return data[node.field];
  return React.createElement(node.tag, { ...node.props, key }, node.children.map((child, index) => renderNode(child, data, String(index))));
}

