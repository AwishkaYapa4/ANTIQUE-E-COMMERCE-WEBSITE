import { z } from 'zod';

const optionalText = (max: number) => z.string().trim().max(max, `Maximum ${max} characters.`);
const money = z.union([z.number(), z.nan()]).transform((v) => (Number.isNaN(v) ? null : v));

/** Mirrors the API's ProductUpsertValidator so most mistakes are caught before saving. */
export const productSchema = z
  .object({
    name: z.string().trim().min(1, 'Product name is required.').max(200),
    slug: z.string().trim().max(200).regex(/^$|^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'Use lowercase letters, numbers and dashes.'),
    sku: z.string().trim().min(1, 'SKU is required.').max(64).regex(/^[A-Za-z0-9._-]+$/, 'Letters, numbers, dots, dashes and underscores only.'),
    shortDescription: optionalText(500),
    description: optionalText(20000),
    price: z.number({ message: 'Enter a price.' }).positive('Price must be greater than 0.'),
    compareAtPrice: money,
    categoryId: z.string(),
    collectionIds: z.array(z.number()),
    condition: z.enum(['Excellent', 'VeryGood', 'Good', 'Fair', 'Restored', 'ForRestoration']),
    conditionNotes: optionalText(2000),
    signsOfWear: optionalText(2000),
    scratches: optionalText(2000),
    damage: optionalText(2000),
    restorationHistory: optionalText(2000),
    missingParts: optionalText(2000),
    functionalStatus: optionalText(2000),
    material: optionalText(100),
    color: optionalText(100),
    dimensions: optionalText(200),
    weightKg: money,
    era: optionalText(100),
    approximateAge: optionalText(100),
    provenance: optionalText(2000),
    hasProvenanceEvidence: z.boolean(),
    provenanceEvidence: optionalText(2000),
    initialQuantity: z.number({ message: 'Enter a quantity.' }).int().min(0).max(10000),
    maxPerOrder: z.number({ message: 'Enter a number.' }).int().min(1).max(20),
    status: z.enum(['Draft', 'Published', 'Archived']),
    isFeatured: z.boolean(),
    isDeliveryAvailable: z.boolean(),
    showWhenSold: z.boolean(),
    seoTitle: optionalText(200),
    seoDescription: optionalText(500),
    attributes: z.array(z.object({ name: z.string().trim().max(100), value: z.string().trim().max(500) })).max(30),
  })
  .superRefine((v, ctx) => {
    if (v.compareAtPrice != null && v.compareAtPrice > 0 && v.compareAtPrice <= v.price)
      ctx.addIssue({ code: 'custom', path: ['compareAtPrice'], message: 'Compare-at price must be higher than the price (it shows as the original price).' });
    if (v.hasProvenanceEvidence && !v.provenanceEvidence)
      ctx.addIssue({ code: 'custom', path: ['provenanceEvidence'], message: 'Describe the documentation you hold.' });
  });

export type ProductFormValues = z.input<typeof productSchema>;
export type ProductFormOutput = z.output<typeof productSchema>;
