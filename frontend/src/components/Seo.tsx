import { useEffect } from 'react';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';

interface SeoProps {
  title?: string;
  description?: string | null;
  image?: string | null;
  noIndex?: boolean;
  type?: 'website' | 'product';
  jsonLd?: Record<string, unknown>;
}

/**
 * Client-side metadata using React 19's native <title>/<meta> hoisting. The server injects equivalent tags
 * (data-seo="server") into index.html for crawlers; they are removed here once React manages the head.
 */
export function Seo({ title, description, image, noIndex, type = 'website', jsonLd }: SeoProps) {
  const { brand } = useSiteConfig();
  useEffect(() => {
    document.querySelectorAll('[data-seo="server"]').forEach((el) => el.remove());
  }, []);

  const fullTitle = title ? `${title} | ${brand.storeName}` : brand.seoDefaultTitle ?? brand.storeName;
  const desc = description ?? brand.seoDefaultDescription ?? brand.description ?? undefined;
  const url = typeof window !== 'undefined' ? window.location.origin + window.location.pathname : undefined;
  const img = image ?? brand.defaultOgImageUrl;
  const absoluteImg = img && img.startsWith('/') && typeof window !== 'undefined' ? window.location.origin + img : img;

  return (
    <>
      <title>{fullTitle}</title>
      {desc && <meta name="description" content={desc} />}
      {noIndex ? <meta name="robots" content="noindex, nofollow" /> : url && <link rel="canonical" href={url} />}
      <meta property="og:title" content={fullTitle} />
      <meta property="og:type" content={type} />
      <meta property="og:site_name" content={brand.storeName} />
      {url && <meta property="og:url" content={url} />}
      {desc && <meta property="og:description" content={desc} />}
      {absoluteImg && <meta property="og:image" content={absoluteImg} />}
      {jsonLd && <script type="application/ld+json">{JSON.stringify(jsonLd).replaceAll('</', '<\\/')}</script>}
    </>
  );
}
