import { createRoot } from 'react-dom/client';
import type { ProfileData, ProfileTree } from '../../contracts/src/schemas.js';
import { renderProfile } from './render.js';
const payload = document.getElementById('profile-data');
const container = document.getElementById('root');
if (!payload || !container) throw new Error('Missing trusted artifact payload');
// The artifact producer validates and escapes this payload; clients never authorize publication.
const profile = JSON.parse(payload.textContent ?? '') as { tree: ProfileTree; data: ProfileData };
createRoot(container).render(renderProfile(profile.tree, profile.data));
