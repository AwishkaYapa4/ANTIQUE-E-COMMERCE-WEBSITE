import type { SectionType } from './api';

// Matches the server-side schemas in SectionConfigs.cs (validated and normalised by the API).
export interface CtaLink { label: string; href: string }

export interface HeroConfig {
  imageUrl: string | null; title: string; subtitle: string | null; eyebrow: string | null;
  primaryCta: CtaLink | null; secondaryCta: CtaLink | null;
  alignment: 'left' | 'center' | 'right'; height: 'medium' | 'large' | 'full'; overlayOpacity: number;
}
export interface FeaturedCategoriesConfig { title: string; subtitle: string | null; categoryIds: number[] | null; limit: number }
export interface FeaturedProductsConfig { title: string; subtitle: string | null; productIds: number[] | null; limit: number; cta: CtaLink | null }
export interface NewArrivalsConfig { title: string; subtitle: string | null; limit: number; cta: CtaLink | null }
export interface CollectionProductsConfig { title: string; subtitle: string | null; collectionId: number | null; limit: number; cta: CtaLink | null }
export interface ImageTextConfig {
  imageUrl: string | null; eyebrow: string | null; heading: string; body: string | null; cta: CtaLink | null;
  imagePosition: 'left' | 'right'; background: 'surface' | 'background' | 'dark';
}
export interface PromoBannerConfig { imageUrl: string | null; eyebrow: string | null; heading: string; text: string | null; cta: CtaLink | null; theme: 'dark' | 'light' | 'accent' }
export interface TestimonialsConfig { title: string; subtitle: string | null; limit: number }
export interface NewsletterConfig { title: string; text: string | null; buttonLabel: string }
export interface RichTextConfig { title: string | null; html: string; width: 'narrow' | 'wide' }
export interface EraShowcaseConfig { title: string; subtitle: string | null; items: { label: string; imageUrl: string | null; href: string }[] }
export type TrustIcon = 'gem' | 'camera' | 'shield' | 'truck' | 'search' | 'heart' | 'award' | 'clock' | 'package' | 'sparkles';
export interface TrustFeaturesConfig { title: string | null; items: { icon: TrustIcon; title: string; text: string }[] }

export const sectionLabels: Record<SectionType, string> = {
  hero: 'Hero',
  featuredCategories: 'Featured categories',
  featuredProducts: 'Featured products',
  newArrivals: 'New arrivals',
  collectionProducts: 'Collection highlight (e.g. Rare Finds)',
  imageText: 'Image + text (e.g. Our Story)',
  promoBanner: 'Promotional banner',
  testimonials: 'Testimonials',
  newsletter: 'Newsletter',
  richText: 'Rich text',
  eraShowcase: 'Shop by era / style',
  trustFeatures: 'Trust features',
};

/** Starting configuration when an admin adds a new section. */
export const defaultSectionConfig: Record<SectionType, Record<string, unknown>> = {
  hero: { imageUrl: null, title: 'A new headline', subtitle: null, eyebrow: null, primaryCta: { label: 'Shop now', href: '/shop' }, secondaryCta: null, alignment: 'left', height: 'large', overlayOpacity: 35 },
  featuredCategories: { title: 'Shop by Category', subtitle: null, categoryIds: null, limit: 8 },
  featuredProducts: { title: 'Featured Pieces', subtitle: null, productIds: null, limit: 8, cta: null },
  newArrivals: { title: 'New Arrivals', subtitle: null, limit: 8, cta: null },
  collectionProducts: { title: 'Rare Finds', subtitle: null, collectionId: null, limit: 4, cta: null },
  imageText: { imageUrl: null, eyebrow: null, heading: 'Our Story', body: 'Tell your story here.', cta: null, imagePosition: 'left', background: 'surface' },
  promoBanner: { imageUrl: null, eyebrow: null, heading: 'A special announcement', text: null, cta: { label: 'Learn more', href: '/shop' }, theme: 'dark' },
  testimonials: { title: 'Kind Words', subtitle: null, limit: 6 },
  newsletter: { title: 'Be the first to know', text: null, buttonLabel: 'Subscribe' },
  richText: { title: null, html: '<p>Write something here.</p>', width: 'narrow' },
  eraShowcase: { title: 'Shop by Era', subtitle: null, items: [{ label: 'Victorian', imageUrl: null, href: '/shop?era=Victorian' }] },
  trustFeatures: { title: null, items: [{ icon: 'gem', title: 'Carefully Selected Pieces', text: 'Every item is chosen individually.' }] },
};
