'use client';

import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';

export type Locale = 'en' | 'ro';

export type Translations = {
  landing: {
    eyebrow: string;
    headlineL1: string;
    headlineL2: string;
    description: string;
    signIn: string;
    newHere: string;
  };
  profile: {
    title: string;
    created: string;
    edit: string;
    displayName: string;
    bio: string;
    previewTitle: string;
  };
  create: {
    title: string;
    subtitle: string;
    displayName: string;
    bio: string;
    bioOptional: string;
    displayNamePlaceholder: string;
    bioPlaceholder: string;
    submit: string;
    submitting: string;
    displayNameRequired: string;
    fallbackError: string;
  };
  edit: {
    title: string;
    subtitle: string;
    displayName: string;
    bio: string;
    bioOptional: string;
    submit: string;
    submitting: string;
    cancel: string;
    displayNameRequired: string;
    fallbackError: string;
  };
  shell: {
    signOut: string;
  };
};

const translations: Record<Locale, Translations> = {
  en: {
    landing: {
      eyebrow: '✦ OurSpace',
      headlineL1: 'Your space.',
      headlineL2: 'Your story.',
      description: 'A place to share who you are. Create your profile once, own it forever.',
      signIn: 'Sign in',
      newHere: 'New here? Create an account from the sign-in page.',
    },
    profile: {
      title: 'Your profile',
      created: 'Created',
      edit: 'Edit',
      displayName: 'Display name',
      bio: 'Bio',
      previewTitle: 'Profile preview',
    },
    create: {
      title: 'Create your profile',
      subtitle: 'You get one profile per account — make it yours.',
      displayName: 'Display name',
      bio: 'Bio',
      bioOptional: '— optional',
      displayNamePlaceholder: 'How should we call you?',
      bioPlaceholder: 'Tell people a bit about yourself…',
      submit: 'Create profile',
      submitting: 'Creating…',
      displayNameRequired: 'Display name is required.',
      fallbackError: 'Something went wrong.',
    },
    edit: {
      title: 'Edit your profile',
      subtitle: 'Changes take effect immediately.',
      displayName: 'Display name',
      bio: 'Bio',
      bioOptional: '— optional',
      submit: 'Save changes',
      submitting: 'Saving…',
      cancel: 'Cancel',
      displayNameRequired: 'Display name is required.',
      fallbackError: 'Something went wrong.',
    },
    shell: {
      signOut: 'Sign out',
    },
  },
  ro: {
    landing: {
      eyebrow: '✦ OurSpace',
      headlineL1: 'Spațiul tău.',
      headlineL2: 'Povestea ta.',
      description:
        'Un loc unde să te prezinți. Creează-ți profilul o dată, păstrează-l pentru totdeauna.',
      signIn: 'Autentificare',
      newHere: 'Ești nou? Creează un cont din pagina de autentificare.',
    },
    profile: {
      title: 'Profilul tău',
      created: 'Creat',
      edit: 'Editează',
      displayName: 'Nume afișat',
      bio: 'Bio',
      previewTitle: 'Previzualizare profil',
    },
    create: {
      title: 'Creează-ți profilul',
      subtitle: 'Un singur profil per cont — fă-l al tău.',
      displayName: 'Nume afișat',
      bio: 'Bio',
      bioOptional: '— opțional',
      displayNamePlaceholder: 'Cum să te strigăm?',
      bioPlaceholder: 'Spune-le oamenilor câte ceva despre tine…',
      submit: 'Creează profilul',
      submitting: 'Se creează…',
      displayNameRequired: 'Numele afișat este obligatoriu.',
      fallbackError: 'Ceva nu a mers bine.',
    },
    edit: {
      title: 'Editează-ți profilul',
      subtitle: 'Modificările intră în vigoare imediat.',
      displayName: 'Nume afișat',
      bio: 'Bio',
      bioOptional: '— opțional',
      submit: 'Salvează modificările',
      submitting: 'Se salvează…',
      cancel: 'Anulează',
      displayNameRequired: 'Numele afișat este obligatoriu.',
      fallbackError: 'Ceva nu a mers bine.',
    },
    shell: {
      signOut: 'Deconectare',
    },
  },
};

interface I18nContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: Translations;
}

const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  // Start with 'en' to match the static-export server render, then hydrate from localStorage.
  const [locale, setLocaleState] = useState<Locale>('en');

  useEffect(() => {
    try {
      const saved = localStorage.getItem('locale') as Locale | null;
      if (saved === 'ro') setLocaleState('ro');
    } catch {
      // localStorage unavailable (private browsing, etc.)
    }
  }, []);

  function setLocale(next: Locale) {
    setLocaleState(next);
    try {
      localStorage.setItem('locale', next);
    } catch {
      // ignore
    }
    // Plain cookie so the BFF can read it on /bff/login and forward locale
    // to the Identity service via the ui_locales OIDC parameter.
    try {
      document.cookie = `locale=${next}; Path=/; SameSite=Lax; Secure`;
    } catch {
      // ignore
    }
  }

  return (
    <I18nContext.Provider value={{ locale, setLocale, t: translations[locale] }}>
      {children}
    </I18nContext.Provider>
  );
}

export function useTranslation() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useTranslation must be used inside I18nProvider');
  return ctx;
}

// Date locale map — extend when adding more locales.
export const dateLocaleMap: Record<Locale, string> = {
  en: 'en-US',
  ro: 'ro-RO',
};
