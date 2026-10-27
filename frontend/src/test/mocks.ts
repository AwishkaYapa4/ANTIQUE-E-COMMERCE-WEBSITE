import { vi } from 'vitest';
import { fallbackConfig } from '@/features/site/SiteConfigProvider';

/** Guest (signed-out) auth state used by most component tests. */
export const authState = {
  firebaseUser: null, me: undefined, initializing: false, isAuthenticated: false, isAdmin: false, isConfigured: true,
  signIn: vi.fn(), register: vi.fn(), signInWithGoogle: vi.fn(), resetPassword: vi.fn(), resendVerification: vi.fn(),
  refreshUser: vi.fn(), signOut: vi.fn(),
};

export const siteState = {
  ...fallbackConfig, isLoading: false, preview: false, exitPreview: vi.fn(),
  menu: (location: string) => fallbackConfig.navigation.find((m) => m.location === location) ?? { location, title: '', items: [] },
};
