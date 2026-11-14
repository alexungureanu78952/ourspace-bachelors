'use client';

import { useState, useEffect, useCallback, type CSSProperties } from 'react';
import {
  getMe,
  getMyProfile,
  createProfile,
  signOut,
  type Profile,
  type CurrentUser,
} from '../lib/api';

type AppState =
  | { kind: 'loading' }
  | { kind: 'unauthenticated' }
  | { kind: 'creating'; user: CurrentUser; error?: string }
  | { kind: 'profile'; user: CurrentUser; profile: Profile };

export default function Home() {
  const [state, setState] = useState<AppState>({ kind: 'loading' });

  useEffect(() => {
    (async () => {
      const user = await getMe();
      if (!user) return setState({ kind: 'unauthenticated' });
      const profile = await getMyProfile();
      setState(profile ? { kind: 'profile', user, profile } : { kind: 'creating', user });
    })().catch(() => setState({ kind: 'unauthenticated' }));
  }, []);

  const handleCreated = useCallback((user: CurrentUser, profile: Profile) => {
    setState({ kind: 'profile', user, profile });
  }, []);

  if (state.kind === 'loading') return <Spinner />;
  if (state.kind === 'unauthenticated') return <Landing />;
  if (state.kind === 'creating')
    return (
      <Shell user={state.user}>
        <CreateForm onCreated={(p) => handleCreated(state.user, p)} initialError={state.error} />
      </Shell>
    );
  return (
    <Shell user={state.user}>
      <ProfileView profile={state.profile} />
    </Shell>
  );
}

// ── Loading ───────────────────────────────────────────────────────────────────

function Spinner() {
  return (
    <div style={centered}>
      <div
        style={{
          width: 36,
          height: 36,
          border: '3px solid var(--border)',
          borderTopColor: 'var(--accent)',
          borderRadius: '50%',
          animation: 'spin 0.75s linear infinite',
        }}
      />
    </div>
  );
}

// ── Landing ───────────────────────────────────────────────────────────────────

function Landing() {
  return (
    <div style={{ ...centered, flexDirection: 'column', textAlign: 'center', padding: '0 20px' }}>
      <p
        style={{
          fontSize: 12,
          letterSpacing: '0.18em',
          textTransform: 'uppercase',
          color: 'var(--accent)',
          fontWeight: 700,
          marginBottom: 20,
        }}
      >
        ✦ OurSpace
      </p>
      <h1
        style={{
          fontSize: 'clamp(2.2rem, 7vw, 3.8rem)',
          fontWeight: 800,
          lineHeight: 1.08,
          marginBottom: 20,
          maxWidth: 460,
        }}
      >
        Your space.
        <br />
        Your story.
      </h1>
      <p
        style={{
          fontSize: 17,
          color: 'var(--text-muted)',
          marginBottom: 44,
          maxWidth: 380,
          lineHeight: 1.6,
        }}
      >
        A place to share who you are. Create your profile once, own it forever.
      </p>
      <a href="/bff/login?returnUrl=/" style={accentBtn}>
        Sign in
      </a>
      <p style={{ marginTop: 16, fontSize: 13, color: 'var(--text-muted)' }}>
        New here? Create an account from the sign-in page.
      </p>
    </div>
  );
}

// ── Authenticated shell ───────────────────────────────────────────────────────

function Shell({ user, children }: { user: CurrentUser; children: React.ReactNode }) {
  const displayName = user.name ?? user.userId.slice(0, 8) + '…';
  return (
    <div
      style={{
        minHeight: '100vh',
        background: 'var(--bg)',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <header
        style={{
          borderBottom: '1px solid var(--border)',
          padding: '0 24px',
          height: 56,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexShrink: 0,
        }}
      >
        <span
          style={{ fontSize: 14, fontWeight: 700, color: 'var(--accent)', letterSpacing: '0.06em' }}
        >
          ✦ OurSpace
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <span
            style={{
              fontSize: 13,
              color: 'var(--text-muted)',
              maxWidth: 220,
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {displayName}
          </span>
          <button onClick={() => void signOut()} style={ghostBtn}>
            Sign out
          </button>
        </div>
      </header>
      <main
        style={{
          flex: 1,
          maxWidth: 580,
          width: '100%',
          margin: '0 auto',
          padding: '48px 20px',
          animation: 'fadeIn 0.2s ease',
        }}
      >
        {children}
      </main>
    </div>
  );
}

// ── Create form ───────────────────────────────────────────────────────────────

function CreateForm({
  onCreated,
  initialError,
}: {
  onCreated: (p: Profile) => void;
  initialError?: string;
}) {
  const [displayName, setDisplayName] = useState('');
  const [bio, setBio] = useState('');
  const [error, setError] = useState(initialError ?? '');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!displayName.trim()) {
      setError('Display name is required.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      onCreated(await createProfile(displayName.trim(), bio));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h2 style={{ fontSize: 26, fontWeight: 800, marginBottom: 8 }}>Create your profile</h2>
      <p style={{ color: 'var(--text-muted)', fontSize: 14, marginBottom: 36, lineHeight: 1.5 }}>
        You get one profile per account — make it yours.
      </p>

      {error && (
        <div
          style={{
            background: 'var(--error-bg)',
            border: '1px solid var(--error-border)',
            color: 'var(--error-text)',
            borderRadius: 8,
            padding: '12px 16px',
            marginBottom: 28,
            fontSize: 14,
          }}
        >
          {error}
        </div>
      )}

      <form
        onSubmit={(e) => void handleSubmit(e)}
        style={{ display: 'flex', flexDirection: 'column', gap: 24 }}
      >
        <div>
          <label style={label}>Display name</label>
          <input
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            maxLength={200}
            placeholder="How should we call you?"
            style={input}
            required
          />
        </div>

        <div>
          <label style={label}>
            Bio <span style={{ fontWeight: 400, color: 'var(--text-muted)' }}>— optional</span>
          </label>
          <textarea
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            maxLength={500}
            rows={5}
            placeholder="Tell people a bit about yourself…"
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
          {loading ? 'Creating…' : 'Create profile'}
        </button>
      </form>
    </div>
  );
}

// ── Profile view ──────────────────────────────────────────────────────────────

function ProfileView({ profile }: { profile: Profile }) {
  const createdAt = new Date(profile.createdAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

  return (
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
  );
}

function Field({
  label: labelText,
  value,
  large,
}: {
  label: string;
  value: string;
  large?: boolean;
}) {
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
        {labelText}
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

// ── Shared styles ─────────────────────────────────────────────────────────────

const centered: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  minHeight: '100vh',
};

const accentBtn: CSSProperties = {
  display: 'inline-block',
  background: 'var(--accent)',
  color: '#0f0e17',
  fontWeight: 700,
  fontSize: 15,
  padding: '12px 28px',
  borderRadius: 8,
  border: 'none',
  textDecoration: 'none',
  cursor: 'pointer',
  transition: 'background 0.15s',
};

const ghostBtn: CSSProperties = {
  fontSize: 13,
  color: 'var(--text-muted)',
  background: 'none',
  border: '1px solid var(--border)',
  padding: '5px 12px',
  borderRadius: 6,
  cursor: 'pointer',
};

const input: CSSProperties = {
  width: '100%',
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  color: 'var(--text)',
  borderRadius: 8,
  padding: '10px 14px',
  fontSize: 15,
  outline: 'none',
};

const label: CSSProperties = {
  display: 'block',
  fontSize: 13,
  fontWeight: 600,
  color: 'var(--text-muted)',
  marginBottom: 8,
};
