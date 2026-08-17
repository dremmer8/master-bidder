const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const errors = [];
  const failedImages = [];
  page.on('pageerror', (e) => errors.push('PAGEERROR: ' + e.message));
  page.on('console', (msg) => {
    if (msg.type() === 'error') errors.push('CONSOLE: ' + msg.text());
  });
  page.on('requestfailed', (req) => {
    if (req.resourceType() === 'image') failedImages.push(req.url() + ' :: ' + (req.failure() || {}).errorText);
  });

  await page.goto('http://localhost:8934/index.html');
  await page.click('#btn-start-campaign');
  await page.waitForSelector('#screen-brief.active');
  await page.click('#btn-start-day');
  await page.waitForSelector('#screen-auction.active');

  let lotCount = 0;
  while (true) {
    const auctionActive = await page.$('#screen-auction.active');
    if (!auctionActive) break;
    lotCount++;

    // Wait for genre + period fields to reveal (2nd/3rd fields ~ 3000-4500ms)
    await page.waitForTimeout(4700);

    const stillHere = await page.$('#screen-auction.active:not(.hidden)');
    const resolved = await page.$('#lot-result-banner:not(.hidden)');
    if (resolved) {
      // rival already won it before we could act (shouldn't happen this fast normally)
      await page.waitForTimeout(1600);
      continue;
    }

    const info = await page.evaluate(() => {
      const genre = document.querySelector('#field-genre .field-value').textContent;
      const period = document.querySelector('#field-period .field-value').textContent;
      const orders = Array.from(document.querySelectorAll('.field-order-hint, .order-brief-card')).map((c) => c.textContent);
      return { genre, period, orders };
    });

    const isMatch = info.orders.some((o) => o.includes(info.genre) || o.includes(info.period));

    if (isMatch) {
      const buyBtn = await page.$('#btn-buy:not([disabled])');
      if (buyBtn) await buyBtn.click();
    }

    await page.waitForTimeout(2200); // let resolution + transition to next lot happen
  }

  await page.waitForSelector('#screen-report.active', { timeout: 20000 });
  const reportText = await page.$eval('#screen-report', (el) => el.innerText);
  console.log('LOTS SEEN:', lotCount);
  console.log('--- REPORT ---');
  console.log(reportText);
  console.log('--- FAILED IMAGES ---');
  console.log(failedImages.length ? failedImages.join('\n') : 'none');
  console.log('--- ERRORS ---');
  console.log(errors.length ? errors.join('\n') : 'none');

  await page.screenshot({ path: 'shots/6-smart-report.png' });
  await browser.close();
})();
