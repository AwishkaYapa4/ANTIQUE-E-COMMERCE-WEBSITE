import { expect, test } from '@playwright/test';
import { api, checkoutToReview, createAccount, createProduct, loginUi, password, unique, verifyEmail, type CreatedProduct } from './helpers';

test.describe.configure({ mode: 'serial' });

let adminEmail: string;
let adminToken: string;
let customerEmail: string;
let orderNumber: string;
let product: CreatedProduct;

test.beforeAll(async () => {
  adminEmail = `${unique('owner')}@example.test`;
  adminToken = await createAccount(adminEmail, { admin: true });
  product = await createProduct(adminToken);
});

test('1. a new customer registers, verifies their email and is signed in', async ({ page }) => {
  customerEmail = `${unique('shopper')}@example.test`;
  await page.goto('/register');
  await page.getByLabel('Full name').fill('Nimal Perera');
  await page.getByLabel('Email', { exact: true }).fill(customerEmail);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page.getByRole('heading', { name: 'Verify your email' })).toBeVisible();
  await verifyEmail(customerEmail); // stands in for clicking the link in the email
  await page.getByRole('button', { name: "I've verified my email" }).click();
  await expect(page).toHaveURL(/\/account/);
  await expect(page.getByRole('heading', { name: /hello, nimal/i })).toBeVisible();

  await page.getByRole('navigation', { name: 'Account' }).getByRole('button', { name: 'Sign out' }).click();
  await loginUi(page, customerEmail);
});

test('2-4. the customer browses, adds a unique piece to the cart and places an order', async ({ page }) => {
  await loginUi(page, customerEmail);
  await page.goto('/shop');
  await expect(page.getByRole('heading', { name: 'Shop all pieces' })).toBeVisible();
  await expect(page.locator('article').first()).toBeVisible();

  await page.goto(`/products/${product.slug}`);
  await expect(page.getByRole('heading', { level: 1, name: product.name })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Condition report' })).toBeVisible();
  await page.getByRole('button', { name: 'Add to cart', exact: true }).first().click();
  await expect(page.getByRole('dialog', { name: 'Your cart' })).toContainText(product.name);
  await page.getByRole('dialog', { name: 'Your cart' }).getByRole('button', { name: 'Checkout' }).click();

  await checkoutToReview(page);
  await expect(page.getByText('Your pieces are reserved')).toBeVisible();
  await page.getByRole('button', { name: /place order/i }).click();

  await expect(page.getByRole('heading', { name: 'Thank you for your order' })).toBeVisible();
  orderNumber = (await page.locator('strong.font-mono').first().textContent())!.trim();
  expect(orderNumber).toMatch(/^[A-Z]+-\d{6}-[A-Z0-9]{6}$/);

  // The piece is now sold for everyone.
  const publicView = await api<{ inventoryState: string }>(`/api/products/${product.slug}`, null);
  expect(publicView.inventoryState).toBe('Sold');
});

test('5-6. the admin finds the order and confirms it', async ({ page }) => {
  await loginUi(page, adminEmail);
  await page.goto('/admin/orders');
  await page.getByPlaceholder('Order no., name, email, phone').fill(orderNumber);
  await page.getByRole('link', { name: orderNumber }).click();
  await expect(page.getByRole('heading', { name: `Order ${orderNumber}` })).toBeVisible();
  await expect(page.getByText(product.name)).toBeVisible();

  await page.getByRole('button', { name: 'Mark confirmed' }).click();
  await page.getByRole('dialog').getByRole('textbox').fill('Confirmed by phone');
  await page.getByRole('dialog').getByRole('button', { name: 'Confirm' }).click();
  await expect(page.getByText('Order updated')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Mark processing' })).toBeVisible();

  const customerToken = await (await import('./helpers')).signIn(customerEmail);
  const mine = await api<{ items: { orderNumber: string; status: string }[] }>('/api/orders/my', customerToken);
  expect(mine.items.find((o) => o.orderNumber === orderNumber)?.status).toBe('Confirmed');
});

test('7. the admin adds a new antique with an uploaded photo and publishes it', async ({ page }) => {
  const name = unique('E2E Teak Writing Desk');
  await loginUi(page, adminEmail);
  await page.goto('/admin/products/new');
  await page.getByLabel(/product name/i).fill(name);
  await page.getByLabel(/^sku/i).fill(unique('DESK').toUpperCase());
  await page.getByLabel(/^price/i).fill('98000');
  await page.getByLabel('Overall condition').selectOption('VeryGood');
  await page.getByLabel(/signs of wear/i).fill('Gentle wear to the writing surface.');
  await page.getByLabel(/era \/ period/i).fill('Colonial');
  await page.getByLabel('Status', { exact: true }).selectOption('Published');

  await page.getByRole('button', { name: 'Add photos' }).click();
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAIAAAACUFjqAAAAEklEQVR4nGP4z8CAB+GTG8HSALfKY52fTcuYAAAAAElFTkSuQmCC', 'base64');
  await page.locator('input[type=file]').setInputFiles({ name: 'desk.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByText(/1 image uploaded/)).toBeVisible();
  await page.getByRole('button', { name: /use 1 selected/i }).click();
  await page.getByRole('button', { name: 'Create product' }).click();

  await expect(page.getByText('Product created')).toBeVisible();
  await expect(page).toHaveURL(/\/admin\/products\/\d+/);

  await page.goto(`/shop?q=${encodeURIComponent(name)}`);
  await expect(page.getByRole('heading', { name, level: 3 })).toBeVisible();
});

test('8-9. the admin changes the homepage hero, previews, publishes and visitors see it', async ({ page, browser }) => {
  const headline = `Autumn treasures ${Date.now().toString(36)}`;
  await loginUi(page, adminEmail);
  await page.goto('/admin/homepage');
  await page.getByRole('button', { name: 'Edit section' }).first().click();
  await page.getByLabel('Headline').fill(headline);
  await page.getByRole('button', { name: 'Done' }).click();
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(page.getByText('Homepage draft saved')).toBeVisible();

  // Not live yet for anonymous visitors...
  const visitor = await browser.newPage();
  await visitor.goto('/');
  await expect(visitor.getByRole('heading', { level: 1 })).not.toHaveText(headline);
  // ...but visible to the admin in preview mode.
  await page.goto('/?preview=draft');
  await expect(page.getByText('You are previewing unpublished changes')).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: headline })).toBeVisible();

  await page.goto('/admin/homepage');
  await page.getByRole('button', { name: 'Publish' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText(/your changes are live/i)).toBeVisible();

  await expect(async () => {
    await visitor.goto('/');
    await expect(visitor.getByRole('heading', { level: 1, name: headline })).toBeVisible({ timeout: 3000 });
  }).toPass({ timeout: 75_000 }); // public responses are cached for up to 60s
  await visitor.close();
});

test('10. two customers cannot buy the same one-of-a-kind piece at the same time', async ({ browser }) => {
  const contested = await createProduct(adminToken, unique('E2E Rosewood Box'));
  const emails = [`${unique('alice')}@example.test`, `${unique('bob')}@example.test`];
  for (const e of emails) await createAccount(e);

  // Separate browser contexts = two independent shoppers.
  const ctxA = await browser.newContext();
  const ctxB = await browser.newContext();
  const alice = await ctxA.newPage();
  const bob = await ctxB.newPage();

  for (const [page, email] of [[alice, emails[0]], [bob, emails[1]]] as const) {
    await loginUi(page, email);
    await page.goto(`/products/${contested.slug}`);
    await page.getByRole('button', { name: 'Add to cart', exact: true }).first().click();
    await expect(page.getByRole('dialog', { name: 'Your cart' })).toContainText(contested.name);
  }

  await checkoutToReview(alice);
  await expect(alice.getByText('Your pieces are reserved')).toBeVisible();

  // Bob reaches the same step while Alice holds the only unit.
  await bob.goto('/checkout');
  await expect(bob).toHaveURL(/\/cart/); // checkout redirects: the item is reserved by someone else
  await expect(bob.getByText(/reserved by another customer/i)).toBeVisible();

  await alice.getByRole('button', { name: /place order/i }).click();
  await expect(alice.getByRole('heading', { name: 'Thank you for your order' })).toBeVisible();

  await bob.reload();
  await expect(bob.getByText(/has been sold/i)).toBeVisible();
  const bobToken = await (await import('./helpers')).signIn(emails[1]);
  await expect(api('/api/orders', bobToken, {
    method: 'POST',
    body: JSON.stringify({
      idempotencyKey: unique('bobkey').replace(/[^a-z0-9]/gi, '').padEnd(20, '0'), checkoutToken: null,
      contact: { fullName: 'Bob', email: emails[1], phone: '+94 77 765 4321' }, fulfillmentMethod: 'Delivery',
      address: { line1: '1 Lake Road', line2: null, city: 'Kandy', district: 'Colombo', postalCode: null },
      paymentMethod: 'CashOnDelivery', discountCode: null, notes: null, saveAddress: false, expectedTotal: null,
    }),
  })).rejects.toThrow(/422/);

  await ctxA.close();
  await ctxB.close();
});

