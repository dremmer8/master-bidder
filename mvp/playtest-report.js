const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push('PAGEERROR: ' + e.message));
  page.on('console', (msg) => { if (msg.type() === 'error') errors.push('CONSOLE: ' + msg.text()); });

  await page.goto('http://localhost:8934/index.html');
  await page.click('#btn-start-campaign');
  await page.waitForSelector('#screen-brief.active');
  await page.waitForFunction(() => !document.getElementById('btn-start-day').disabled, null, { timeout: 15000 });
  await page.click('#btn-start-day');
  await page.waitForSelector('#screen-auction.active');
  await page.click('#btn-start-lot');

  // buy every lot regardless of match, to get a mix of correct/incorrect in the report
  for (let i = 0; i < 14; i++) {
    const active = await page.$('#screen-auction.active');
    if (!active) break;
    await page.waitForTimeout(900);
    const buyBtn = await page.$('#btn-buy:not([disabled])');
    if (buyBtn) await buyBtn.click();
    await page.waitForTimeout(1600);
  }

  await page.waitForSelector('#screen-report.active', { timeout: 20000 });
  await page.screenshot({ path: 'shots/13-detailed-report.png', fullPage: true });
  console.log('ERRORS:', errors.length ? errors.join('\n') : 'none');
  await browser.close();
})();
