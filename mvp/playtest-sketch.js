const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push('PAGEERROR: ' + e.message));
  page.on('console', (msg) => { if (msg.type() === 'error') errors.push('CONSOLE: ' + msg.text()); });

  await page.goto('http://localhost:8934/index.html');
  await page.click('#btn-start-campaign');
  await page.waitForSelector('#screen-brief.active');
  await page.waitForFunction(() => !document.getElementById('btn-start-day').disabled, null, { timeout: 15000 });
  await page.click('#btn-start-day');
  await page.waitForSelector('#screen-auction.active');
  await page.waitForTimeout(1700);
  await page.screenshot({ path: 'shots/7-sketch-layout.png' });

  // trigger skip to confirm it works
  await page.click('#btn-skip');
  await page.waitForTimeout(200);
  await page.screenshot({ path: 'shots/8-skipped.png' });

  // wait long enough on next lot for a rival win to show the raised-hand animation
  await page.waitForTimeout(1600);
  await page.waitForSelector('#lot-result-banner:not(.hidden)', { timeout: 12000 }).catch(() => {});
  await page.screenshot({ path: 'shots/9-rival-hand.png' });

  console.log('ERRORS:', errors.length ? errors.join('\n') : 'none');
  await browser.close();
})();
