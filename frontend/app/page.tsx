'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getMe } from '../lib/api';
import { Spinner, centered, accentBtn } from '../components/shared';

export default function Home() {
  const router = useRouter();
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    getMe()
      .then((user) => {
        if (user) router.replace('/profile');
        else setChecking(false);
      })
      .catch(() => setChecking(false));
  }, [router]);

  if (checking) return <Spinner />;
  return <Landing />;
}

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
