'use client';

import type { CSSProperties } from 'react';
import { getMe, signOut, type CurrentUser } from '../lib/api';
import { useEffect, useState } from 'react';

// ── Auth guard hook ───────────────────────────────────────────────────────────

export type AuthState =
  | { kind: 'loading' }
  | { kind: 'unauthenticated' }
  | { kind: 'ready'; user: CurrentUser };

export function useAuth(): AuthState {
  const [state, setState] = useState<AuthState>({ kind: 'loading' });

  useEffect(() => {
    getMe()
      .then((user) => {
        if (!user) {
          window.location.replace('/');
        } else {
          setState({ kind: 'ready', user });
        }
      })
      .catch(() => {
        window.location.replace('/');
      });
  }, []);

  return state;
}

// ── Shell ─────────────────────────────────────────────────────────────────────

export function Shell({ user, children }: { user: CurrentUser; children: React.ReactNode }) {
  const displayName = user.name ?? user.userId.slice(0, 8) + '…';
  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', display: 'flex', flexDirection: 'column' }}>
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
        <span style={{ fontSize: 14, fontWeight: 700, color: 'var(--accent)', letterSpacing: '0.06em' }}>
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

// ── Spinner ───────────────────────────────────────────────────────────────────

export function Spinner() {
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

// ── Error banner ──────────────────────────────────────────────────────────────

export function ErrorBanner({ message }: { message: string }) {
  return (
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
      {message}
    </div>
  );
}

// ── Shared styles ─────────────────────────────────────────────────────────────

export const centered: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  minHeight: '100vh',
};

export const accentBtn: CSSProperties = {
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

export const ghostBtn: CSSProperties = {
  fontSize: 13,
  color: 'var(--text-muted)',
  background: 'none',
  border: '1px solid var(--border)',
  padding: '5px 12px',
  borderRadius: 6,
  cursor: 'pointer',
};

export const input: CSSProperties = {
  width: '100%',
  background: 'var(--surface)',
  border: '1px solid var(--border)',
  color: 'var(--text)',
  borderRadius: 8,
  padding: '10px 14px',
  fontSize: 15,
  outline: 'none',
};

export const label: CSSProperties = {
  display: 'block',
  fontSize: 13,
  fontWeight: 600,
  color: 'var(--text-muted)',
  marginBottom: 8,
};
