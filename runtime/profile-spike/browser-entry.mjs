import { createRoot } from 'react-dom/client';
import { renderNode } from './renderer.mjs';
const profile = JSON.parse(document.getElementById('profile-data').textContent);
createRoot(document.getElementById('root')).render(renderNode(profile.tree, profile.data));

