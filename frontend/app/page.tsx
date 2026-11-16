'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getMe } from '../lib/api';
import { Spinner, centered, accentBtn, LanguageSwitcher } from '../components/shared';
import { useTranslation } from '../lib/i18n';

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
  const { t } = useTranslation();
  const l = t.landing;

  return (
    <div style={{ ...centered, flexDirection: 'column', textAlign: 'center', padding: '0 20px' }}>
      <div style={{ position: 'absolute', top: 16, right: 20 }}>
        <LanguageSwitcher />
      </div>
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
        {l.eyebrow}
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
        {l.headlineL1}
        <br />
        {l.headlineL2}
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
        {l.description}
      </p>
      <a href="/bff/login?returnUrl=/" style={accentBtn}>
        {l.signIn}
      </a>
      <p style={{ marginTop: 16, fontSize: 13, color: 'var(--text-muted)' }}>{l.newHere}</p>
    </div>
  );
}
