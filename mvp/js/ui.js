// Pure rendering layer. Reads state passed in by Game, writes to DOM only.

const UI = {
  screens: {},
  zoomSrc: null,
  pendingValues: null,
  insufficientFundsTimer: null,

  init() {
    ['intro', 'brief', 'auction', 'report', 'end'].forEach((name) => {
      this.screens[name] = document.getElementById('screen-' + name);
    });
  },

  showScreen(name) {
    Object.values(this.screens).forEach((s) => s.classList.remove('active'));
    this.screens[name].classList.add('active');
  },

  // --- Brief screen -------------------------------------------------------

  showBrief(state, cfg) {
    this.showScreen('brief');
    document.getElementById('brief-day-number').textContent = state.day;
    this.updateBriefCapital(state.capital);
    this.refreshBranchChoice(state);
    this.refreshUpgradeShop(state);
  },

  updateBriefCapital(capital) {
    document.getElementById('brief-capital').textContent = formatMoney(capital);
  },

  refreshBranchChoice(state) {
    const el = document.getElementById('brief-branch-choice');
    el.innerHTML = '';
    COLLECTORS.forEach((c) => {
      const missionIndex = state.branchProgress[c.id] || 0;
      const branchCfg = getBranchMissionConfig(missionIndex, c.missions.length);
      const venue = VENUES[branchCfg.venueTier];
      const mastered = missionIndex >= c.missions.length - 1;
      const card = document.createElement('div');
      card.className = 'venue-card' + (state.selectedBranchId === c.id ? ' selected' : '');
      card.innerHTML = `
        <div class="venue-card-name">${c.nameRu}</div>
        <div class="venue-card-desc">${c.taglineRu}</div>
        <div class="venue-card-desc">Заказ №${missionIndex + 1}${mastered ? ' (мастерство)' : ''} · ${venue.labelRu}</div>
      `;
      card.addEventListener('click', () => Game.selectBranch(c.id));
      el.appendChild(card);
    });
  },

  refreshUpgradeShop(state) {
    const el = document.getElementById('brief-upgrades');
    el.innerHTML = '';
    META_UPGRADES.forEach((u) => {
      const owned = state.upgrades.has(u.id);
      const card = document.createElement('div');
      card.className = 'shop-card' + (owned ? ' owned' : '');
      card.innerHTML = `
        <div class="shop-card-name">${u.nameRu}</div>
        <div class="shop-card-desc">${u.descRu}</div>
        <button class="btn btn-secondary" ${owned || state.capital < u.cost ? 'disabled' : ''}>
          ${owned ? 'Приобретено' : `Купить за ${formatMoney(u.cost)} ₽`}
        </button>
      `;
      if (!owned) card.querySelector('button').addEventListener('click', () => Game.buyUpgrade(u.id));
      el.appendChild(card);
    });
  },

  // --- Auction screen ------------------------------------------------------

  showAuctionScreen(state) {
    this.showScreen('auction');
    document.getElementById('hud-day').textContent = state.day;
    document.getElementById('hud-venue').textContent = VENUES[state.currentVenue].labelRu;
    document.getElementById('hud-lot-total').textContent = state.lots.length;
    this.updateCapitalDisplays(state.capital);
    this.renderOrderBrief(state);
  },

  renderOrderBrief(state) {
    const el = document.getElementById('order-brief');
    el.innerHTML = '';
    state.dayOrders.forEach((o) => {
      const card = document.createElement('div');
      card.className = 'order-brief-card';
      card.innerHTML = `
        <div class="order-brief-kicker">Заказчик</div>
        <div class="order-brief-name">${o.nameRu}</div>
        <div class="order-brief-want">${o.criteriaLabel}</div>
        <div class="order-brief-budget">Бюджет ${formatMoney(o.budget)} ₽</div>
      `;
      el.appendChild(card);
    });
    this.highlightOrderFields(state);
  },

  highlightOrderFields(state) {
    document.querySelectorAll('.reveal-field').forEach((field) => field.classList.remove('order-target'));
    const fieldByType = { genre: 'genre', period: 'period', artist: 'artist', artwork: 'title' };
    state.dayOrders.forEach((order) => {
      (order.criteriaTags || []).forEach((tag) => {
        const fieldName = fieldByType[tag.type];
        const field = fieldName && document.getElementById('field-' + fieldName);
        if (field) field.classList.add('order-target');
      });
    });
  },

  updateCapitalDisplays(capital) {
    document.getElementById('hud-capital').textContent = formatMoney(capital);
  },

  flashInsufficientFunds() {
    const hint = document.getElementById('insufficient-funds-hint');
    hint.classList.remove('hidden');
    if (this.insufficientFundsTimer) clearTimeout(this.insufficientFundsTimer);
    this.insufficientFundsTimer = setTimeout(() => hint.classList.add('hidden'), 1200);
  },

  renderLot(lot, index, total, initialPrice, initialMultiplier) {
    document.getElementById('hud-lot-index').textContent = index + 1;
    document.getElementById('hud-lot-total').textContent = total;
    document.getElementById('lot-image').src = lot.imageUrl;
    document.getElementById('familiar-badge').classList.toggle('hidden', !lot.familiar);

    const banner = document.getElementById('lot-result-banner');
    banner.classList.add('hidden');
    banner.className = 'lot-result-banner hidden';

    document.getElementById('btn-buy').disabled = false;
    document.getElementById('btn-skip').disabled = false;
    document.querySelectorAll('.rival-head.raised').forEach((h) => h.classList.remove('raised'));

    this.pendingValues = {
      title: lot.titleRu,
      artist: lot.artistRu + (lot.year ? ` (${lot.year})` : ''),
      period: lot.periodRu,
      genre: lot.genreRu,
      fact: lot.factRu,
    };
    Object.keys(this.pendingValues).forEach((f) => {
      const el = document.getElementById('field-' + f);
      el.classList.remove('revealed');
      el.querySelector('.field-value').textContent = maskValue(this.pendingValues[f]);
    });

    this.updateLiveEconomics(initialPrice, initialMultiplier);
    this.zoomSrc = lot.imageUrl;
    if (typeof Game !== 'undefined' && Game.state) this.highlightOrderFields(Game.state);
  },

  revealField(fieldName) {
    const el = document.getElementById('field-' + fieldName);
    if (!el) return;
    el.classList.add('revealed');
    if (this.pendingValues && this.pendingValues[fieldName] !== undefined) {
      el.querySelector('.field-value').textContent = this.pendingValues[fieldName];
    }
  },

  updateLiveEconomics(price, multiplier) {
    document.getElementById('live-price').textContent = formatMoney(price) + ' ₽';
    document.getElementById('live-multiplier').textContent = '×' + multiplier.toFixed(2);
  },

  showWaitingHint() {
    const banner = document.getElementById('lot-result-banner');
    banner.textContent = 'Конкуренты уже присматриваются к лоту...';
    banner.className = 'lot-result-banner';
    banner.classList.remove('hidden');
  },

  showLotResult(kind) {
    const texts = {
      won: 'Лот ваш!',
      lost: 'Лот ушёл другому покупателю!',
      skipped: 'Лот пропущен.',
    };
    const banner = document.getElementById('lot-result-banner');
    banner.textContent = texts[kind] || '';
    banner.className = 'lot-result-banner ' + kind;
    banner.classList.remove('hidden');
    document.getElementById('btn-buy').disabled = true;
    document.getElementById('btn-skip').disabled = true;
  },

  raiseRandomHand() {
    const heads = document.querySelectorAll('.rival-head');
    if (!heads.length) return;
    const head = heads[Math.floor(Math.random() * heads.length)];
    head.classList.add('raised');
    setTimeout(() => head.classList.remove('raised'), 1200);
  },

  // --- Report screen --------------------------------------------------------

  showReport(state, result) {
    this.showScreen('report');
    document.getElementById('report-day-number').textContent = state.day;

    const ordersEl = document.getElementById('report-orders');
    ordersEl.innerHTML = '';
    result.orderStats.forEach((o) => {
      const sign = o.commissionEarned >= 0 ? '+' : '−';
      const row = document.createElement('div');
      row.className = 'order-report-card';
      row.innerHTML = `
        <div class="order-report-name">${o.nameRu} <span class="order-venue-tag">${VENUES[o.venue].labelRu}</span></div>
        <div class="order-report-line">Бюджет: ${formatMoney(o.budget)} ₽ · Потрачено: ${formatMoney(o.spent)} ₽ · Списано неизрасходованного: ${formatMoney(o.leftover)} ₽</div>
        <div class="order-report-line">Верно: ${o.correctCount} · Неверно: ${o.incorrectCount} · Комиссия: ${sign}${formatMoney(Math.abs(o.commissionEarned))} ₽</div>
      `;
      ordersEl.appendChild(row);
    });

    const txEl = document.getElementById('report-transactions');
    txEl.innerHTML = '';
    if (!result.purchaseDetails.length) {
      txEl.innerHTML = '<div class="transaction-empty">Вы не купили ни одного лота за этот день.</div>';
    } else {
      result.purchaseDetails.forEach((p) => {
        const row = document.createElement('div');
        row.className = 'transaction-row ' + (p.matched ? 'matched' : 'unmatched');
        const sign = p.amount >= 0 ? '+' : '−';
        row.innerHTML = `
          <div class="transaction-main">
            <div class="transaction-title">${p.matched ? '✅' : '❌'} ${p.titleRu}
              <span class="transaction-price">(${formatMoney(p.price)} ₽)</span>
            </div>
            <div class="transaction-reason">${p.reason}</div>
          </div>
          <div class="transaction-amount ${p.amount >= 0 ? 'positive' : 'negative'}">${sign}${formatMoney(
          Math.abs(p.amount)
        )} ₽</div>
        `;
        txEl.appendChild(row);
      });
    }

    const netSign = result.totalCommission >= 0 ? '+' : '−';
    const otherRow =
      result.otherSpend !== 0
        ? `<div class="ledger-row negative"><span>− Билеты на аукционы / переплаты сверх бюджета</span><span>−${formatMoney(Math.abs(result.otherSpend))} ₽</span></div>`
        : '';
    document.getElementById('report-ledger').innerHTML = `
      <div class="ledger-row"><span>Капитал на начало дня</span><span>${formatMoney(result.startingCapital)} ₽</span></div>
      <div class="ledger-row ${result.totalCommission >= 0 ? 'positive' : 'negative'}"><span>Комиссии/штрафы (нетто)</span><span>${netSign}${formatMoney(Math.abs(result.totalCommission))} ₽</span></div>
      <div class="ledger-row negative"><span>− Списано неизрасходованного бюджета заказов</span><span>−${formatMoney(result.totalClawback)} ₽</span></div>
      ${otherRow}
      <div class="ledger-row total"><span>Капитал на конец дня</span><span>${formatMoney(result.projectedCapital)} ₽</span></div>
    `;

    const verdict = document.getElementById('report-verdict');
    const continueBtn = document.getElementById('btn-report-continue');
    const boostersSection = document.getElementById('report-boosters-section');
    if (result.pass) {
      verdict.textContent =
        state.day >= CAMPAIGN_LENGTH
          ? 'День пройден! Кампания завершена.'
          : 'День пройден! Переходим к следующему дню.';
      verdict.className = 'report-verdict pass';
      continueBtn.textContent = 'Продолжить';
      if (state.day < CAMPAIGN_LENGTH) {
        boostersSection.classList.remove('hidden');
        this.refreshBoosterShop(state);
      } else {
        boostersSection.classList.add('hidden');
      }
    } else {
      verdict.textContent = 'БАНКРОТСТВО: капитал ушёл в минус. Карьера окончена.';
      verdict.className = 'report-verdict fail';
      continueBtn.textContent = 'Закончить';
      boostersSection.classList.add('hidden');
    }
  },

  updateReportCapital(capital) {
    const row = document.querySelector('#report-ledger .ledger-row.total span:last-child');
    if (row) row.textContent = formatMoney(capital) + ' ₽';
  },

  refreshBoosterShop(state) {
    const el = document.getElementById('report-boosters');
    el.innerHTML = '';
    BOOSTERS.forEach((b) => {
      const cost = b.cost(state.day + 1);
      const owned = state.pendingBoosters.has(b.id);
      const card = document.createElement('div');
      card.className = 'shop-card' + (owned ? ' owned' : '');
      card.innerHTML = `
        <div class="shop-card-name">${b.nameRu}</div>
        <div class="shop-card-desc">${b.descRu}</div>
        <button class="btn btn-secondary" ${owned || state.capital < cost ? 'disabled' : ''}>
          ${owned ? 'Куплено на завтра' : `Купить за ${formatMoney(cost)} ₽`}
        </button>
      `;
      if (!owned) card.querySelector('button').addEventListener('click', () => Game.buyBooster(b.id));
      el.appendChild(card);
    });
  },

  // --- End screens --------------------------------------------------------

  showCampaignEnd(state) {
    this.showScreen('end');
    document.getElementById('end-title').textContent = 'Карьера завершена!';
    document.getElementById('end-message').textContent =
      `Вы прошли все ${CAMPAIGN_LENGTH} дней аукциона с капиталом ${formatMoney(state.capital)} ₽. ` +
      'Вы — признанный эксперт арт-рынка.';
  },

  showGameOver(state, result) {
    this.showScreen('end');
    document.getElementById('end-title').textContent = 'Банкротство';
    document.getElementById('end-message').textContent =
      `На дне ${state.day} ваш капитал ушёл в минус (${formatMoney(result.projectedCapital)} ₽). ` +
      'Карьера скупщика окончена.';
  },

  openZoom() {
    if (!this.zoomSrc) return;
    document.getElementById('zoom-image').src = this.zoomSrc;
    document.getElementById('zoom-modal').classList.remove('hidden');
  },

  closeZoom() {
    document.getElementById('zoom-modal').classList.add('hidden');
  },
};
