const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1366, height: 768 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push('PAGEERROR: ' + e.message));
  page.on('console', (msg) => { if (msg.type() === 'error') errors.push('CONSOLE: ' + msg.text()); });

  await page.goto('http://localhost:8934/index.html');
  await page.screenshot({ path: 'shots/10-desktop-intro.png' });

  await page.click('#btn-start-campaign');
  await page.waitForSelector('#screen-brief.active');
  await page.waitForFunction(() => !document.getElementById('btn-start-day').disabled, null, { timeout: 15000 });
  await page.click('#btn-start-day');
  await page.waitForSelector('#screen-auction.active');
  await page.click('#btn-start-lot');
  await page.waitForTimeout(5000); // let most fields reveal
  await page.screenshot({ path: 'shots/11-desktop-auction.png' });

  // check for any page-level scroll
  const scrollInfo = await page.evaluate(() => ({
    bodyScrollHeight: document.body.scrollHeight,
    windowInnerHeight: window.innerHeight,
    hasScroll: document.body.scrollHeight > window.innerHeight,
  }));
  console.log('SCROLL CHECK:', JSON.stringify(scrollInfo));
  console.log('ERRORS:', errors.length ? errors.join('\n') : 'none');

  await browser.close();
})();
