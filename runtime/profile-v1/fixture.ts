import { VERSIONS } from '../contracts/src/policy.js';
import type { ProfileData, SourceBundle } from '../contracts/src/schemas.js';
export const sourceFixture: SourceBundle = {
  manifest: {
    schemaVersion: VERSIONS.source,
    runtimeVersion: VERSIONS.runtime,
    policyVersion: VERSIONS.policy,
    entryPoint: 'Profile.tsx',
  },
  files: {
    'Profile.tsx':
      'export default function Profile({ data }) { return <main className="profile"><h1>{data.displayName}</h1><p>{data.bio}</p>{data.sections}</main>; }',
    'styles.css':
      '.profile { color: #eeeeff; background: #19152d; padding: 32px; font-family: sans-serif; } h1 { font-size: 40px; } .ourspace-section { padding: 16px; background: #30284a; border-radius: 16px; margin-top: 16px; }',
  },
};
export const dataFixture: ProfileData = {
  schemaVersion: VERSIONS.data,
  displayName: 'Alex',
  bio: 'My corner of OurSpace.',
  sections: [
    {
      id: 'about',
      heading: 'About me',
      body: 'A flexible profile, rendered with a shared runtime.',
    },
  ],
};
