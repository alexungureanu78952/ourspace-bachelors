'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { getMyProfile, updateProfile, type Profile } from '../../lib/api';
import {
  useAuth,
  Shell,
  Spinner,
  ErrorBanner,
  accentBtn,
  ghostBtn,
  input,
  label,
} from '../../components/shared';
import { useTranslation } from '../../lib/i18n';

export default function EditPage() {
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

  return (
    <Shell user={auth.user}>
      <EditForm
        profile={profile}
        onSaved={() => router.push('/profile')}
        onCancel={() => router.push('/profile')}
      />
    </Shell>
  );
}

function EditForm({
  profile,
  onSaved,
  onCancel,
}: {
  profile: Profile;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const e = t.edit;
  const [displayName, setDisplayName] = useState(profile.displayName);
  const [bio, setBio] = useState(profile.bio);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(ev: React.FormEvent) {
    ev.preventDefault();
    if (!displayName.trim()) {
      setError(e.displayNameRequired);
      return;
    }
    setLoading(true);
    setError('');
    try {
      await updateProfile(displayName.trim(), bio);
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : e.fallbackError);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h2 style={{ fontSize: 26, fontWeight: 800, marginBottom: 8 }}>{e.title}</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: 14, marginBottom: 36, lineHeight: 1.5 }}>
        {e.subtitle}
      </p>

      {error && <ErrorBanner message={error} />}

      <form
        onSubmit={(ev) => void handleSubmit(ev)}
        style={{ display: 'flex', flexDirection: 'column', gap: 24 }}
      >
        <div>
          <label htmlFor="edit-display-name" style={label}>
            {e.displayName}
          </label>
          <input
            id="edit-display-name"
            type="text"
            value={displayName}
            onChange={(ev) => setDisplayName(ev.target.value)}
            maxLength={200}
            style={input}
            required
          />
        </div>

        <div>
          <label htmlFor="edit-bio" style={label}>
            {e.bio}{' '}
            <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>{e.bioOptional}</span>
          </label>
          <textarea
            id="edit-bio"
            value={bio}
            onChange={(ev) => setBio(ev.target.value)}
            maxLength={500}
            rows={5}
            style={{ ...input, resize: 'vertical', minHeight: 110 }}
          />
          <div
            style={{ textAlign: 'right', fontSize: 12, color: 'var(--text-muted)', marginTop: 6 }}
          >
            {bio.length} / 500
          </div>
        </div>

        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <button
            type="submit"
            disabled={loading}
            style={{
              ...accentBtn,
              opacity: loading ? 0.65 : 1,
              cursor: loading ? 'not-allowed' : 'pointer',
            }}
          >
            {loading ? e.submitting : e.submit}
          </button>
          <button type="button" onClick={onCancel} style={ghostBtn} disabled={loading}>
            {e.cancel}
          </button>
        </div>
      </form>
    </div>
  );
}
