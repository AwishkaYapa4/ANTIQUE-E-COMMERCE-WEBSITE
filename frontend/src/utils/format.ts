import type { InventoryState, OrderStatus, PaymentMethod, PaymentStatus, ProductCondition } from '@/types/api';

const lkr = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR', maximumFractionDigits: 0 });
const lkrExact = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR', minimumFractionDigits: 2 });

/** "Rs 185,000" style display; exact cents shown only when present. */
export function formatPrice(value: number): string {
  return Number.isInteger(value) ? lkr.format(value) : lkrExact.format(value);
}

const dateFormat = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Asia/Colombo' });
const dateTimeFormat = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Colombo',
});

export const formatDate = (iso: string) => dateFormat.format(new Date(iso));
export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso));

export const conditionLabels: Record<ProductCondition, string> = {
  Excellent: 'Excellent',
  VeryGood: 'Very Good',
  Good: 'Good',
  Fair: 'Fair',
  Restored: 'Restored',
  ForRestoration: 'For Restoration',
};

export const conditionDescriptions: Record<ProductCondition, string> = {
  Excellent: 'Minimal signs of age; displays beautifully.',
  VeryGood: 'Light, honest wear consistent with age.',
  Good: 'Visible wear and patina; sound and usable.',
  Fair: 'Noticeable wear or flaws, described below.',
  Restored: 'Has been restored; details below.',
  ForRestoration: 'Needs work; sold for restoration.',
};

export const inventoryLabels: Record<InventoryState, string> = {
  Draft: 'Draft',
  Available: 'Available',
  Reserved: 'Reserved',
  Sold: 'Sold',
  Archived: 'Archived',
};

export const orderStatusLabels: Record<OrderStatus, string> = {
  Pending: 'Pending',
  Confirmed: 'Confirmed',
  Processing: 'Processing',
  Shipped: 'Shipped',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled',
};

export const paymentStatusLabels: Record<PaymentStatus, string> = {
  Unpaid: 'Unpaid',
  PendingVerification: 'Pending verification',
  Paid: 'Paid',
  Failed: 'Failed',
  Refunded: 'Refunded',
};

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  CashOnDelivery: 'Cash on Delivery',
  BankTransfer: 'Bank Transfer',
};

export const SRI_LANKA_DISTRICTS = [
  'Ampara', 'Anuradhapura', 'Badulla', 'Batticaloa', 'Colombo', 'Galle', 'Gampaha', 'Hambantota', 'Jaffna', 'Kalutara',
  'Kandy', 'Kegalle', 'Kilinochchi', 'Kurunegala', 'Mannar', 'Matale', 'Matara', 'Monaragala', 'Mullaitivu',
  'Nuwara Eliya', 'Polonnaruwa', 'Puttalam', 'Ratnapura', 'Trincomalee', 'Vavuniya',
] as const;

export function cn(...classes: (string | false | null | undefined)[]): string {
  return classes.filter(Boolean).join(' ');
}

export function newIdempotencyKey(): string {
  return crypto.randomUUID().replaceAll('-', '');
}

export const isExternal = (href: string) => /^https?:\/\//i.test(href);
