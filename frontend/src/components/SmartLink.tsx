import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { isExternal } from '@/utils/format';

/** Renders admin-configured links: internal paths via the router, external URLs in a new tab. */
export function SmartLink({ href, className, children, onClick }: { href: string; className?: string; children: ReactNode; onClick?: () => void }) {
  if (isExternal(href)) {
    return (
      <a href={href} className={className} target="_blank" rel="noopener noreferrer" onClick={onClick}>
        {children}
      </a>
    );
  }
  if (href.startsWith('mailto:') || href.startsWith('tel:')) {
    return <a href={href} className={className} onClick={onClick}>{children}</a>;
  }
  return <Link to={href} className={className} onClick={onClick}>{children}</Link>;
}
