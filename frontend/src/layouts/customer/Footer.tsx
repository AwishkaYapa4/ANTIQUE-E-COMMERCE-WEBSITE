import { useState, type FormEvent } from 'react';
import { Mail, MapPin, MessageCircle, Phone } from 'lucide-react';
import { toast } from 'sonner';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { siteApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { SmartLink } from '@/components/SmartLink';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Field';
import { Logo } from './Header';

export function NewsletterForm({ source, buttonLabel = 'Subscribe', dark = false }: { source: string; buttonLabel?: string; dark?: boolean }) {
  const [email, setEmail] = useState('');
  const [website, setWebsite] = useState('');
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!/^\S+@\S+\.\S+$/.test(email)) {
      toast.error('Enter a valid email address.');
      return;
    }
    setBusy(true);
    try {
      await siteApi.subscribe(email, source, website);
      setDone(true);
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };
  if (done) return <p className={dark ? 'text-button-ink' : 'text-success'}>Thank you — you're on the list.</p>;
  return (
    <form onSubmit={submit} className="flex w-full max-w-md flex-col gap-2 sm:flex-row" noValidate>
      <label htmlFor={`nl-${source}`} className="sr-only">Email address</label>
      <Input id={`nl-${source}`} type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Your email address" autoComplete="email" />
      <input type="text" value={website} onChange={(e) => setWebsite(e.target.value)} tabIndex={-1} autoComplete="off" aria-hidden className="absolute -left-[9999px]" />
      <Button type="submit" variant={dark ? 'accent' : 'primary'} loading={busy}>{buttonLabel}</Button>
    </form>
  );
}

export function Footer() {
  const { brand, menu } = useSiteConfig();
  const columns = [menu('footer-shop'), menu('footer-help')].filter((m) => m.items.length > 0);
  const year = new Date().getFullYear();
  const whatsApp = brand.whatsAppNumber?.replace(/[^\d]/g, '');

  return (
    <footer className="mt-24 border-t border-line bg-surface">
      <div className="container-page grid gap-12 py-16 md:grid-cols-2 lg:grid-cols-12">
        <div className="lg:col-span-4">
          <Logo />
          {(brand.footerDescription ?? brand.description) && (
            <p className="mt-5 max-w-sm text-sm leading-relaxed text-muted">{brand.footerDescription ?? brand.description}</p>
          )}
          {brand.socialLinks.length > 0 && (
            <ul className="mt-6 flex flex-wrap gap-4 text-sm">
              {brand.socialLinks.map((s) => (
                <li key={s.url}>
                  <a href={s.url} target="_blank" rel="noopener noreferrer" className="capitalize text-ink hover:text-accent">{s.platform}</a>
                </li>
              ))}
            </ul>
          )}
        </div>

        {columns.map((col) => (
          <nav key={col.location} className="lg:col-span-2" aria-label={col.title}>
            <h2 className="mb-4 font-body text-xs font-semibold tracking-[0.18em] text-ink uppercase">{col.title}</h2>
            <ul className="space-y-2.5 text-sm">
              {col.items.map((item) => (
                <li key={item.label}><SmartLink href={item.href} className="text-muted transition-colors hover:text-ink">{item.label}</SmartLink></li>
              ))}
            </ul>
          </nav>
        ))}

        <div className="lg:col-span-4">
          <h2 className="mb-4 font-body text-xs font-semibold tracking-[0.18em] text-ink uppercase">Contact</h2>
          <ul className="space-y-2.5 text-sm text-muted">
            {brand.contactPhone && <li className="flex gap-2"><Phone className="mt-0.5 h-4 w-4 shrink-0" aria-hidden /><a href={`tel:${brand.contactPhone}`} className="hover:text-ink">{brand.contactPhone}</a></li>}
            {brand.contactEmail && <li className="flex gap-2"><Mail className="mt-0.5 h-4 w-4 shrink-0" aria-hidden /><a href={`mailto:${brand.contactEmail}`} className="hover:text-ink">{brand.contactEmail}</a></li>}
            {whatsApp && <li className="flex gap-2"><MessageCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden /><a href={`https://wa.me/${whatsApp}`} target="_blank" rel="noopener noreferrer" className="hover:text-ink">WhatsApp us</a></li>}
            {brand.address && <li className="flex gap-2"><MapPin className="mt-0.5 h-4 w-4 shrink-0" aria-hidden /><span className="whitespace-pre-line">{brand.address}</span></li>}
            {!brand.contactPhone && !brand.contactEmail && !brand.address && <li><SmartLink href="/contact" className="hover:text-ink">Send us a message</SmartLink></li>}
          </ul>
          {brand.newsletterEnabled && (
            <div className="mt-8">
              <p className="mb-3 text-sm text-ink">New arrivals, first.</p>
              <NewsletterForm source="footer" />
            </div>
          )}
        </div>
      </div>
      <div className="border-t border-line">
        <div className="container-page flex flex-col gap-2 py-5 text-xs text-muted sm:flex-row sm:justify-between">
          <p>© {year} {brand.copyrightText ?? brand.storeName}</p>
          <p>All prices in LKR · Secure checkout · No card details stored</p>
        </div>
      </div>
    </footer>
  );
}
