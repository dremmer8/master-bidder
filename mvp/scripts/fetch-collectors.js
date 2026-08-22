// One-time asset bake: download Wikimedia portraits and write compressed WebP
// files under assets/collectors/. Re-run after changing COLLECTORS in js/collectors.js.

const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const ROOT = path.join(__dirname, '..');
const COLLECTORS_PATH = path.join(ROOT, 'js', 'collectors.js');
const OUT_DIR = path.join(ROOT, 'assets', 'collectors');

const DISPLAY_WIDTH = 320;
const WEBP_QUALITY = 80;

function parseCollectors(source) {
  const entries = [];
  const blockRe = /\{\s*\n\s*id:\s*'([^']+)'[\s\S]*?portraitSource:\s*\n\s*'([^']+)'/g;
  let match;
  while ((match = blockRe.exec(source))) {
    entries.push({ id: match[1], portraitSource: match[2] });
  }
  return entries;
}

async function downloadBuffer(url, retries = 6) {
  for (let attempt = 0; attempt <= retries; attempt++) {
    const res = await fetch(url, { redirect: 'follow' });
    if (res.status === 429 && attempt < retries) {
      const waitMs = 5000 * (attempt + 1);
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

  const sourceUrl = entry.portraitSource.replace('width=800', `width=${DISPLAY_WIDTH}`);
  const input = await downloadBuffer(sourceUrl);
  await sharp(input)
    .resize({ width: DISPLAY_WIDTH, height: DISPLAY_WIDTH, fit: 'cover', position: 'top' })
    .webp({ quality: WEBP_QUALITY })
    .toFile(outPath);

  const stat = fs.statSync(outPath);
  console.log(`wrote ${entry.id}.webp (${Math.round(stat.size / 1024)} KB)`);
}

async function main() {
  fs.mkdirSync(OUT_DIR, { recursive: true });
  const source = fs.readFileSync(COLLECTORS_PATH, 'utf8');
  const collectors = parseCollectors(source);
  if (!collectors.length) throw new Error('No collector portraits found in collectors.js');

  console.log(`Compressing ${collectors.length} collector portraits to ${DISPLAY_WIDTH}px WebP…`);
  for (const entry of collectors) {
    try {
      await compressOne(entry);
      await new Promise((r) => setTimeout(r, 3000));
    } catch (err) {
      console.error(`failed ${entry.id}:`, err.message);
      process.exitCode = 1;
    }
  }
}

main();
