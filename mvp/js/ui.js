// Pure rendering layer. Reads state passed in by Game, writes to DOM only.

const UI = {
  screens: {},
  zoomSrc: null,
  pendingValues: null,
  insufficientFundsTimer: null,
  purchaseCardDismiss: null,
  purchaseCardDefaultContinueLabel: 'Продолжить торги',
  purchaseCardDefaultPauseHint:
    'Торги на паузе — сверните визитку, когда будете готовы к следующему лоту.',
  _zoomCloseTimer: null,

  RARITY_LABELS: { common: 'Обычная', rare: 'Редкая', epic: 'Эпическая' },

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
    this.updateBriefPreload(ImageCache.progress());
  },

  updateBriefPreload({ loaded, total, ready }) {
    const btn = document.getElementById('btn-start-day');
    const status = document.getElementById('brief-preload-status');
    if (ready || total === 0) {
      btn.disabled = false;
      btn.textContent = 'Выйти в зал';
      status.classList.add('hidden');
      return;
    }
    btn.disabled = true;
    btn.textContent = `Загрузка экспонатов (${loaded}/${total})…`;
    status.textContent = `Подготовка зала: ${loaded} из ${total} картин`;
    status.classList.remove('hidden');
  },

  updateBriefCapital(capital) {
    document.getElementById('brief-capital').textContent = formatMoney(capital);
  },

  formatOrdersRemaining(n) {
    if (n === 1) return 'Остался 1 заказ';
    const mod10 = n % 10;
    const mod100 = n % 100;
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return `Осталось ${n} заказа`;
    return `Осталось ${n} заказов`;
  },

  buildCollectorProgressMarkup(missionIndex) {
    const { total, completed, remaining, mastered, currentOrder } = getCollectorBranchProgress(missionIndex);
    const segments = [];
    for (let i = 0; i < total; i++) {
      const phase = getOrderPhaseForMission(i);
      let cls = 'collector-progress-segment phase-' + phase;
      if (i < completed) cls += ' done';
      else if (i === missionIndex && !mastered) cls += ' current';
      segments.push(`<div class="${cls}" title="Заказ ${i + 1}"></div>`);
    }
    const labelRight = mastered ? 'Мастерство' : this.formatOrdersRemaining(remaining);
    return `
      <div class="collector-progress">
        <div class="collector-progress-label">
          <span>Заказ ${currentOrder} из ${total}</span>
          <span>${labelRight}</span>
        </div>
        <div class="collector-progress-track" role="progressbar" aria-valuenow="${completed}" aria-valuemin="0" aria-valuemax="${total}" aria-label="Прогресс заказчика">
          ${segments.join('')}
        </div>
      </div>
    `;
  },

  refreshBranchChoice(state) {
    const el = document.getElementById('brief-branch-choice');
    el.innerHTML = '';
    COLLECTORS.forEach((c) => {
      const missionIndex = state.branchProgress[c.id] || 0;
      const branchCfg = getBranchMissionConfig(missionIndex, ORDER_LADDER_LENGTH);
      const venue = VENUES[branchCfg.venueTier];
      const card = document.createElement('div');
      card.className = 'venue-card' + (state.selectedBranchId === c.id ? ' selected' : '');
      const portraitHtml = c.portraitUrl
        ? `<div class="venue-card-portrait"><img src="${c.portraitUrl}" alt="${c.nameRu}"></div>`
        : '';
      card.innerHTML = `
        ${portraitHtml}
        <div class="venue-card-body">
          <div class="venue-card-name">${c.nameRu}</div>
          <div class="venue-card-desc">${c.taglineRu}</div>
          ${this.buildCollectorProgressMarkup(missionIndex)}
          <div class="venue-card-desc venue-card-venue">${venue.labelRu}</div>
        </div>
      `;
      card.addEventListener('click', () => {
        Sound.playSelect();
        Game.selectBranch(c.id);
      });
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
        <div class="shop-card-name"><span class="shop-card-icon">${u.icon}</span>${u.nameRu}</div>
        <div class="shop-card-desc">${u.descRu}</div>
        <button class="btn btn-secondary" ${owned || state.capital < u.cost ? 'disabled' : ''}>
          ${owned ? 'Приобретено' : `Купить за ${formatMoney(u.cost)} ₽`}
        </button>
      `;
      if (!owned)
        card.querySelector('button').addEventListener('click', () => {
          Sound.playUpgrade();
          Game.buyUpgrade(u.id);
        });
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
    this.renderActiveBoosters(state);
    this.renderActiveUpgrades(state);
  },

  renderEffectIcons(elId, items) {
    const el = document.getElementById(elId);
    if (!el) return;
    el.classList.toggle('hidden', items.length === 0);
    el.innerHTML = items
      .map(
        (item) => `
        <span class="effect-icon" tabindex="0">
          ${item.icon}
          <span class="effect-tooltip"><strong>${item.nameRu}</strong><br>${item.descRu}</span>
        </span>
      `
      )
      .join('');
  },

  renderActiveBoosters(state) {
    this.renderEffectIcons(
      'hud-active-boosters',
      BOOSTERS.filter((b) => state.activeBoosters.has(b.id))
    );
  },

  renderActiveUpgrades(state) {
    this.renderEffectIcons(
      'hud-active-upgrades',
      META_UPGRADES.filter((u) => state.upgrades.has(u.id))
    );
  },

  buildOrderTagsMarkup(order) {
    const labels = { period: 'Период', genre: 'Жанр', artist: 'Автор', artwork: 'Работа' };
    const tags = (order.criteriaTags || []).map((tag) => {
      let value = tag.value;
      if (tag.type === 'artwork') {
        const art = ARTWORKS.find((a) => a.id === tag.value);
        value = art ? `«${art.titleRu}»` : tag.value;
      }
      return {
        type: tag.type,
        label: labels[tag.type] || tag.type,
        value,
      };
    });
    if (!tags.length) return '';
    return `
      <div class="order-brief-tags">
        ${tags
          .map(
            (t) =>
              `<span class="order-tag order-tag-${t.type}"><span class="order-tag-label">${t.label}</span>${t.value}</span>`
          )
          .join('')}
      </div>
    `;
  },

  renderOrderBrief(state) {
    const el = document.getElementById('order-brief');
    el.innerHTML = '';
    state.dayOrders.forEach((o) => {
      const card = document.createElement('div');
      card.className = 'order-brief-card';
      const portraitHtml = o.portraitUrl
        ? `<div class="order-brief-portrait"><img src="${o.portraitUrl}" alt="${o.nameRu}"></div>`
        : '';
      card.innerHTML = `
        ${portraitHtml}
        <div class="order-brief-content">
          <div class="order-brief-name">${o.nameRu}</div>
          ${this.buildOrderTagsMarkup(o)}
        </div>
      `;
      el.appendChild(card);
    });
    this.highlightOrderFields(state);
  },

  highlightOrderFields(state) {
    document.querySelectorAll('.reveal-field').forEach((field) => field.classList.remove('order-target'));
    const fieldByType = { genre: 'genre', period: 'period', artist: 'artist', artwork: 'title' };
    (state.dayOrders || []).forEach((order) => {
      (order.criteriaTags || []).forEach((tag) => {
        const fieldName = fieldByType[tag.type];
        const field = fieldName && document.getElementById('field-' + fieldName);
        if (field) field.classList.add('order-target');
      });
    });
  },

  updateCapitalDisplays(capital) {
    document.getElementById('hud-capital').textContent = formatMoney(capital);
    const live = document.getElementById('live-capital');
    if (live) live.textContent = formatMoney(capital) + ' ₽';
  },

  flashInsufficientFunds() {
    Sound.playError();
    const hint = document.getElementById('insufficient-funds-hint');
    hint.classList.remove('hidden');
    if (this.insufficientFundsTimer) clearTimeout(this.insufficientFundsTimer);
    this.insufficientFundsTimer = setTimeout(() => hint.classList.add('hidden'), 1200);
  },

  renderLot(lot, index, total, initialPrice) {
    document.getElementById('hud-lot-index').textContent = index + 1;
    document.getElementById('hud-lot-total').textContent = total;
    document.getElementById('lot-image').src = lot.imageUrl;
    document.getElementById('familiar-badge').classList.toggle('hidden', !lot.familiar);

    const banner = document.getElementById('lot-result-banner');
    banner.classList.add('hidden');
    banner.className = 'lot-result-banner hidden';

    this.clearLotOutcomeFx();

    document.getElementById('btn-buy').disabled = false;
    document.getElementById('btn-skip').disabled = false;
    document.getElementById('btn-finish-day').disabled = false;
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

    this.updateLiveEconomics(initialPrice, { animate: false });
    if (typeof Game !== 'undefined' && Game.state) {
      this.updateCapitalDisplays(Game.state.capital);
    }
    this.zoomSrc = lot.imageUrl;
    if (typeof Game !== 'undefined' && Game.state) this.highlightOrderFields(Game.state);
  },

  clearLotOutcomeFx() {
    const wrap = document.getElementById('lot-image-wrap');
    const fx = document.getElementById('lot-fx');
    const img = document.getElementById('lot-image');
    wrap.classList.remove('fx-won', 'fx-lost');
    fx.className = 'lot-fx';
    fx.textContent = '';
    img.style.filter = '';
    img.style.transform = '';
  },

  playLotOutcomeFx(kind) {
    const wrap = document.getElementById('lot-image-wrap');
    const fx = document.getElementById('lot-fx');
    this.clearLotOutcomeFx();
    void wrap.offsetWidth;

    if (kind === 'won') {
      wrap.classList.add('fx-won');
      fx.className = 'lot-fx lot-fx-won';
      fx.textContent = 'ВАШ!';
    } else if (kind === 'lost') {
      wrap.classList.add('fx-lost');
      fx.className = 'lot-fx lot-fx-lost';
      fx.textContent = 'ПРОДАН';
    }
  },

  // Black fade-out → swapFn (render / finish) → fade-in. Returns a promise that
  // resolves after the fade-out completes and swapFn has run (fade-in still running).
  withLotFade(swapFn) {
    const fade = document.getElementById('lot-fade');
    const FADE_MS = 340;
    if (this._lotFadeTimer) clearTimeout(this._lotFadeTimer);
    fade.classList.add('on');
    return new Promise((resolve) => {
      this._lotFadeTimer = setTimeout(() => {
        this._lotFadeTimer = null;
        swapFn();
        requestAnimationFrame(() => {
          requestAnimationFrame(() => fade.classList.remove('on'));
        });
        resolve();
      }, FADE_MS);
    });
  },

  // First lot of the day: start blacked out, then reveal.
  revealLotFromBlack() {
    const fade = document.getElementById('lot-fade');
    fade.classList.add('on');
    requestAnimationFrame(() => {
      requestAnimationFrame(() => fade.classList.remove('on'));
    });
  },

  revealField(fieldName) {
    const el = document.getElementById('field-' + fieldName);
    if (!el) return;
    el.classList.add('revealed');
    if (this.pendingValues && this.pendingValues[fieldName] !== undefined) {
      el.querySelector('.field-value').textContent = this.pendingValues[fieldName];
    }
  },

  updateLiveEconomics(price, { animate = true } = {}) {
    const priceEl = document.getElementById('live-price');
    const fromPrice = this._livePrice ?? price;
    this._livePrice = price;

    if (!animate || fromPrice === price) {
      if (this._econAnim) {
        cancelAnimationFrame(this._econAnim);
        this._econAnim = null;
      }
      priceEl.textContent = formatMoney(price) + ' ₽';
      return;
    }

    this._animatePrice(priceEl, fromPrice, price);
  },

  _animatePrice(priceEl, fromPrice, toPrice) {
    if (this._econAnim) cancelAnimationFrame(this._econAnim);

    priceEl.classList.remove('econ-flash-up', 'econ-flash-down');
    void priceEl.offsetWidth;
    priceEl.classList.add(toPrice > fromPrice ? 'econ-flash-up' : 'econ-flash-down');

    const duration = 480;
    const start = performance.now();
    const easeOut = (t) => 1 - Math.pow(1 - t, 3);

    const tick = (now) => {
      const t = Math.min(1, (now - start) / duration);
      const e = easeOut(t);
      const p = Math.round(fromPrice + (toPrice - fromPrice) * e);
      priceEl.textContent = formatMoney(p) + ' ₽';
      if (t < 1) this._econAnim = requestAnimationFrame(tick);
      else this._econAnim = null;
    };
    this._econAnim = requestAnimationFrame(tick);
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
    document.getElementById('btn-finish-day').disabled = true;
    this.playLotOutcomeFx(kind);
    Sound.playOutcome(kind);
  },

  raiseRandomHand() {
    Sound.playRivalRaise();
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
      const status = o.fulfilled ? 'Заказ выполнен' : 'Заказ не выполнен';
      const row = document.createElement('div');
      row.className = 'order-report-card' + (o.fulfilled ? '' : ' unfulfilled');
      const portraitHtml = o.portraitUrl
        ? `<div class="order-report-portrait"><img src="${o.portraitUrl}" alt="${o.nameRu}"></div>`
        : '';
      row.innerHTML = `
        ${portraitHtml}
        <div class="order-report-body">
          <div class="order-report-name">${o.nameRu} <span class="order-venue-tag">${VENUES[o.venue].labelRu}</span>
            <span class="order-fulfill-tag ${o.fulfilled ? 'ok' : 'bad'}">${status}</span>
          </div>
          <div class="order-report-line">Бюджет: ${formatMoney(o.budget)} ₽ · Потрачено: ${formatMoney(o.spent)} ₽ · Списано неизрасходованного: ${formatMoney(o.leftover)} ₽</div>
          <div class="order-report-line">Верно: ${o.correctCount} · Неверно: ${o.incorrectCount} · Комиссия: ${sign}${formatMoney(Math.abs(o.commissionEarned))} ₽</div>
          ${
            o.fulfilled
              ? ''
              : '<div class="order-report-line order-unfulfilled-hint">Нужна хотя бы одна подходящая картина — иначе заказ остаётся открытым.</div>'
          }
        </div>
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
        row.className = 'transaction-row transaction-row-clickable ' + (p.matched ? 'matched' : 'unmatched');
        row.setAttribute('role', 'button');
        row.tabIndex = 0;
        const sign = p.amount >= 0 ? '+' : '−';
        row.innerHTML = `
          <div class="transaction-main">
            <div class="transaction-title">${p.matched ? '✅' : '❌'} ${p.titleRu}
              <span class="transaction-price">(${formatMoney(p.price)} ₽)</span>
            </div>
            <div class="transaction-reason">${p.reason}</div>
            <div class="transaction-view-card-hint">Нажмите, чтобы открыть визиточку</div>
          </div>
          <div class="transaction-amount ${p.amount >= 0 ? 'positive' : 'negative'}">${sign}${formatMoney(
          Math.abs(p.amount)
        )} ₽</div>
        `;
        row.addEventListener('click', () => {
          const artwork = ARTWORKS.find((a) => a.id === p.artworkId);
          if (artwork) this.showPurchaseCard(artwork, p.price, { review: true });
        });
        row.addEventListener('keydown', (e) => {
          if (e.code === 'Enter' || e.code === 'Space') {
            e.preventDefault();
            row.click();
          }
        });
        txEl.appendChild(row);
      });
    }

    const netSign = result.totalCommission >= 0 ? '+' : '−';
    const otherRow =
      result.otherSpend !== 0
        ? `<div class="ledger-row negative"><span>− Билеты на аукционы / переплаты сверх бюджета</span><span>−${formatMoney(Math.abs(result.otherSpend))} ₽</span></div>`
        : '';
    const creditLineRow = result.savedByCreditLine
      ? `<div class="ledger-row positive"><span>+ Кредитная линия (разово за карьеру)</span><span>+${formatMoney(result.creditLineCoverage)} ₽</span></div>`
      : '';
    document.getElementById('report-ledger').innerHTML = `
      <div class="ledger-row"><span>Капитал на начало дня</span><span>${formatMoney(result.startingCapital)} ₽</span></div>
      <div class="ledger-row ${result.totalCommission >= 0 ? 'positive' : 'negative'}"><span>Комиссии/штрафы (нетто)</span><span>${netSign}${formatMoney(Math.abs(result.totalCommission))} ₽</span></div>
      <div class="ledger-row negative"><span>− Списано неизрасходованного бюджета заказов</span><span>−${formatMoney(result.totalClawback)} ₽</span></div>
      ${otherRow}
      ${creditLineRow}
      <div class="ledger-row total"><span>Капитал на конец дня</span><span>${formatMoney(result.projectedCapital)} ₽</span></div>
    `;

    const verdict = document.getElementById('report-verdict');
    const continueBtn = document.getElementById('btn-report-continue');
    const boostersSection = document.getElementById('report-boosters-section');
    if (result.pass) {
      Sound.playDayPass(result.ordersFulfilled);
      if (result.savedByCreditLine) {
        verdict.textContent =
          `Капитал ушёл в минус, но кредитная линия покрыла разницу (${formatMoney(result.creditLineCoverage)} ₽) — использована один раз за карьеру.`;
        verdict.className = 'report-verdict warn';
      } else if (!result.ordersFulfilled) {
        verdict.textContent =
          'День пережит, но заказ не закрыт: нужна хотя бы одна подходящая картина. Заказ остаётся открытым.';
        verdict.className = 'report-verdict warn';
      } else if (state.day >= CAMPAIGN_LENGTH) {
        verdict.textContent = 'День пройден! Кампания завершена.';
        verdict.className = 'report-verdict pass';
      } else {
        verdict.textContent = 'День пройден! Переходим к следующему дню.';
        verdict.className = 'report-verdict pass';
      }
      continueBtn.textContent = 'Продолжить';
      if (state.day < CAMPAIGN_LENGTH) {
        boostersSection.classList.remove('hidden');
        this.refreshBoosterShop(state);
      } else {
        boostersSection.classList.add('hidden');
      }
    } else {
      Sound.playDayFail();
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
    const offers = state.boosterOffers.map((id) => BOOSTERS.find((b) => b.id === id)).filter(Boolean);

    const heading = document.getElementById('report-boosters-heading');
    if (heading) heading.textContent = `Бустеры на завтра (${state.pendingBoosters.size}/${offers.length})`;

    const el = document.getElementById('report-boosters');
    el.innerHTML = '';
    offers.forEach((b) => {
      const cost = getBoosterCost(b, state);
      const owned = state.pendingBoosters.has(b.id);
      const disabled = owned || state.capital < cost;
      const card = document.createElement('div');
      card.className = 'shop-card' + (owned ? ' owned' : '');
      card.innerHTML = `
        <div class="shop-card-name"><span class="shop-card-icon">${b.icon}</span>${b.nameRu}</div>
        <div class="shop-card-desc">${b.descRu}</div>
        <button class="btn btn-secondary" ${disabled ? 'disabled' : ''}>
          ${owned ? 'Куплено на завтра' : `Купить за ${formatMoney(cost)} ₽`}
        </button>
      `;
      if (!owned)
        card.querySelector('button').addEventListener('click', () => {
          Sound.playUpgrade();
          Game.buyBooster(b.id);
        });
      el.appendChild(card);
    });
  },

  // --- End screens --------------------------------------------------------

  showCampaignEnd(state) {
    Sound.playCampaignEnd();
    this.showScreen('end');
    document.getElementById('end-title').textContent = 'Карьера завершена!';
    document.getElementById('end-message').textContent =
      `Вы прошли все ${CAMPAIGN_LENGTH} дней аукциона с капиталом ${formatMoney(state.capital)} ₽. ` +
      'Вы — признанный эксперт арт-рынка.';
  },

  showGameOver(state, result) {
    Sound.playDayFail();
    this.showScreen('end');
    document.getElementById('end-title').textContent = 'Банкротство';
    document.getElementById('end-message').textContent =
      `На дне ${state.day} ваш капитал ушёл в минус (${formatMoney(result.projectedCapital)} ₽). ` +
      'Карьера скупщика окончена.';
  },

  revealAllLotFields() {
    REVEALABLE_FIELDS.forEach((f) => this.revealField(f));
  },

  isPurchaseCardOpen() {
    const el = document.getElementById('purchase-card-overlay');
    return el && !el.classList.contains('hidden');
  },

  showPurchaseCard(lot, price, { onDismiss = null, review = false } = {}) {
    Sound.playCardOpen();
    this.purchaseCardDismiss = onDismiss;

    if (!review) this.revealAllLotFields();

    const hintEl = document.getElementById('purchase-card-pause-hint');
    const continueBtn = document.getElementById('btn-purchase-card-continue');
    const overlayEl = document.getElementById('purchase-card-overlay');
    if (!overlayEl || !continueBtn) {
      console.error('Purchase card markup is missing from index.html');
      if (onDismiss) onDismiss();
      return;
    }

    if (hintEl) {
      hintEl.textContent = review
        ? 'Визиточка купленного лота. Закройте, чтобы вернуться к итогам дня.'
        : this.purchaseCardDefaultPauseHint;
    }
    continueBtn.textContent = review ? 'Закрыть' : this.purchaseCardDefaultContinueLabel;

    document.getElementById('purchase-card-image').src = lot.imageUrl;
    document.getElementById('purchase-card-image').alt = lot.titleRu;
    document.getElementById('purchase-card-title').textContent = lot.titleRu;
    document.getElementById('purchase-card-artist').textContent =
      lot.artistRu + (lot.year ? ` (${lot.year})` : '');
    document.getElementById('purchase-card-period').textContent = lot.periodRu;
    document.getElementById('purchase-card-genre').textContent = lot.genreRu;
    document.getElementById('purchase-card-rarity').textContent =
      this.RARITY_LABELS[lot.rarity] || lot.rarity;
    document.getElementById('purchase-card-price').textContent = formatMoney(price) + ' ₽';
    document.getElementById('purchase-card-fact').textContent = lot.factRu;

    overlayEl.classList.remove('hidden');
    continueBtn.focus();
  },

  closePurchaseCard() {
    if (!this.isPurchaseCardOpen()) return;
    Sound.playCardClose();
    document.getElementById('purchase-card-overlay').classList.add('hidden');
    const hintEl = document.getElementById('purchase-card-pause-hint');
    if (hintEl) hintEl.textContent = this.purchaseCardDefaultPauseHint;
    document.getElementById('btn-purchase-card-continue').textContent = this.purchaseCardDefaultContinueLabel;
    const dismiss = this.purchaseCardDismiss;
    this.purchaseCardDismiss = null;
    if (dismiss) dismiss();
  },

  isZoomOpen() {
    const modal = document.getElementById('zoom-modal');
    return modal && modal.classList.contains('open');
  },

  openZoom(src, alt) {
    const url = src || this.zoomSrc;
    if (!url) return;

    const modal = document.getElementById('zoom-modal');
    const img = document.getElementById('zoom-image');
    if (!modal || !img) return;

    Sound.playZoomOpen();
    if (this._zoomCloseTimer) {
      clearTimeout(this._zoomCloseTimer);
      this._zoomCloseTimer = null;
    }

    img.src = url;
    if (alt) img.alt = alt;

    modal.classList.remove('hidden');
    modal.setAttribute('aria-hidden', 'false');
    requestAnimationFrame(() => {
      requestAnimationFrame(() => modal.classList.add('open'));
    });
  },

  closeZoom() {
    const modal = document.getElementById('zoom-modal');
    if (!modal || !modal.classList.contains('open')) return;

    Sound.playZoomClose();
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');

    if (this._zoomCloseTimer) clearTimeout(this._zoomCloseTimer);
    this._zoomCloseTimer = setTimeout(() => {
      modal.classList.add('hidden');
      this._zoomCloseTimer = null;
    }, 400);
  },

  openPurchaseCardZoom() {
    const img = document.getElementById('purchase-card-image');
    if (!img || !img.src) return;
    this.openZoom(img.src, img.alt);
  },
};
