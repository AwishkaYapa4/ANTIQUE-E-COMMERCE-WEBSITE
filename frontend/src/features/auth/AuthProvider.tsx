import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  createUserWithEmailAndPassword, GoogleAuthProvider, onIdTokenChanged, sendEmailVerification, sendPasswordResetEmail,
  signInWithEmailAndPassword, signInWithPopup, signOut as firebaseSignOut, updateProfile, type User,
} from 'firebase/auth';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { getFirebaseAuth, isFirebaseConfigured } from '@/services/firebase';
import { cartApi, meApi, wishlistApi } from '@/services/api';
import { useGuestStore } from '@/store/guestStore';
import type { Me } from '@/types/api';

interface AuthContextValue {
  firebaseUser: User | null;
  me: Me | undefined;
  /** True until Firebase has restored any persisted session. */
  initializing: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  isConfigured: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  signInWithGoogle: () => Promise<void>;
  resetPassword: (email: string) => Promise<void>;
  resendVerification: () => Promise<void>;
  /** Re-reads the Firebase user and forces a fresh token (e.g. after the email link was clicked). */
  refreshUser: () => Promise<boolean>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function requireAuth() {
  const auth = getFirebaseAuth();
  if (!auth) throw new Error('Sign-in is not configured for this site yet.');
  return auth;
}

/** Human-friendly messages for common Firebase auth errors. */
export function authErrorMessage(error: unknown): string {
  const code = (error as { code?: string })?.code ?? '';
  const map: Record<string, string> = {
    'auth/invalid-credential': 'Email or password is incorrect.',
    'auth/wrong-password': 'Email or password is incorrect.',
    'auth/user-not-found': 'Email or password is incorrect.',
    'auth/email-already-in-use': 'An account with this email already exists. Try signing in.',
    'auth/weak-password': 'Please choose a stronger password (at least 8 characters).',
    'auth/too-many-requests': 'Too many attempts. Please wait a moment and try again.',
    'auth/popup-closed-by-user': 'The Google sign-in window was closed.',
    'auth/network-request-failed': 'Network error. Check your connection and try again.',
    'auth/invalid-email': 'Enter a valid email address.',
  };
  return map[code] ?? (error instanceof Error ? error.message : 'Sign-in failed. Please try again.');
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [firebaseUser, setFirebaseUser] = useState<User | null>(null);
  const [initializing, setInitializing] = useState(isFirebaseConfigured);
  const queryClient = useQueryClient();
  const mergedFor = useRef<string | null>(null);

  useEffect(() => {
    const auth = getFirebaseAuth();
    if (!auth) return;
    return onIdTokenChanged(auth, (user) => {
      setFirebaseUser(user);
      setInitializing(false);
    });
  }, []);

  const meQuery = useQuery({
    queryKey: ['me', firebaseUser?.uid],
    queryFn: meApi.get,
    enabled: Boolean(firebaseUser),
    staleTime: 5 * 60_000,
  });

  // After sign-in, move the browser cart/wishlist into the account. The server re-validates everything.
  useEffect(() => {
    if (!firebaseUser || !meQuery.data || mergedFor.current === firebaseUser.uid) return;
    mergedFor.current = firebaseUser.uid;
    const { cart, wishlist, clearCart, clearWishlist } = useGuestStore.getState();
    (async () => {
      try {
        if (cart.length > 0) {
          const result = await cartApi.merge(cart.map((l) => ({ productId: l.productId, quantity: l.quantity })));
          clearCart();
          queryClient.setQueryData(['cart'], result.cart);
          result.skipped.forEach((m) => toast.warning(m));
        }
        if (wishlist.length > 0) {
          queryClient.setQueryData(['wishlist'], await wishlistApi.merge(wishlist));
          clearWishlist();
        }
      } catch (e) {
        console.error('Could not merge guest cart', e);
      }
    })();
  }, [firebaseUser, meQuery.data, queryClient]);

  const signOut = useCallback(async () => {
    const auth = getFirebaseAuth();
    if (auth) await firebaseSignOut(auth);
    mergedFor.current = null;
    queryClient.removeQueries({ queryKey: ['me'] });
    queryClient.removeQueries({ queryKey: ['cart'] });
    queryClient.removeQueries({ queryKey: ['wishlist'] });
    queryClient.removeQueries({ queryKey: ['orders'] });
    queryClient.removeQueries({ queryKey: ['admin'] });
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(() => ({
    firebaseUser,
    me: meQuery.data,
    initializing: initializing || (Boolean(firebaseUser) && meQuery.isPending),
    isAuthenticated: Boolean(firebaseUser && meQuery.data),
    isAdmin: meQuery.data?.role === 'Admin',
    isConfigured: isFirebaseConfigured,
    signIn: async (email, password) => {
      await signInWithEmailAndPassword(requireAuth(), email, password);
    },
    register: async (name, email, password) => {
      const credential = await createUserWithEmailAndPassword(requireAuth(), email, password);
      await updateProfile(credential.user, { displayName: name });
      await sendEmailVerification(credential.user, { url: `${window.location.origin}/verify-email` });
      await credential.user.getIdToken(true);
    },
    signInWithGoogle: async () => {
      await signInWithPopup(requireAuth(), new GoogleAuthProvider());
    },
    resetPassword: async (email) => {
      await sendPasswordResetEmail(requireAuth(), email, { url: `${window.location.origin}/login` });
    },
    resendVerification: async () => {
      const user = requireAuth().currentUser;
      if (user) await sendEmailVerification(user, { url: `${window.location.origin}/verify-email` });
    },
    refreshUser: async () => {
      const user = requireAuth().currentUser;
      if (!user) return false;
      await user.reload();
      await user.getIdToken(true);
      await queryClient.invalidateQueries({ queryKey: ['me'] });
      return requireAuth().currentUser?.emailVerified ?? false;
    },
    signOut,
  }), [firebaseUser, meQuery.data, meQuery.isPending, initializing, queryClient, signOut]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
