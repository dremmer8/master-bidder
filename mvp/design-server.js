// Tiny zero-dependency dev server for gamedesign.html.
// Serves the mvp/ folder statically and exposes a couple of JSON endpoints so
// the game-design editor can read/write real data files that the main game
// (index.html) loads directly — no build step, no database.
//
// Run: node design-server.js
// Then open http://localhost:8935/gamedesign.html (editor)
//  or  http://localhost:8935/index.html       (the game itself)

const http = require('http');
const fs = require('fs');
const path = require('path');
const { URL } = require('url');

const ROOT = __dirname;
const COLLECTORS_PATH = path.join(ROOT, 'js', 'collectors.js');
const DATA_PATH = path.join(ROOT, 'js', 'data.js');
const PORT = process.env.PORT || 8935;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.webp': 'image/webp',
  '.svg': 'image/svg+xml',
};

const TAG_TYPES = ['period', 'genre', 'artist'];

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' });
  res.end(body);
}

// Data files are plain `const NAME = [...]` scripts meant for a <script> tag,
// not modules — evaluate them in a throwaway function scope to pull the value
// out, same trust level as any other file in this repo.
function loadArrayFromFile(filePath, exportedName) {
  const src = fs.readFileSync(filePath, 'utf8');
  const fn = new Function(src + `\nreturn ${exportedName};`);
  return fn();
}

function loadCollectors() {
  return loadArrayFromFile(COLLECTORS_PATH, 'COLLECTORS');
}

function loadArtworks() {
  return loadArrayFromFile(DATA_PATH, 'ARTWORKS');
}

function uniqSorted(values) {
  return Array.from(new Set(values.filter(Boolean))).sort((a, b) => a.localeCompare(b, 'ru'));
}

function validateCollectors(list) {
  if (!Array.isArray(list) || list.length === 0) {
    return 'Ожидается непустой массив заказчиков.';
  }
  const ids = new Set();
  for (const c of list) {
    if (!c || typeof c !== 'object') return 'Каждый заказчик должен быть объектом.';
    if (typeof c.id !== 'string' || !/^[a-z0-9-]+$/.test(c.id)) {
      return `Некорректный id: "${c.id}". Разрешены строчные латинские буквы, цифры и дефис.`;
    }
    if (ids.has(c.id)) return `Повторяющийся id: "${c.id}".`;
    ids.add(c.id);
    if (typeof c.nameRu !== 'string' || !c.nameRu.trim()) {
      return `У заказчика "${c.id}" отсутствует имя (nameRu).`;
    }
    if (typeof c.taglineRu !== 'string' || !c.taglineRu.trim()) {
      return `У заказчика "${c.id}" отсутствует описание (taglineRu).`;
    }
    if (typeof c.orderGenre !== 'string' || !c.orderGenre.trim()) {
      return `У заказчика "${c.id}" отсутствует orderGenre.`;
    }
    if (typeof c.orderPeriod !== 'string' || !c.orderPeriod.trim()) {
      return `У заказчика "${c.id}" отсутствует orderPeriod.`;
    }
    if (typeof c.orderArtist !== 'string' || !c.orderArtist.trim()) {
      return `У заказчика "${c.id}" отсутствует orderArtist.`;
    }
    if (typeof c.personalModifier !== 'number' || !(c.personalModifier > 0)) {
      return `personalModifier у "${c.id}" должен быть положительным числом.`;
    }
    if (typeof c.baseBudget !== 'number' || !(c.baseBudget > 0)) {
      return `baseBudget у "${c.id}" должен быть положительным числом.`;
    }
  }
  return null;
}

function jsString(value) {
  return "'" + String(value).replace(/\\/g, '\\\\').replace(/'/g, "\\'") + "'";
}

function serializeCollectorsFile(list) {
  const header = `// Collector (client) definitions — each one is a campaign branch: a named,
// recurring character with distinct tastes (see GAME_DESIGN.md, Orders & Collectors).
//
// orderGenre / orderPeriod / orderArtist — the collector's thematic focus. The
// shared ladder in campaign.js applies the same phase structure to everyone:
// ORDER_PHASE_GENRE_DAYS of genre-only orders, then period-only, then artist-only.
//
// portraitSource — Wikimedia Commons URL for the collector's portrait.
// Run \`npm run fetch-collectors\` after changing portraitSource.
//
// This file is generated/edited by gamedesign.html via design-server.js's
// POST /api/collectors — hand edits are fine, just keep the shape intact.
`;

  const entries = list.map((c) => {
    return [
      '  {',
      `    id: ${jsString(c.id)},`,
      `    nameRu: ${jsString(c.nameRu)},`,
      `    taglineRu: ${jsString(c.taglineRu)},`,
      ...(c.portraitSource ? [`    portraitSource:\n      ${jsString(c.portraitSource)},`] : []),
      `    personalModifier: ${c.personalModifier},`,
      `    baseBudget: ${c.baseBudget},`,
      `    orderGenre: ${jsString(c.orderGenre)},`,
      `    orderPeriod: ${jsString(c.orderPeriod)},`,
      `    orderArtist: ${jsString(c.orderArtist)},`,
      '  },',
    ].join('\n');
  });

  return header + 'const COLLECTORS = [\n' + entries.join('\n') + '\n];\n';
}

function handleGetCollectors(res) {
  try {
    sendJson(res, 200, loadCollectors());
  } catch (e) {
    sendJson(res, 500, { error: 'Не удалось прочитать collectors.js: ' + e.message });
  }
}

function handlePostCollectors(req, res) {
  let body = '';
  req.on('data', (chunk) => {
    body += chunk;
    if (body.length > 2_000_000) req.destroy();
  });
  req.on('end', () => {
    let list;
    try {
      list = JSON.parse(body);
    } catch (e) {
      return sendJson(res, 400, { error: 'Некорректный JSON: ' + e.message });
    }
    const validationError = validateCollectors(list);
    if (validationError) return sendJson(res, 400, { error: validationError });
    try {
      fs.writeFileSync(COLLECTORS_PATH, serializeCollectorsFile(list));
      sendJson(res, 200, { ok: true, count: list.length });
    } catch (e) {
      sendJson(res, 500, { error: 'Не удалось записать collectors.js: ' + e.message });
    }
  });
}

function handleGetTagOptions(res) {
  try {
    const artworks = loadArtworks();
    sendJson(res, 200, {
      periods: uniqSorted(artworks.map((a) => a.periodRu)),
      genres: uniqSorted(artworks.map((a) => a.genreRu)),
      artists: uniqSorted(artworks.map((a) => a.artistRu)),
    });
  } catch (e) {
    sendJson(res, 500, { error: 'Не удалось прочитать data.js: ' + e.message });
  }
}

function serveStatic(pathname, res) {
  const rel = pathname === '/' ? '/index.html' : pathname;
  const filePath = path.normalize(path.join(ROOT, rel));
  if (!filePath.startsWith(ROOT)) {
    res.writeHead(403);
    return res.end('Forbidden');
  }
  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      return res.end('Not found: ' + rel);
    }
    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
    res.end(data);
  });
}

const server = http.createServer((req, res) => {
  const { pathname } = new URL(req.url, 'http://localhost');

  if (pathname === '/api/collectors' && req.method === 'GET') return handleGetCollectors(res);
  if (pathname === '/api/collectors' && req.method === 'POST') return handlePostCollectors(req, res);
  if (pathname === '/api/tag-options' && req.method === 'GET') return handleGetTagOptions(res);

  return serveStatic(pathname, res);
});

server.listen(PORT, () => {
  console.log(`Game design server: http://localhost:${PORT}/gamedesign.html`);
  console.log(`Game itself:        http://localhost:${PORT}/index.html`);
});
