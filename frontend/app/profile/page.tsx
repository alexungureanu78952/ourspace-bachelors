'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getMyProfile, type Profile } from '../../lib/api';
import { useAuth, Shell, Spinner, ghostBtn } from '../../components/shared';

export default function ProfilePage() {
  const auth = useAuth();
  const router = useRouter();
  const [profile, setProfile] = useState<Profile | null | undefined>(undefined);

  useEffect(() => {
    if (auth.kind !== 'ready') return;
    getMyProfile()
      .then((p) => {
        setProfile(p);
        if (!p) router.replace('/create');
      })
      .catch(() => {
        setProfile(null);
        router.replace('/');
      });
  }, [auth.kind, router]);

  if (auth.kind === 'loading') return <Spinner />;
  if (auth.kind !== 'ready') return null;
  if (profile === undefined) return <Spinner />;
  if (profile === null) return null;

  const createdAt = new Date(profile.createdAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

  return (
    <Shell user={auth.user}>
      <div>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'flex-start',
            marginBottom: 32,
            flexWrap: 'wrap',
            gap: 8,
          }}
        >
          <div>
            <h2 style={{ fontSize: 26, fontWeight: 800 }}>Your profile</h2>
            <p style={{ color: 'var(--text-muted)', fontSize: 13, marginTop: 4 }}>
              Created {createdAt}
            </p>
          </div>
          <button onClick={() => router.push('/edit')} style={ghostBtn}>
            Edit
          </button>
        </div>

        <div
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            borderRadius: 14,
            overflow: 'hidden',
            marginBottom: 20,
          }}
        >
          <iframe
            src={`/bff/profiles/${profile.id}/view`}
            sandbox="allow-scripts"
            title="Profile preview"
            style={{ width: '100%', height: 440, border: 'none', display: 'block' }}
          />
        </div>

        <div
          style={{
            background: 'var(--surface)',
            border: '1px solid var(--border)',
            borderRadius: 14,
            padding: '22px 24px',
            display: 'flex',
            flexDirection: 'column',
            gap: 16,
          }}
        >
          <Field label="Display name" value={profile.displayName} large />
          {profile.bio && <Field label="Bio" value={profile.bio} />}
        </div>
      </div>
    </Shell>
  );
}

function Field({ label, value, large }: { label: string; value: string; large?: boolean }) {
  return (
    <div>
      <span
        style={{
          fontSize: 11,
          fontWeight: 600,
          letterSpacing: '0.1em',
          textTransform: 'uppercase',
          color: 'var(--text-muted)',
        }}
      >
        {label}
      </span>
      <p
        style={{
          marginTop: 5,
          fontSize: large ? 20 : 15,
          fontWeight: large ? 700 : 400,
          color: 'var(--text)',
          lineHeight: 1.55,
          whiteSpace: 'pre-wrap',
        }}
      >
        {value}
      </p>
    </div>
  );
}
