import { initializeApp, type FirebaseApp } from 'firebase/app';
import { connectAuthEmulator, getAuth, type Auth } from 'firebase/auth';

const config = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY as string | undefined,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN as string | undefined,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID as string | undefined,
  appId: import.meta.env.VITE_FIREBASE_APP_ID as string | undefined,
};
const emulatorHost = import.meta.env.VITE_FIREBASE_AUTH_EMULATOR_HOST as string | undefined;

/** True when enough configuration exists to talk to Firebase (real project or local emulator). */
export const isFirebaseConfigured = Boolean(config.apiKey && config.projectId);

let app: FirebaseApp | null = null;
let auth: Auth | null = null;

export function getFirebaseAuth(): Auth | null {
  if (!isFirebaseConfigured) return null;
  if (!auth) {
    app = initializeApp(config);
    auth = getAuth(app);
    if (emulatorHost) connectAuthEmulator(auth, `http://${emulatorHost}`, { disableWarnings: true });
  }
  return auth;
}

/** Fresh ID token for API calls (Firebase refreshes it automatically before expiry). */
export async function getIdToken(): Promise<string | null> {
  const user = getFirebaseAuth()?.currentUser;
  return user ? user.getIdToken() : null;
}
