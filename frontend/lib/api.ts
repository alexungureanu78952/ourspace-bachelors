export type Profile = {
  id: string;
  ownerId: string;
  displayName: string;
  bio: string;
  createdAt: string;
};

export type CurrentUser = {
  userId: string;
  name: string | null;
};

export async function getMe(): Promise<CurrentUser | null> {
  const res = await fetch('/bff/me', { cache: 'no-store' });
  if (res.status === 401) return null;
  if (!res.ok) throw new Error(`/bff/me failed: ${res.status}`);
  return res.json() as Promise<CurrentUser>;
}

async function getCsrf(): Promise<string> {
  const res = await fetch('/bff/csrf', { cache: 'no-store' });
  if (!res.ok) throw new Error('Failed to get CSRF token');
  const data = (await res.json()) as { token: string };
  return data.token;
}

export async function getMyProfile(): Promise<Profile | null> {
  const res = await fetch('/bff/profiles/me', { cache: 'no-store' });
  if (res.status === 401 || res.status === 404) return null;
  if (!res.ok) throw new Error(`/bff/profiles/me failed: ${res.status}`);
  return res.json() as Promise<Profile>;
}

export async function createProfile(displayName: string, bio: string): Promise<Profile> {
  const csrf = await getCsrf();
  const res = await fetch('/bff/profiles', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf },
    body: JSON.stringify({ displayName, bio }),
    cache: 'no-store',
  });
  if (!res.ok) {
    const err = (await res.json().catch(() => ({}))) as { title?: string };
    throw new Error(err?.title ?? 'Failed to create profile.');
  }
  return res.json() as Promise<Profile>;
}

export async function updateProfile(displayName: string, bio: string): Promise<Profile> {
  const csrf = await getCsrf();
  const res = await fetch('/bff/profiles/me', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf },
    body: JSON.stringify({ displayName, bio }),
    cache: 'no-store',
  });
  if (!res.ok) {
    const err = (await res.json().catch(() => ({}))) as { title?: string };
    throw new Error(err?.title ?? 'Failed to update profile.');
  }
  return res.json() as Promise<Profile>;
}

export async function signOut(): Promise<void> {
  // Sign-out must be a real browser navigation so the BFF can redirect through
  // the OIDC end-session endpoint and clear the identity server session.
  // A fetch() call keeps the redirect chain inside the browser's HTTP client
  // and the OIDC session is never cleared, causing silent re-authentication.
  const csrf = await getCsrf();
  const form = document.createElement('form');
  form.method = 'POST';
  form.action = '/bff/logout';
  const token = document.createElement('input');
  token.type = 'hidden';
  token.name = '__RequestVerificationToken';
  token.value = csrf;
  form.appendChild(token);
  document.body.appendChild(form);
  form.submit();
}
