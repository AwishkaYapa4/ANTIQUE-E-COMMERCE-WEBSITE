import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { expect, type Page } from '@playwright/test';
import { e2eEnv } from '../playwright.config';

const EMULATOR = 'http://127.0.0.1:9099';
const PROJECT = 'demo-antique-store';
const API = 'http://localhost:5080';
const apiDir = fileURLToPath(new URL('../../backend/AntiqueStore.Api', import.meta.url));

export const unique = (prefix: string) => `${prefix}-${Date.now().toString(36)}${Math.floor(Math.random() * 1e4)}`;
export const password = 'Antique#2026';

/** Creates a user in the Auth Emulator and returns an ID token. */
export async function signUp(email: string): Promise<{ idToken: string; localId: string }> {
  const r = await fetch(`${EMULATOR}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=demo-key`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, returnSecureToken: true }),
  });
  if (!r.ok) throw new Error(`signUp failed: ${await r.text()}`);
  return (await r.json()) as { idToken: string; localId: string };
}

export async function signIn(email: string): Promise<string> {
  const r = await fetch(`${EMULATOR}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=demo-key`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, returnSecureToken: true }),
  });
  return ((await r.json()) as { idToken: string }).idToken;
}

/** Marks an emulator account's email as verified (stands in for clicking the email link). */
export async function verifyEmail(email: string) {
  const lookup = await fetch(`${EMULATOR}/identitytoolkit.googleapis.com/v1/projects/${PROJECT}/accounts:query`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer owner' }, body: JSON.stringify({ returnUserInfo: true }),
  });
  const users: { localId: string; email: string }[] = ((await lookup.json()) as { userInfo?: { localId: string; email: string }[] }).userInfo ?? [];
  const user = users.find((u) => u.email === email);
  if (!user) throw new Error(`No emulator user ${email}`);
  const r = await fetch(`${EMULATOR}/identitytoolkit.googleapis.com/v1/projects/${PROJECT}/accounts:update`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer owner' }, body: JSON.stringify({ localId: user.localId, emailVerified: true }),
  });
  if (!r.ok) throw new Error(`verify failed: ${await r.text()}`);
}

export async function api<T>(path: string, token: string | null, init: RequestInit = {}): Promise<T> {
  const r = await fetch(`${API}${path}`, {
    ...init, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...init.headers },
  });
  if (!r.ok) throw new Error(`${init.method ?? 'GET'} ${path} -> ${r.status}: ${await r.text()}`);
  return r.status === 204 ? (undefined as T) : ((await r.json()) as T);
}

/** Creates a verified account, signs it in once (creating the MySQL profile) and optionally grants Admin via the CLI. */
export async function createAccount(email: string, { admin = false } = {}) {
  await signUp(email);
  await verifyEmail(email);
  const token = await signIn(email);
  await api('/api/me', token);
  if (admin) {
    // The same trusted, server-side provisioning step a store owner runs. Never exposed over HTTP.
    execFileSync('dotnet', ['run', '--no-build', '--no-launch-profile', '--', 'admin', 'grant', email], { cwd: apiDir, env: { ...process.env, ...e2eEnv }, stdio: 'pipe' });
  }
  return token;
}

export async function loginUi(page: Page, email: string) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page).toHaveURL(/\/account/);
}

export interface CreatedProduct { id: number; slug: string; name: string }

/** Publishes a fresh one-of-a-kind product through the admin API so tests never compete for seed items. */
export async function createProduct(adminToken: string, name = unique('E2E Brass Inkwell')): Promise<CreatedProduct> {
  return api<CreatedProduct>('/api/admin/products', adminToken, {
    method: 'POST',
    body: JSON.stringify({
      name, sku: unique('E2E').toUpperCase(), price: 12500, status: 'Published', initialQuantity: 1, maxPerOrder: 1, condition: 'Good',
      shortDescription: 'An end-to-end test item.', conditionNotes: 'Light patina.', images: [{ mediaAssetId: null, url: '/placeholders/product-box.svg', altText: name }],
      collectionIds: [], attributes: [], isDeliveryAvailable: true, showWhenSold: true,
    }),
  });
}

/** Walks the checkout UI up to (and including) the Review step, which reserves the items. */
export async function checkoutToReview(page: Page) {
  await page.goto('/checkout');
  await page.getByLabel('Full name').fill('E2E Customer');
  await page.getByLabel('Phone number').fill('+94 77 123 4567');
  await page.getByRole('button', { name: 'Continue' }).click();
  await page.getByRole('textbox', { name: 'Address', exact: true }).fill('12 Galle Road');
  await page.getByLabel('City / town').fill('Colombo 03');
  await page.getByLabel('District').selectOption('Colombo');
  await page.getByRole('button', { name: 'Continue' }).click();
  await page.getByLabel('Cash on Delivery').check();
  await page.getByRole('button', { name: 'Review order' }).click();
  const reserved = page.getByText('Your pieces are reserved');
  const toast = page.locator('[data-sonner-toast]').first();
  await expect(reserved.or(toast)).toBeVisible();
  if (!(await reserved.isVisible())) throw new Error(`Checkout did not reach review: ${await toast.textContent()}`);
}

