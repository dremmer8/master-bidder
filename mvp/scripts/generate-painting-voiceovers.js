/**
 * Batch-generate modular ElevenLabs voiceovers by field type.
 *
 * Shared (deduped): genre / period / artist / year
 * Per painting:     title / fact  (keyed by artwork id)
 *
 * Writes:
 *   master-bidder-3d/Assets/content/paintings/audio/{field}/<key>.mp3
 *
 * Setup:
 *   1. Copy scripts/.env.elevenlabs.example → scripts/.env.elevenlabs
 *   2. Fill ELEVENLABS_API_KEY and ELEVENLABS_VOICE_ID
 *   3. npm run voiceovers -- --list-voices
 *   4. npm run voiceovers -- --dry-run
 *   5. npm run voiceovers
 *
 * Flags:
 *   --fields genre,period,artist,year,title,fact
 *   --ids a,b,c     only clips needed for these paintings (test one: --ids mona-lisa)
 *   --force --dry-run --list-voices --delay MS --limit N
 */

const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const REPO_ROOT = path.join(ROOT, '..');
const ARTWORKS_JSON = path.join(
  REPO_ROOT,
  'master-bidder-3d',
  'Assets',
  'content',
  'paintings',
  'mvp_artworks.json'
);
const OUT_DIR = path.join(
  REPO_ROOT,
  'master-bidder-3d',
  'Assets',
  'content',
  'paintings',
  'audio'
);
const MANIFEST_PATH = path.join(OUT_DIR, 'manifest.json');
const ENV_PATH = path.join(__dirname, '.env.elevenlabs');

const API_BASE = 'https://api.elevenlabs.io/v1';
const DEFAULT_MODEL = 'eleven_multilingual_v2';
const DEFAULT_OUTPUT_FORMAT = 'mp3_44100_128';
const DEFAULT_DELAY_MS = 400;

const ALL_FIELDS = ['genre', 'period', 'artist', 'year', 'title', 'fact'];
const SHARED_FIELDS = new Set(['genre', 'period', 'artist', 'year']);

const CYR = {
  а: 'a', б: 'b', в: 'v', г: 'g', д: 'd', е: 'e', ё: 'yo', ж: 'zh', з: 'z',
  и: 'i', й: 'y', к: 'k', л: 'l', м: 'm', н: 'n', о: 'o', п: 'p', р: 'r',
  с: 's', т: 't', у: 'u', ф: 'f', х: 'kh', ц: 'ts', ч: 'ch', ш: 'sh', щ: 'sch',
  ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu', я: 'ya',
};

function slug(value) {
  if (!value || !String(value).trim()) return 'empty';
  let out = '';
  for (const raw of String(value).trim().toLowerCase()) {
    if (CYR[raw] !== undefined) {
      out += CYR[raw];
      continue;
    }
    if (/[a-z0-9]/i.test(raw)) out += raw;
    else if (out && out[out.length - 1] !== '-') out += '-';
  }
  out = out.replace(/-+/g, '-').replace(/^-|-$/g, '');
  if (out.length > 80) out = out.slice(0, 80).replace(/-$/, '');
  return out || 'empty';
}

function clean(value) {
  if (!value) return '';
  return String(value).trim().replace(/[.]+$/, '');
}

function rawField(art, field) {
  switch (field) {
    case 'genre': return art.genreRu;
    case 'period': return art.periodRu;
    case 'artist': return art.artistRu;
    case 'year': return art.year;
    case 'title': return art.titleRu;
    case 'fact': return art.factRu;
    default: return '';
  }
}

const ONES_NOM = {
  1: 'первый', 2: 'второй', 3: 'третий', 4: 'четвёртый', 5: 'пятый',
  6: 'шестой', 7: 'седьмой', 8: 'восьмой', 9: 'девятый', 10: 'десятый',
  11: 'одиннадцатый', 12: 'двенадцатый', 13: 'тринадцатый', 14: 'четырнадцатый',
  15: 'пятнадцатый', 16: 'шестнадцатый', 17: 'семнадцатый', 18: 'восемнадцатый',
  19: 'девятнадцатый',
};
const ONES_GEN = {
  1: 'первого', 2: 'второго', 3: 'третьего', 4: 'четвёртого', 5: 'пятого',
  6: 'шестого', 7: 'седьмого', 8: 'восьмого', 9: 'девятого', 10: 'десятого',
  11: 'одиннадцатого', 12: 'двенадцатого', 13: 'тринадцатого', 14: 'четырнадцатого',
  15: 'пятнадцатого', 16: 'шестнадцатого', 17: 'семнадцатого', 18: 'восемнадцатого',
  19: 'девятнадцатого',
};
const TENS_NOM = {
  20: 'двадцатый', 30: 'тридцатый', 40: 'сороковой', 50: 'пятидесятый',
  60: 'шестидесятый', 70: 'семидесятый', 80: 'восьмидесятый', 90: 'девяностый',
};
const TENS_GEN = {
  20: 'двадцатого', 30: 'тридцатого', 40: 'сорокового', 50: 'пятидесятого',
  60: 'шестидесятого', 70: 'семидесятого', 80: 'восьмидесятого', 90: 'девяностого',
};
const TENS_CARD = {
  20: 'двадцать', 30: 'тридцать', 40: 'сорок', 50: 'пятьдесят',
  60: 'шестьдесят', 70: 'семьдесят', 80: 'восемьдесят', 90: 'девяносто',
};
const TENS_CARD_GEN = {
  20: 'двадцати', 30: 'тридцати', 40: 'сорока', 50: 'пятидесяти',
  60: 'шестидесяти', 70: 'семидесяти', 80: 'восьмидесяти', 90: 'девяноста',
};
const HUND_NOM = {
  1: 'сто', 2: 'двести', 3: 'триста', 4: 'четыреста', 5: 'пятьсот',
  6: 'шестьсот', 7: 'семьсот', 8: 'восемьсот', 9: 'девятьсот',
};
const HUND_GEN = {
  1: 'ста', 2: 'двухсот', 3: 'трёхсот', 4: 'четырёхсот', 5: 'пятисот',
  6: 'шестисот', 7: 'семисот', 8: 'восьмисот', 9: 'девятисот',
};

function below100(n, gen) {
  if (n < 20) return (gen ? ONES_GEN : ONES_NOM)[n] || '';
  const tens = Math.floor(n / 10) * 10;
  const one = n % 10;
  if (one === 0) return (gen ? TENS_GEN : TENS_NOM)[tens] || '';
  return `${(gen ? TENS_CARD_GEN : TENS_CARD)[tens]} ${(gen ? ONES_GEN : ONES_NOM)[one]}`;
}

function below1000(n, gen) {
  if (n <= 0) return gen ? 'нулевого' : 'нулевой';
  const hundreds = Math.floor(n / 100);
  const rest = n % 100;
  const parts = [];
  if (hundreds > 0) parts.push((gen ? HUND_GEN : HUND_NOM)[hundreds]);
  if (rest > 0) parts.push(below100(rest, gen));
  return parts.join(' ').trim();
}

function yearToRussian(year, gen) {
  if (year >= 1000 && year <= 1999) {
    const rem = year - 1000;
    if (gen) return rem === 0 ? 'тысячи' : `тысячи ${below1000(rem, true)}`;
    return rem === 0 ? 'тысячный' : `тысяча ${below1000(rem, false)}`;
  }
  if (year >= 2000 && year <= 2099) {
    const rem = year - 2000;
    if (gen) return rem === 0 ? 'двухтысячного' : `двух тысяч ${below1000(rem, true)}`;
    return rem === 0 ? 'двухтысячный' : `две тысячи ${below1000(rem, false)}`;
  }
  return below1000(year, !!gen);
}

function expandDatesAndNumbers(input) {
  return String(input).replace(
    /(?<prefix>ок\.?\s*|около\s*)?(?<a>\d{3,4})(?:\s*[–—-]\s*(?<b>\d{3,4}))?/giu,
    (...args) => {
      const g = args[args.length - 1];
      const approx = !!(g.prefix && String(g.prefix).trim());
      const a = Number(g.a);
      if (g.b) {
        const b = Number(g.b);
        const core = `с ${yearToRussian(a, true)} по ${yearToRussian(b, false)} год`;
        return approx ? `около периода ${core}` : core;
      }
      if (approx) return `около ${yearToRussian(a, true)} года`;
      return `${yearToRussian(a, false)} год`;
    }
  );
}

function uncapitalizeFirst(text) {
  if (!text) return text;
  return text.charAt(0).toLocaleLowerCase('ru-RU') + text.slice(1);
}

function formatSpoken(field, rawValue) {
  if (!rawValue || !String(rawValue).trim()) return '';
  let text = String(rawValue).trim().replace(/\u00A0/g, ' ').replace(/\s+/g, ' ');
  text = expandDatesAndNumbers(text);
  text = text.replace(/(\.\.\.|…|,|;|:)\s*$/u, '').replace(/[.!?]+$/u, '').trim();
  if (!text) return '';

  switch (field) {
    case 'genre':
      text = `Жанр — ${uncapitalizeFirst(text)}`;
      break;
    case 'period':
      text = `Стиль — ${uncapitalizeFirst(text)}`;
      break;
    case 'artist':
      text = `Автор — ${text}`;
      break;
    case 'year':
      if (!/^около/i.test(text) && !/^(год|тысяч)/i.test(text)) text = `Год — ${text}`;
      break;
    case 'title':
      text = `Название — ${text}`;
      break;
    default:
      break;
  }
  return `${text}.`;
}

function fieldValue(art, field) {
  return formatSpoken(field, rawField(art, field));
}

function fieldKey(art, field) {
  if (SHARED_FIELDS.has(field)) return slug(clean(rawField(art, field)));
  return art.id;
}

function loadEnvFile(filePath) {
  if (!fs.existsSync(filePath)) return;
  for (const line of fs.readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eq = trimmed.indexOf('=');
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq).trim();
    let value = trimmed.slice(eq + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    if (!(key in process.env)) process.env[key] = value;
  }
}

function parseArgs(argv) {
  const opts = {
    fields: ALL_FIELDS.slice(),
    ids: null,
    force: false,
    dryRun: false,
    listVoices: false,
    delayMs: DEFAULT_DELAY_MS,
    limit: null,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--force') opts.force = true;
    else if (arg === '--dry-run') opts.dryRun = true;
    else if (arg === '--list-voices') opts.listVoices = true;
    else if (arg === '--fields') {
      opts.fields = String(argv[++i] || '')
        .split(',')
        .map((s) => s.trim())
        .filter((s) => ALL_FIELDS.includes(s));
    } else if (arg === '--ids') {
      opts.ids = new Set(
        String(argv[++i] || '')
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean)
      );
    } else if (arg === '--delay') opts.delayMs = Number(argv[++i]);
    else if (arg === '--limit') opts.limit = Number(argv[++i]);
    else if (arg === '--help' || arg === '-h') opts.help = true;
    else console.warn(`Unknown arg: ${arg}`);
  }
  return opts;
}

function collectJobs(artworks, fields) {
  const jobs = [];
  const seen = new Set();

  for (const field of fields) {
    for (const art of artworks) {
      const text = fieldValue(art, field);
      if (!text) continue;

      const key = fieldKey(art, field);
      if (!key) continue;

      const dedupe = `${field}|${key}`;
      if (seen.has(dedupe)) continue;
      seen.add(dedupe);

      jobs.push({
        field,
        key,
        text,
        outPath: path.join(OUT_DIR, field, `${key}.mp3`),
        rel: path.join(field, `${key}.mp3`),
      });
    }
  }

  jobs.sort((a, b) => a.field.localeCompare(b.field) || a.key.localeCompare(b.key));
  return jobs;
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function apiGet(pathname, apiKey) {
  const res = await fetch(`${API_BASE}${pathname}`, {
    headers: { 'xi-api-key': apiKey },
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`GET ${pathname} → HTTP ${res.status}: ${text.slice(0, 400)}`);
  return JSON.parse(text);
}

async function synthesize(voiceId, text, apiKey, modelId, outputFormat) {
  const url = `${API_BASE}/text-to-speech/${encodeURIComponent(voiceId)}?output_format=${encodeURIComponent(outputFormat)}`;
  const res = await fetch(url, {
    method: 'POST',
    headers: {
      'xi-api-key': apiKey,
      'Content-Type': 'application/json',
      Accept: 'audio/mpeg',
    },
    body: JSON.stringify({
      text,
      model_id: modelId,
      apply_text_normalization: 'on',
    }),
  });
  if (!res.ok) {
    const errText = await res.text();
    throw new Error(`TTS HTTP ${res.status}: ${errText.slice(0, 500)}`);
  }
  return Buffer.from(await res.arrayBuffer());
}

function loadManifest() {
  if (!fs.existsSync(MANIFEST_PATH)) return {};
  try {
    return JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));
  } catch {
    return {};
  }
}

function saveManifest(manifest) {
  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
}

async function listVoices(apiKey) {
  const data = await apiGet('/voices', apiKey);
  const voices = data.voices || [];
  console.log(`Found ${voices.length} voices:\n`);
  for (const v of voices) {
    console.log(`${v.voice_id}  ${v.name}`);
  }
}

async function main() {
  loadEnvFile(ENV_PATH);
  const opts = parseArgs(process.argv.slice(2));
  if (opts.help) {
    console.log('See file header for flags.');
    return;
  }

  const apiKey = process.env.ELEVENLABS_API_KEY;
  if (!opts.dryRun && !apiKey) {
    console.error('Missing ELEVENLABS_API_KEY in scripts/.env.elevenlabs');
    process.exit(1);
  }

  if (opts.listVoices) {
    await listVoices(apiKey);
    return;
  }

  const voiceId = process.env.ELEVENLABS_VOICE_ID;
  if (!opts.dryRun && !voiceId) {
    console.error('Missing ELEVENLABS_VOICE_ID. Run with --list-voices');
    process.exit(1);
  }

  const modelId = process.env.ELEVENLABS_MODEL_ID || DEFAULT_MODEL;
  const outputFormat = process.env.ELEVENLABS_OUTPUT_FORMAT || DEFAULT_OUTPUT_FORMAT;

  const artworksAll = JSON.parse(fs.readFileSync(ARTWORKS_JSON, 'utf8'));
  if (!Array.isArray(artworksAll) || !artworksAll.length) {
    console.error('mvp_artworks.json empty');
    process.exit(1);
  }

  let artworks = artworksAll;
  if (opts.ids) {
    artworks = artworksAll.filter((a) => opts.ids.has(a.id));
    const missing = [...opts.ids].filter((id) => !artworks.some((a) => a.id === id));
    if (missing.length) console.warn(`Unknown ids: ${missing.join(', ')}`);
    if (!artworks.length) {
      console.error('No artworks matched --ids');
      process.exit(1);
    }
  }

  let jobs = collectJobs(artworks, opts.fields);
  if (opts.limit != null && Number.isFinite(opts.limit)) jobs = jobs.slice(0, opts.limit);

  const counts = {};
  for (const j of jobs) counts[j.field] = (counts[j.field] || 0) + 1;
  console.log(
    `${opts.dryRun ? '[dry-run] ' : ''}Jobs: ${jobs.length}  ` +
      Object.entries(counts)
        .map(([k, v]) => `${k}=${v}`)
        .join(' ')
  );

  if (opts.dryRun) {
    let chars = 0;
    for (const j of jobs) {
      chars += j.text.length;
      console.log(`${j.rel}  (${j.text.length})  ${j.text}`);
    }
    console.log(`Estimated credits ≈ ${chars}`);
    return;
  }

  fs.mkdirSync(OUT_DIR, { recursive: true });
  for (const f of opts.fields) fs.mkdirSync(path.join(OUT_DIR, f), { recursive: true });

  const manifest = loadManifest();
  let written = 0;
  let skipped = 0;
  let failed = 0;
  let chars = 0;

  for (let i = 0; i < jobs.length; i++) {
    const job = jobs[i];
    const prev = manifest[job.rel];
    const exists = fs.existsSync(job.outPath);
    const unchanged = prev && prev.text === job.text && exists;

    console.log(`\n[${i + 1}/${jobs.length}] ${job.rel}  (${job.text.length} chars)`);

    if (!opts.force && (exists || unchanged)) {
      console.log('skip');
      skipped++;
      continue;
    }

    try {
      const audio = await synthesize(voiceId, job.text, apiKey, modelId, outputFormat);
      fs.writeFileSync(job.outPath, audio);
      manifest[job.rel] = {
        field: job.field,
        key: job.key,
        text: job.text,
        chars: job.text.length,
        voiceId,
        modelId,
        bytes: audio.length,
        generatedAt: new Date().toISOString(),
      };
      saveManifest(manifest);
      written++;
      chars += job.text.length;
      console.log(`wrote (${Math.round(audio.length / 1024)} KB)`);
      if (i < jobs.length - 1 && opts.delayMs > 0) await sleep(opts.delayMs);
    } catch (err) {
      failed++;
      console.error(`FAILED:`, err.message);
      process.exitCode = 1;
    }
  }

  console.log(`\nDone. written=${written} skipped=${skipped} failed=${failed} chars≈${chars}`);
  console.log('Next in Unity: Master Bidder → Painting Voiceovers → Rebuild Library Only');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
