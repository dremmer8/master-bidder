const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push('PAGEERROR: ' + e.message));
  page.on('console', (msg) => {
    if (msg.type() === 'error') errors.push('CONSOLE: ' + msg.text());
  });

  await page.goto('http://localhost:8934/index.html');
  await page.screenshot({ path: 'shots/1-intro.png' });

  await page.click('#btn-start-campaign');
  await page.waitForSelector('#screen-brief.active');
  await page.click('#brief-branch-choice .venue-card'); // pick a client branch for today's order
  await page.screenshot({ path: 'shots/2-brief.png' });

  await page.click('#btn-start-day');
  await page.waitForSelector('#screen-auction.active');
  await page.waitForTimeout(1700); // let title field reveal
  await page.screenshot({ path: 'shots/3-auction-mid-reveal.png' });

  // try to buy immediately
  await page.click('#btn-buy');
  await page.waitForTimeout(300);
  await page.screenshot({ path: 'shots/4-lot-resolved.png' });

  // spin through remaining lots quickly: alternate buy / skip so the smoke
  // test never has to idle out a slow-rival venue tier (e.g. the 'local'
  // tier a fresh collector branch starts on has a 2.2x rival-speed factor).
  for (let i = 0; i < 15; i++) {
    const stillAuction = await page.$('#screen-auction.active');
    if (!stillAuction) break;
    if (i % 2 === 0) {
      const buyBtn = await page.$('#btn-buy:not([disabled])');
      if (buyBtn) await buyBtn.click();
    } else {
      const skipBtn = await page.$('#btn-skip:not([disabled])');
      if (skipBtn) await skipBtn.click();
    }
    await page.waitForTimeout(900);
  }

  await page.waitForSelector('#screen-report.active', { timeout: 20000 });
  await page.screenshot({ path: 'shots/5-report.png' });

  const reportHtml = await page.$eval('#screen-report', (el) => el.innerText);
  console.log('--- REPORT TEXT ---');
  console.log(reportHtml);

  console.log('--- ERRORS ---');
  console.log(errors.length ? errors.join('\n') : 'none');

  await browser.close();
})();
