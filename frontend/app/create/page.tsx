'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { getMyProfile, createProfile } from '../../lib/api';
import {
  useAuth,
  Shell,
  Spinner,
  ErrorBanner,
  accentBtn,
  input,
  label,
} from '../../components/shared';
import { useTranslation } from '../../lib/i18n';

export default function CreatePage() {
  const auth = useAuth();
  const router = useRouter();
  const [hasProfile, setHasProfile] = useState<boolean | undefined>(undefined);

  useEffect(() => {
    if (auth.kind !== 'ready') return;
    getMyProfile()
      .then((p) => {
        setHasProfile(p !== null);
        if (p) router.replace('/profile');
      })
      .catch(() => {
        setHasProfile(false);
        router.replace('/');
      });
  }, [auth.kind, router]);

  if (auth.kind === 'loading') return <Spinner />;
  if (auth.kind !== 'ready') return null;
  if (hasProfile === undefined) return <Spinner />;
  if (hasProfile) return null;

  return (
    <Shell user={auth.user}>
      <CreateForm onCreated={() => router.push('/profile')} />
    </Shell>
  );
}

function CreateForm({ onCreated }: { onCreated: () => void }) {
  const { t } = useTranslation();
  const c = t.create;
  const [displayName, setDisplayName] = useState('');
  const [bio, setBio] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!displayName.trim()) {
      setError(c.displayNameRequired);
      return;
    }
    setLoading(true);
    setError('');
    try {
      await createProfile(displayName.trim(), bio);
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : c.fallbackError);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h2 style={{ fontSize: 26, fontWeight: 800, marginBottom: 8 }}>{c.title}</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: 14, marginBottom: 36, lineHeight: 1.5 }}>
        {c.subtitle}
      </p>

      {error && <ErrorBanner message={error} />}

      <form
        onSubmit={(e) => void handleSubmit(e)}
        style={{ display: 'flex', flexDirection: 'column', gap: 24 }}
      >
        <div>
          <label htmlFor="create-display-name" style={label}>
            {c.displayName}
          </label>
          <input
            id="create-display-name"
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            maxLength={200}
            placeholder={c.displayNamePlaceholder}
            style={input}
            required
          />
        </div>

        <div>
          <label htmlFor="create-bio" style={label}>
            {c.bio}{' '}
            <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>{c.bioOptional}</span>
          </label>
          <textarea
            id="create-bio"
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            maxLength={500}
            rows={5}
            placeholder={c.bioPlaceholder}
            style={{ ...input, resize: 'vertical', minHeight: 110 }}
          />
          <div
            style={{ textAlign: 'right', fontSize: 12, color: 'var(--text-muted)', marginTop: 6 }}
          >
            {bio.length} / 500
          </div>
        </div>

        <button
          type="submit"
          disabled={loading}
          style={{
            ...accentBtn,
            opacity: loading ? 0.65 : 1,
            cursor: loading ? 'not-allowed' : 'pointer',
            alignSelf: 'flex-start',
          }}
        >
          {loading ? c.submitting : c.submit}
        </button>
      </form>
    </div>
  );
}
