import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const root = new URL('../', import.meta.url);
const source = await readFile(new URL('packages/web-ui/assets/brand-mark.svg', root), 'utf8');
const svg = Buffer.from(source);
const white = { r: 255, g: 255, b: 255, alpha: 1 };
async function icon(path, size, artworkSize, background) {
  const padding = (size - artworkSize) / 2;
  const image = sharp(svg).resize(artworkSize, artworkSize)
    .extend({ top: padding, bottom: padding, left: padding, right: padding, background });
  if (background.alpha === 1) image.flatten({ background });
  await image.png().toFile(fileURLToPath(new URL(path, root)));
}

// Preserve the supplied shield and its original #3B4652 color.
const favicon = source.replace(/<svg\b([^>]*)>/, '<svg$1><rect width="512" height="512" rx="96" fill="#fff"/>');
for (const portal of ['company', 'employee']) {
  await writeFile(new URL(`apps/${portal}/src/app/icon.svg`, root), favicon);
  await icon(`apps/${portal}/src/app/apple-icon.png`, 180, 180, white);
}
await icon('apps/mobile/assets/icon.png', 1024, 1024, white);
// The shield fits inside Android's central adaptive-icon safe area.
await icon('apps/mobile/assets/adaptive-icon.png', 1080, 864, { ...white, alpha: 0 });
await icon('apps/mobile/assets/splash-icon.png', 1024, 640, { ...white, alpha: 0 });
console.log('Generated web, Apple touch, mobile launcher, adaptive and splash icons.');
