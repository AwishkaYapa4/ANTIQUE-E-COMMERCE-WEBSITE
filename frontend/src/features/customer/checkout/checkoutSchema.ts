import { z } from 'zod';
import { SRI_LANKA_DISTRICTS } from '@/utils/format';

const phone = z.string().trim().regex(/^\+?[0-9 ()-]{7,20}$/, 'Enter a valid phone number (e.g. +94 77 123 4567).');

/** Client-side validation mirrors the API's PlaceOrderValidator; the server re-validates everything. */
export const checkoutSchema = z
  .object({
    fullName: z.string().trim().min(1, 'Enter your full name.').max(200),
    email: z.email('Enter a valid email address.'),
    phone,
    fulfillmentMethod: z.enum(['Delivery', 'LocalPickup']),
    line1: z.string().trim().max(200),
    line2: z.string().trim().max(200).optional(),
    city: z.string().trim().max(100),
    district: z.string(),
    postalCode: z.string().trim().max(20).regex(/^[0-9A-Za-z -]*$/, 'Enter a valid postal code.').optional(),
    saveAddress: z.boolean(),
    notes: z.string().trim().max(1000).optional(),
    paymentMethod: z.enum(['CashOnDelivery', 'BankTransfer']),
    discountCode: z.string().trim().max(64).optional(),
  })
  .superRefine((v, ctx) => {
    if (v.fulfillmentMethod !== 'Delivery') return;
    if (!v.line1) ctx.addIssue({ code: 'custom', path: ['line1'], message: 'Enter your street address.' });
    if (!v.city) ctx.addIssue({ code: 'custom', path: ['city'], message: 'Enter your city or town.' });
    if (!(SRI_LANKA_DISTRICTS as readonly string[]).includes(v.district)) ctx.addIssue({ code: 'custom', path: ['district'], message: 'Select your district.' });
  });

export type CheckoutValues = z.infer<typeof checkoutSchema>;

export const stepFields: Record<number, (keyof CheckoutValues)[]> = {
  0: ['fullName', 'email', 'phone'],
  1: ['fulfillmentMethod', 'line1', 'line2', 'city', 'district', 'postalCode', 'notes'],
  2: ['paymentMethod', 'discountCode'],
};
