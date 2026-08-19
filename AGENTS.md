# AGENTS.md

## Cursor Cloud specific instructions

This repo is a small browser game, "Аукционный дом" (an art-auction buyer simulator), living entirely in `mvp/`. It is a static HTML/CSS/JS game (`mvp/index.html` + `mvp/js/*.js`) served by a tiny zero-dependency Node.js dev server (`mvp/design-server.js`). There is no build step and no database.

### Running the app (dev)
- Start the server: `cd mvp && node design-server.js` (see `mvp/package.json` `npm start`).
- Defaults: `PORT=8935`, `HOST=0.0.0.0`. Game at `/`, data editor at `/gamedesign.html`.
- The editor persists changes back to `mvp/js/collectors.js` via `POST /api/collectors`; `GET /api/tag-options` reads `mvp/js/data.js`. There is no separate backend service.

### Tests / lint / build
- There is no lint step, no build step, and no unit-test framework (`npm test` is just a placeholder that exits 1).
- The de-facto tests are Playwright smoke scripts in `mvp/`: `playtest.js` (main smoke test), `playtest-desktop.js`, `playtest-smart.js`, `playtest-report.js`, `playtest-sketch.js`. They drive a headless browser and write screenshots to `mvp/shots/`.
- Gotcha: these playtest scripts hardcode `http://localhost:8934`, but the server defaults to `8935`. Start the server on 8934 first, e.g. `PORT=8934 node design-server.js`, then run `node playtest.js` from `mvp/`.

### Playwright notes (non-obvious)
- `node_modules/` is committed, but the committed `node_modules/.bin/playwright` wrapper is not executable in the read-only checkout, so `npx playwright ...` fails with "Permission denied". Invoke the CLI via node instead: `node mvp/node_modules/playwright-core/cli.js install chromium`.
- The Playwright browser binary is not committed; the update script refreshes it. The chromium system libraries were installed once during environment setup via `--with-deps`; if a fresh VM ever lacks them, re-run `node mvp/node_modules/playwright-core/cli.js install --with-deps chromium` (needs sudo/apt).
