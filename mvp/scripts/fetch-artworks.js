// One-time asset bake: download Wikimedia thumbnails and write compressed WebP
// files under assets/art/. Re-run after changing ARTWORKS in js/data.js.

const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const ROOT = path.join(__dirname, '..');
const DATA_PATH = path.join(ROOT, 'js', 'data.js');
const OUT_DIR = path.join(ROOT, 'assets', 'art');

const DISPLAY_WIDTH = 512;
const WEBP_QUALITY = 78;

function parseArtworks(source) {
  const entries = [];
  const blockRe = /\{\s*\n\s*id:\s*'([^']+)'[\s\S]*?imageSource:\s*'([^']+)'/g;
  let match;
  while ((match = blockRe.exec(source))) {
    entries.push({ id: match[1], imageSource: match[2] });
  }
  return entries;
}

async function downloadBuffer(url, retries = 4) {
  for (let attempt = 0; attempt <= retries; attempt++) {
    const res = await fetch(url, { redirect: 'follow' });
    if (res.status === 429 && attempt < retries) {
      const waitMs = 2000 * (attempt + 1);
      console.log(`rate limited, waiting ${waitMs}ms…`);
      await new Promise((r) => setTimeout(r, waitMs));
      continue;
    }
    if (!res.ok) throw new Error(`HTTP ${res.status} for ${url}`);
    return Buffer.from(await res.arrayBuffer());
  }
  throw new Error(`Failed after retries: ${url}`);
}

async function compressOne(entry) {
  const outPath = path.join(OUT_DIR, `${entry.id}.webp`);
  if (fs.existsSync(outPath)) {
    const stat = fs.statSync(outPath);
    console.log(`skip ${entry.id}.webp (${Math.round(stat.size / 1024)} KB)`);
    return;
  }

  const sourceUrl = entry.imageSource.replace('width=800', `width=${DISPLAY_WIDTH}`);
  const input = await downloadBuffer(sourceUrl);
  await sharp(input)
    .resize({ width: DISPLAY_WIDTH, withoutEnlargement: true })
    .webp({ quality: WEBP_QUALITY })
    .toFile(outPath);

  const stat = fs.statSync(outPath);
  console.log(`wrote ${entry.id}.webp (${Math.round(stat.size / 1024)} KB)`);
}

async function main() {
  fs.mkdirSync(OUT_DIR, { recursive: true });
  const source = fs.readFileSync(DATA_PATH, 'utf8');
  const artworks = parseArtworks(source);
  if (!artworks.length) throw new Error('No artworks found in data.js');

  console.log(`Compressing ${artworks.length} artworks to ${DISPLAY_WIDTH}px WebP…`);
  for (const entry of artworks) {
    try {
      await compressOne(entry);
      await new Promise((r) => setTimeout(r, 1200));
    } catch (err) {
      console.error(`failed ${entry.id}:`, err.message);
      process.exitCode = 1;
    }
  }
}

main();
