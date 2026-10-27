import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { motion } from 'motion/react';
import { ArrowRight, Award, Camera, Clock, Gem, Heart, Package, Quote, Search, ShieldCheck, Sparkles, Truck } from 'lucide-react';
import DOMPurify from 'dompurify';
import type { RenderedSection } from '@/types/api';
import type * as S from '@/types/sections';
import { SmartLink } from '@/components/SmartLink';
import { buttonClass } from '@/components/ui/Button';
import { SmartImage, Stars } from '@/components/ui/Misc';
import { NewsletterForm } from '@/layouts/customer/Footer';
import { ProductGrid } from '@/features/customer/product/ProductCard';
import { cn } from '@/utils/format';

const reveal = {
  initial: { opacity: 0, y: 18 },
  whileInView: { opacity: 1, y: 0 },
  viewport: { once: true, margin: '-60px' },
  transition: { duration: 0.6, ease: 'easeOut' as const },
};

function SectionHeading({ title, subtitle, cta, align = 'left' }: { title: string; subtitle?: string | null; cta?: S.CtaLink | null; align?: 'left' | 'center' }) {
  return (
    <div className={cn('mb-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between', align === 'center' && 'items-center text-center sm:flex-col sm:items-center')}>
      <div className={cn(align === 'center' && 'mx-auto max-w-2xl')}>
        <h2>{title}</h2>
        {subtitle && <p className="mt-3 max-w-xl text-muted">{subtitle}</p>}
      </div>
      {cta && (
        <SmartLink href={cta.href} className="group inline-flex shrink-0 items-center gap-2 text-sm font-medium tracking-wide text-primary">
          {cta.label} <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" aria-hidden />
        </SmartLink>
      )}
    </div>
  );
}

const Section = ({ children, className }: { children: ReactNode; className?: string }) => (
  <motion.section {...reveal} className={cn('py-16 sm:py-24', className)}>{children}</motion.section>
);

export function HeroSection({ config }: { config: S.HeroConfig }) {
  const height = { medium: 'min-h-[60vh]', large: 'min-h-[78vh]', full: 'min-h-[calc(100vh-5rem)]' }[config.height] ?? 'min-h-[78vh]';
  const align = { left: 'items-start text-left', center: 'items-center text-center mx-auto', right: 'items-end text-right ml-auto' }[config.alignment];
  return (
    <section className={cn('relative flex items-center overflow-hidden bg-primary', height)}>
      {config.imageUrl && <SmartImage src={config.imageUrl} alt="" priority className="absolute inset-0 h-full w-full" />}
      <div className="absolute inset-0 bg-ink" style={{ opacity: (config.overlayOpacity ?? 35) / 100 }} aria-hidden />
      <div className="container-page relative py-24">
        <motion.div initial={{ opacity: 0, y: 24 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.8 }} className={cn('flex max-w-2xl flex-col', align)}>
          {config.eyebrow && <p className="mb-5 text-xs font-medium tracking-[0.3em] text-paper/85 uppercase">{config.eyebrow}</p>}
          <h1 className="text-paper" style={{ fontWeight: 400 }}>{config.title}</h1>
          {config.subtitle && <p className="mt-6 max-w-xl text-lg leading-relaxed text-paper/90">{config.subtitle}</p>}
          {(config.primaryCta || config.secondaryCta) && (
            <div className="mt-10 flex flex-wrap gap-3">
              {config.primaryCta && <SmartLink href={config.primaryCta.href} className={buttonClass('light', 'lg')}>{config.primaryCta.label}</SmartLink>}
              {config.secondaryCta && (
                <SmartLink href={config.secondaryCta.href} className={buttonClass('outline', 'lg', 'border-paper text-paper hover:bg-paper hover:text-ink')}>
                  {config.secondaryCta.label}
                </SmartLink>
              )}
            </div>
          )}
        </motion.div>
      </div>
    </section>
  );
}

export function CategoriesSection({ section }: { section: RenderedSection }) {
  const config = section.config as unknown as S.FeaturedCategoriesConfig;
  const categories = section.categories ?? [];
  if (categories.length === 0) return null;
  return (
    <Section>
      <div className="container-page">
        <SectionHeading title={config.title} subtitle={config.subtitle} cta={{ label: 'All categories', href: '/shop' }} />
        <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-4">
          {categories.map((c) => (
            <Link key={c.id} to={`/category/${c.slug}`} className="group relative block overflow-hidden rounded-theme bg-surface">
              <SmartImage src={c.imageUrl} alt={c.name} ratio="4 / 5" sizes="(min-width: 768px) 25vw, 50vw" className="h-full w-full transition-transform duration-700 group-hover:scale-105" />
              <div className="absolute inset-0 bg-gradient-to-t from-ink/70 via-ink/10 to-transparent" aria-hidden />
              <div className="absolute inset-x-0 bottom-0 p-4 sm:p-5">
                <h3 className="text-xl text-paper sm:text-2xl">{c.name}</h3>
                <p className="mt-1 text-xs tracking-wider text-paper/80 uppercase">{c.productCount} {c.productCount === 1 ? 'piece' : 'pieces'}</p>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </Section>
  );
}

export function ProductsSection({ section }: { section: RenderedSection }) {
  const config = section.config as unknown as S.FeaturedProductsConfig;
  const products = section.products ?? [];
  if (products.length === 0) return null;
  return (
    <Section>
      <div className="container-page">
        <SectionHeading title={config.title} subtitle={config.subtitle} cta={config.cta} />
        <ProductGrid products={products} />
      </div>
    </Section>
  );
}

export function ImageTextSection({ config }: { config: S.ImageTextConfig }) {
  const dark = config.background === 'dark';
  const bg = { surface: 'bg-surface', background: 'bg-bg', dark: 'bg-primary' }[config.background];
  return (
    <Section className={cn(bg, 'my-8')}>
      <div className="container-page grid items-center gap-10 md:grid-cols-2 lg:gap-20">
        <div className={cn(config.imagePosition === 'right' && 'md:order-2')}>
          <SmartImage src={config.imageUrl} alt="" ratio="4 / 5" sizes="(min-width: 768px) 50vw, 100vw" className="w-full rounded-theme" />
        </div>
        <div className="max-w-lg">
          {config.eyebrow && <p className="eyebrow mb-4">{config.eyebrow}</p>}
          <h2 className={cn(dark && 'text-paper')}>{config.heading}</h2>
          {config.body?.split(/\n{2,}/).map((para, i) => (
            <p key={i} className={cn('mt-5 leading-relaxed', dark ? 'text-paper/85' : 'text-ink/80')}>{para}</p>
          ))}
          {config.cta && <SmartLink href={config.cta.href} className={buttonClass(dark ? 'light' : 'outline', 'lg', 'mt-8')}>{config.cta.label}</SmartLink>}
        </div>
      </div>
    </Section>
  );
}

export function PromoBannerSection({ config }: { config: S.PromoBannerConfig }) {
  const theme = { dark: 'bg-primary text-paper', light: 'bg-surface text-ink', accent: 'bg-accent text-paper' }[config.theme];
  const light = config.theme === 'light';
  return (
    <Section className="py-10 sm:py-14">
      <div className="container-page">
        <div className={cn('relative overflow-hidden rounded-theme px-6 py-14 text-center sm:px-14 sm:py-20', theme)}>
          {config.imageUrl && (
            <>
              <SmartImage src={config.imageUrl} alt="" className="absolute inset-0 h-full w-full" />
              <div className="absolute inset-0 bg-ink/50" aria-hidden />
            </>
          )}
          <div className="relative mx-auto max-w-2xl">
            {config.eyebrow && <p className={cn('mb-3 text-xs tracking-[0.3em] uppercase', light ? 'text-accent' : 'text-paper/80')}>{config.eyebrow}</p>}
            <h2 className={cn(!light || config.imageUrl ? 'text-paper' : '')}>{config.heading}</h2>
            {config.text && <p className={cn('mt-4', light && !config.imageUrl ? 'text-muted' : 'text-paper/85')}>{config.text}</p>}
            {config.cta && <SmartLink href={config.cta.href} className={buttonClass(light && !config.imageUrl ? 'primary' : 'light', 'lg', 'mt-8')}>{config.cta.label}</SmartLink>}
          </div>
        </div>
      </div>
    </Section>
  );
}

export function TestimonialsSection({ section }: { section: RenderedSection }) {
  const config = section.config as unknown as S.TestimonialsConfig;
  const items = section.testimonials ?? [];
  if (items.length === 0) return null; // never show placeholder reviews
  return (
    <Section className="bg-surface">
      <div className="container-page">
        <SectionHeading title={config.title} subtitle={config.subtitle} align="center" />
        <div className="grid gap-6 md:grid-cols-3">
          {items.map((t) => (
            <figure key={t.id} className="flex flex-col rounded-theme bg-paper p-8">
              <Quote className="h-6 w-6 text-accent" aria-hidden />
              {t.rating && <div className="mt-4"><Stars value={t.rating} /></div>}
              <blockquote className="mt-4 flex-1 font-heading text-xl leading-snug">“{t.quote}”</blockquote>
              <figcaption className="mt-6 text-sm text-muted">
                <span className="font-medium text-ink">{t.authorName}</span>
                {t.authorLocation && <> · {t.authorLocation}</>}
              </figcaption>
            </figure>
          ))}
        </div>
      </div>
    </Section>
  );
}

export function NewsletterSection({ config }: { config: S.NewsletterConfig }) {
  return (
    <Section>
      <div className="container-page">
        <div className="mx-auto flex max-w-2xl flex-col items-center text-center">
          <p className="eyebrow">Newsletter</p>
          <h2 className="mt-3">{config.title}</h2>
          {config.text && <p className="mt-4 text-muted">{config.text}</p>}
          <div className="mt-8 flex w-full justify-center"><NewsletterForm source="homepage" buttonLabel={config.buttonLabel} /></div>
        </div>
      </div>
    </Section>
  );
}

export function RichTextSection({ config }: { config: S.RichTextConfig }) {
  return (
    <Section>
      <div className={cn('container-page', config.width === 'narrow' && 'max-w-3xl')}>
        {config.title && <h2 className="mb-6">{config.title}</h2>}
        {/* Sanitized on the server; DOMPurify is defence in depth. */}
        <div className="prose-antique" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(config.html) }} />
      </div>
    </Section>
  );
}

export function EraSection({ config }: { config: S.EraShowcaseConfig }) {
  return (
    <Section>
      <div className="container-page">
        <SectionHeading title={config.title} subtitle={config.subtitle} />
        <div className="-mx-4 flex snap-x snap-mandatory gap-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:grid sm:grid-cols-3 sm:overflow-visible sm:px-0 lg:grid-cols-5">
          {config.items.map((era) => (
            <SmartLink key={era.label} href={era.href} className="group relative block w-[62vw] shrink-0 snap-start overflow-hidden rounded-theme bg-surface sm:w-auto">
              <SmartImage src={era.imageUrl} alt="" ratio="3 / 4" sizes="(min-width: 1024px) 20vw, 60vw" className="h-full w-full transition-transform duration-700 group-hover:scale-105" />
              <div className="absolute inset-0 flex items-end bg-gradient-to-t from-ink/65 to-transparent p-5">
                <span className="font-heading text-2xl text-paper">{era.label}</span>
              </div>
            </SmartLink>
          ))}
        </div>
      </div>
    </Section>
  );
}

const trustIcons: Record<S.TrustIcon, typeof Gem> = {
  gem: Gem, camera: Camera, shield: ShieldCheck, truck: Truck, search: Search, heart: Heart, award: Award, clock: Clock, package: Package, sparkles: Sparkles,
};

export function TrustSection({ config }: { config: S.TrustFeaturesConfig }) {
  return (
    <Section className="border-y border-line py-12 sm:py-14">
      <div className="container-page">
        {config.title && <h2 className="mb-10 text-center">{config.title}</h2>}
        <ul className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {config.items.map((item) => {
            const Icon = trustIcons[item.icon] ?? Gem;
            return (
              <li key={item.title} className="flex gap-4">
                <Icon className="mt-1 h-6 w-6 shrink-0 text-accent" strokeWidth={1.5} aria-hidden />
                <div>
                  <h3 className="font-body text-sm font-semibold tracking-wide">{item.title}</h3>
                  <p className="mt-1 text-sm text-muted">{item.text}</p>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    </Section>
  );
}

/** Maps a saved section configuration to its component. Unknown types render nothing. */
export function SectionRenderer({ section }: { section: RenderedSection }) {
  const c = section.config as never;
  switch (section.type) {
    case 'hero': return <HeroSection config={c} />;
    case 'featuredCategories': return <CategoriesSection section={section} />;
    case 'featuredProducts':
    case 'newArrivals':
    case 'collectionProducts': return <ProductsSection section={section} />;
    case 'imageText': return <ImageTextSection config={c} />;
    case 'promoBanner': return <PromoBannerSection config={c} />;
    case 'testimonials': return <TestimonialsSection section={section} />;
    case 'newsletter': return <NewsletterSection config={c} />;
    case 'richText': return <RichTextSection config={c} />;
    case 'eraShowcase': return <EraSection config={c} />;
    case 'trustFeatures': return <TrustSection config={c} />;
    default: return null;
  }
}
