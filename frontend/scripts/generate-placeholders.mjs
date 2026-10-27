// Generates clearly-labelled placeholder artwork into public/placeholders.
// These stand in for real product photography until the store owner uploads photos.
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const out = join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'placeholders');
mkdirSync(out, { recursive: true });

const ink = '#45382B';
const gold = '#B08D57';

// Simple line drawings, centred on a 400x400 box.
const drawings = {
  furniture: `<path d="M120 120 h160 v110 h-160z M120 230 v70 M280 230 v70 M130 300 h20 M250 300 h20 M120 175 h160 M200 120 v110"/>
              <circle cx="185" cy="148" r="4"/><circle cx="215" cy="148" r="4"/><circle cx="185" cy="203" r="4"/><circle cx="215" cy="203" r="4"/>`,
  clock: `<rect x="135" y="80" width="130" height="240" rx="60"/><circle cx="200" cy="170" r="48"/>
          <path d="M200 170 v-30 M200 170 l22 14 M175 320 h50 M200 218 v60"/><circle cx="200" cy="285" r="10"/>`,
  frame: `<rect x="105" y="95" width="190" height="230"/><rect x="125" y="115" width="150" height="190"/>
          <path d="M125 270 l45 -50 l35 35 l25 -25 l45 45"/><circle cx="235" cy="160" r="14"/>`,
  vase: `<path d="M175 90 h50 M180 90 c0 30 -60 60 -60 130 c0 60 35 95 80 95 c45 0 80 -35 80 -95 c0 -70 -60 -100 -60 -130"/>
         <path d="M140 210 c40 15 80 15 120 0"/>`,
  gem: `<path d="M140 150 l60 -60 l60 60 l-60 150z M140 150 h120 M175 150 l25 -60 l25 60 l-25 150"/>`,
  lamp: `<path d="M150 110 h100 l30 90 h-160z M200 200 v90 M160 300 h80 M185 290 h30"/>`,
  mirror: `<ellipse cx="200" cy="195" rx="85" ry="115"/><ellipse cx="200" cy="195" rx="68" ry="96"/>
           <path d="M200 80 c-10 -20 10 -20 0 -20 M160 120 l20 -20"/>`,
  box: `<path d="M110 170 h180 v110 h-180z M110 170 l20 -40 h140 l20 40 M190 205 h20 v20 h-20z"/>`,
  chair: `<path d="M140 90 v210 M260 90 v210 M140 90 h120 M140 190 h120 M140 140 h120 M150 300 h0"/>`,
};

const svg = ({ w, h, drawing, label, title, scale = 1, tone = '#F0E8DC' }) => `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" role="img" aria-label="${label}">
  <rect width="${w}" height="${h}" fill="${tone}"/>
  <rect x="24" y="24" width="${w - 48}" height="${h - 48}" fill="none" stroke="${gold}" stroke-opacity="0.35"/>
  <g transform="translate(${w / 2 - 200 * scale} ${h / 2 - 220 * scale}) scale(${scale})" fill="none" stroke="${ink}" stroke-width="${3 / scale}" stroke-linecap="round" stroke-linejoin="round" opacity="0.75">
    ${drawing}
  </g>
  ${title ? `<text x="50%" y="${h - 110}" text-anchor="middle" font-family="Georgia, serif" font-size="${Math.round(w / 22)}" fill="${ink}">${title}</text>` : ''}
  <text x="50%" y="${h - 60}" text-anchor="middle" font-family="Arial, sans-serif" font-size="${Math.max(14, Math.round(w / 60))}" letter-spacing="3" fill="${ink}" opacity="0.55">PLACEHOLDER IMAGE</text>
</svg>`;

const write = (name, content) => writeFileSync(join(out, name), content);

for (const [key, drawing] of Object.entries(drawings)) {
  write(`product-${key}.svg`, svg({ w: 1200, h: 1500, drawing, label: `Placeholder ${key} image`, scale: 2.2 }));
  write(`product-${key}-alt.svg`, svg({ w: 1200, h: 1500, drawing, label: `Placeholder ${key} detail image`, scale: 3.2, tone: '#EAE0D1' }));
  write(`category-${key}.svg`, svg({ w: 900, h: 1100, drawing, label: `Placeholder ${key} category image`, scale: 1.7 }));
}

write('hero.svg', svg({ w: 2400, h: 1350, drawing: drawings.furniture + '<g transform="translate(330 30) scale(0.8)">' + drawings.lamp + '</g>', label: 'Placeholder hero image', scale: 2.4, tone: '#D9CDBB' }));
write('story.svg', svg({ w: 1400, h: 1600, drawing: drawings.clock, label: 'Placeholder story image', scale: 2.4, tone: '#E6DCCB' }));

const eras = { colonial: ['Colonial', 'furniture'], victorian: ['Victorian', 'clock'], artdeco: ['Art Deco', 'lamp'], midcentury: ['Mid-Century', 'chair'], rustic: ['Rustic Vintage', 'box'] };
for (const [key, [title, d]] of Object.entries(eras)) {
  write(`era-${key}.svg`, svg({ w: 900, h: 1100, drawing: drawings[d], label: `Placeholder ${title} image`, scale: 1.6, tone: '#E9DFD0' }));
}

writeFileSync(join(out, '..', 'favicon.svg'), `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="10" fill="${ink}"/><text x="32" y="44" text-anchor="middle" font-family="Georgia, serif" font-size="34" fill="${gold}">A</text></svg>`);
console.warn(`Placeholders written to ${out}`);

