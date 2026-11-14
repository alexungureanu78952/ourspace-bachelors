import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'OurSpace',
  description: 'Your space, your story.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
