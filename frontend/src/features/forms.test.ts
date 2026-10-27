import { describe, expect, it } from 'vitest';
import { checkoutSchema } from '@/features/customer/checkout/checkoutSchema';
import { enquirySchema } from '@/features/customer/product/EnquiryForm';
import { productSchema } from '@/features/admin/pages/productSchema';

const validCheckout = {
  fullName: 'Nimal Perera', email: 'nimal@example.lk', phone: '+94 77 123 4567', fulfillmentMethod: 'Delivery' as const,
  line1: '12 Galle Road', line2: '', city: 'Colombo 03', district: 'Colombo', postalCode: '00300', saveAddress: true, notes: '',
  paymentMethod: 'CashOnDelivery' as const, discountCode: '',
};

const issues = (r: { success: boolean; error?: { issues: { path: PropertyKey[] }[] } }) => r.error?.issues.map((i) => i.path.join('.')) ?? [];

describe('Checkout validation', () => {
  it('accepts a complete delivery order', () => {
    expect(checkoutSchema.safeParse(validCheckout).success).toBe(true);
  });

  it('requires an address and a valid district for delivery', () => {
    const r = checkoutSchema.safeParse({ ...validCheckout, line1: '', city: '', district: 'Atlantis' });
    expect(issues(r)).toEqual(expect.arrayContaining(['line1', 'city', 'district']));
  });

  it('does not require an address for local pickup', () => {
    expect(checkoutSchema.safeParse({ ...validCheckout, fulfillmentMethod: 'LocalPickup', line1: '', city: '', district: '' }).success).toBe(true);
  });

  it('rejects invalid email and phone', () => {
    const r = checkoutSchema.safeParse({ ...validCheckout, email: 'not-an-email', phone: 'call me' });
    expect(issues(r)).toEqual(expect.arrayContaining(['email', 'phone']));
  });
});

describe('Enquiry validation', () => {
  it('requires name, email and a meaningful message', () => {
    const r = enquirySchema.safeParse({ name: '', email: 'x', message: 'hi' });
    expect(issues(r)).toEqual(expect.arrayContaining(['name', 'email', 'message']));
  });
});

const validProduct = {
  name: 'Colonial Teak Planter’s Chair', slug: '', sku: 'FUR-0001', shortDescription: '', description: '', price: 185000, compareAtPrice: NaN,
  categoryId: '1', collectionIds: [], condition: 'Good' as const, conditionNotes: '', signsOfWear: '', scratches: '', damage: '', restorationHistory: '',
  missingParts: '', functionalStatus: '', material: 'Teak', color: '', dimensions: '', weightKg: NaN, era: 'Colonial', approximateAge: '', provenance: '',
  hasProvenanceEvidence: false, provenanceEvidence: '', initialQuantity: 1, maxPerOrder: 1, status: 'Draft' as const, isFeatured: false,
  isDeliveryAvailable: true, showWhenSold: true, seoTitle: '', seoDescription: '', attributes: [],
};

describe('Admin product form validation', () => {
  it('accepts a valid new antique and converts empty numbers to null', () => {
    const r = productSchema.safeParse(validProduct);
    expect(r.success).toBe(true);
    expect(r.data?.compareAtPrice).toBeNull();
    expect(r.data?.weightKg).toBeNull();
  });

  it('requires a name, a valid SKU and a positive price', () => {
    const r = productSchema.safeParse({ ...validProduct, name: '', sku: 'bad sku!', price: 0 });
    expect(issues(r)).toEqual(expect.arrayContaining(['name', 'sku', 'price']));
  });

  it('requires compare-at price to be above the selling price', () => {
    expect(issues(productSchema.safeParse({ ...validProduct, compareAtPrice: 100 }))).toContain('compareAtPrice');
  });

  it('requires evidence notes before provenance can be marked documented', () => {
    expect(issues(productSchema.safeParse({ ...validProduct, hasProvenanceEvidence: true }))).toContain('provenanceEvidence');
  });

  it('rejects an invalid slug format', () => {
    expect(issues(productSchema.safeParse({ ...validProduct, slug: 'Not A Slug' }))).toContain('slug');
  });
});
