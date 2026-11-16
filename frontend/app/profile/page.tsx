'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getMyProfile, type Profile } from '../../lib/api';
import { useAuth, Shell, Spinner, ghostBtn } from '../../components/shared';
import { useTranslation, dateLocaleMap } from '../../lib/i18n';

export default function ProfilePage() {
  const auth = useAuth();
  const router = useRouter();
  const { t, locale } = useTranslation();
  const p = t.profile;
  const [profile, setProfile] = useState<Profile | null | undefined>(undefined);

  useEffect(() => {
    if (auth.kind !== 'ready') return;
    getMyProfile()
      .then((prof) => {
        setProfile(prof);
        if (!prof) router.replace('/create');
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

  const createdAt = new Date(profile.createdAt).toLocaleDateString(dateLocaleMap[locale], {
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
            <h2 style={{ fontSize: 26, fontWeight: 800 }}>{p.title}</h2>
            <p style={{ color: 'var(--text-muted)', fontSize: 13, marginTop: 4 }}>
              {p.created} {createdAt}
            </p>
          </div>
          <button onClick={() => router.push('/edit')} style={ghostBtn}>
            {p.edit}
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
            title={p.previewTitle}
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
          <Field label={p.displayName} value={profile.displayName} large />
          {profile.bio && <Field label={p.bio} value={profile.bio} />}
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
