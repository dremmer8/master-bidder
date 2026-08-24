// Pure rendering layer. Reads state passed in by Game, writes to DOM only.

function uiCollectorName(collectorOrOrder) {
  const id = collectorOrOrder.id || collectorOrOrder.collectorId;
  return I18n.entity('collectors', id, 'name', collectorOrOrder.nameRu);
}

function uiCollectorTagline(collector) {
  return I18n.entity('collectors', collector.id, 'tagline', collector.taglineRu);
}

function uiVenueLabel(venueKey) {
  const venue = VENUES[venueKey];
  return I18n.entity('venues', venueKey, 'label', venue.labelRu);
}

function uiUpgradeName(upgrade) {
  return I18n.entity('upgrades', upgrade.id, 'name', upgrade.nameRu);
}

function uiUpgradeDesc(upgrade) {
  return I18n.entity('upgrades', upgrade.id, 'desc', upgrade.descRu);
}

function uiBoosterName(booster) {
  return I18n.entity('boosters', booster.id, 'name', booster.nameRu);
}

function uiBoosterDesc(booster) {
  return I18n.entity('boosters', booster.id, 'desc', booster.descRu);
}

function uiArtworkFields(artwork) {
  return {
    title: I18n.artwork(artwork, 'title'),
    artist: I18n.artwork(artwork, 'artist') + (artwork.year ? ` (${artwork.year})` : ''),
    period: I18n.vocab(artwork.periodRu),
    genre: I18n.vocab(artwork.genreRu),
    fact: I18n.artwork(artwork, 'fact'),
  };
}

const UI = {
  screens: {},
  zoomSrc: null,
  pendingValues: null,
  insufficientFundsTimer: null,
  purchaseCardDismiss: null,
  _zoomCloseTimer: null,
  _lastBriefState: null,
  _lastBriefCfg: null,
  _lastReportState: null,
  _lastReportResult: null,

  init() {
    ['intro', 'brief', 'auction', 'report', 'end'].forEach((name) => {
      this.screens[name] = document.getElementById('screen-' + name);
    });
    this.refreshIntroContinue();
  },

  refreshIntroContinue() {
    const continueBtn = document.getElementById('btn-continue-campaign');
    const startBtn = document.getElementById('btn-start-campaign');
    if (!continueBtn || !startBtn) return;
    const hasSave = typeof SaveGame !== 'undefined' && SaveGame.hasSave();
    continueBtn.classList.toggle('hidden', !hasSave);
    startBtn.classList.toggle('btn-primary', !hasSave);
    startBtn.classList.toggle('btn-secondary', hasSave);
    startBtn.textContent = I18n.t(hasSave ? 'intro.newCareer' : 'intro.start');
  },

  onLocaleChange() {
    this.refreshIntroContinue();
    if (this._lastBriefState) {
      this.showBrief(this._lastBriefState, this._lastBriefCfg);
      this.updateBriefPreload(ImageCache.progress());
    }
    if (this._lastReportState && this._lastReportResult) {
      this.showReport(this._lastReportState, this._lastReportResult);
    }
    const auctionActive = this.screens.auction?.classList.contains('active');
    if (auctionActive && typeof Game !== 'undefined' && Game.state) {
      this.showAuctionScreen(Game.state);
      if (Game.state.awaitingLotStart) this.showLotStandby(Game.state);
      else if (Game.state.lots[Game.state.currentLotIndex]) {
        const lot = Game.state.lots[Game.state.currentLotIndex];
        const fields = uiArtworkFields(lot);
        this.pendingValues = fields;
        Object.keys(fields).forEach((f) => {
          const el = document.getElementById('field-' + f);
          if (el && el.classList.contains('revealed')) {
            el.querySelector('.field-value').textContent = fields[f];
          }
        });
      }
    }
  },

  showScreen(name) {
    Object.values(this.screens).forEach((s) => s.classList.remove('active'));
    this.screens[name].classList.add('active');
  },

  // --- Brief screen -------------------------------------------------------

  showBrief(state, cfg) {
    this._lastBriefState = state;
    this._lastBriefCfg = cfg;
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
      btn.textContent = I18n.t('brief.enterHall');
      status.classList.add('hidden');
      return;
    }
    btn.disabled = true;
    btn.textContent = I18n.t('brief.preloadBtn', { loaded, total });
    status.textContent = I18n.t('brief.preloadStatus', { loaded, total });
    status.classList.remove('hidden');
  },

  updateBriefCapital(capital) {
    document.getElementById('brief-capital').textContent = formatMoney(capital);
  },

  formatOrdersRemaining(n) {
    return I18n.ordersRemaining(n);
  },

  buildCollectorProgressMarkup(missionIndex, collector) {
    const { total, completed, remaining, mastered, currentOrder } = getCollectorBranchProgress(
      missionIndex,
      collector
    );
    const segments = [];
    for (let i = 0; i < total; i++) {
      const phase = getOrderPhaseForMission(i, collector);
      let cls = 'collector-progress-segment phase-' + phase;
      if (i < completed) cls += ' done';
      else if (i === missionIndex && !mastered) cls += ' current';
      segments.push(`<div class="${cls}" title="${I18n.t('collector.orderTitle', { n: i + 1 })}"></div>`);
    }
    const labelRight = mastered ? I18n.t('collector.mastery') : this.formatOrdersRemaining(remaining);
    return `
      <div class="collector-progress">
        <div class="collector-progress-label">
          <span>${I18n.t('collector.orderOf', { current: currentOrder, total })}</span>
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
      const { mastered } = getCollectorBranchProgress(missionIndex, c);
      const branchCfg = getBranchMissionConfig(missionIndex, getCollectorLadderLength(c));
      const orderTagsHtml = mastered
        ? ''
        : this.buildOrderTagsMarkup(getOrderTagsForMission(missionIndex, c));
      const card = document.createElement('div');
      card.className = 'venue-card' + (state.selectedBranchId === c.id ? ' selected' : '');
      const portraitHtml = c.portraitUrl
        ? `<div class="venue-card-portrait"><img src="${c.portraitUrl}" alt="${uiCollectorName(c)}"></div>`
        : '';
      card.innerHTML = `
        ${portraitHtml}
        <div class="venue-card-body">
          <div class="venue-card-name">${uiCollectorName(c)}</div>
          <div class="venue-card-desc">${uiCollectorTagline(c)}</div>
          ${this.buildCollectorProgressMarkup(missionIndex, c)}
          ${orderTagsHtml}
          <div class="venue-card-desc venue-card-venue">${uiVenueLabel(branchCfg.venueTier)}</div>
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
        <div class="shop-card-name"><span class="shop-card-icon">${u.icon}</span>${uiUpgradeName(u)}</div>
        <div class="shop-card-desc">${uiUpgradeDesc(u)}</div>
        <button class="btn btn-secondary" ${owned || state.capital < u.cost ? 'disabled' : ''}>
          ${owned ? I18n.t('shop.owned') : I18n.t('shop.buyFor', { price: `${formatMoney(u.cost)} ${I18n.currencySymbol()}` })}
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
    document.getElementById('hud-venue').textContent = uiVenueLabel(state.currentVenue);
    document.getElementById('hud-lot-total').textContent = state.lots.length;
    this.updateClientBudgetDisplays(getClientBudgetRemaining(state));
    this.renderOrderBrief(state);
    this.renderActiveBoosters(state);
    this.renderActiveUpgrades(state);
  },

  showLotStandby(state) {
    document.getElementById('hud-lot-index').textContent = state.lots.length ? '1' : '—';
    document.getElementById('lot-fade').classList.add('on');
    document.getElementById('lot-image-wrap').classList.add('standby');
    document.getElementById('lot-image').removeAttribute('src');
    document.getElementById('familiar-badge').classList.add('hidden');
    document.getElementById('lot-result-banner').className = 'lot-result-banner hidden';
    this.clearLotOutcomeFx();

    document.querySelectorAll('.reveal-field').forEach((el) => {
      el.classList.remove('revealed', 'order-target');
      el.querySelector('.field-value').textContent = '—';
    });
    document.getElementById('live-price').textContent = '—';
    if (typeof Game !== 'undefined' && Game.state) {
      this.updateClientBudgetDisplays(getClientBudgetRemaining(Game.state));
    }
    this.highlightOrderFields(state);

    document.getElementById('btn-start-lot').classList.remove('hidden');
    document.getElementById('btn-buy').classList.add('hidden');
    document.getElementById('btn-skip').classList.add('hidden');
    document.getElementById('btn-buy').disabled = true;
    document.getElementById('btn-skip').disabled = true;
    document.getElementById('btn-finish-day').disabled = false;
    document.getElementById('zoom-hint').textContent = I18n.t('auction.zoomHintStandby');
  },

  hideLotStandby() {
    document.getElementById('btn-start-lot').classList.add('hidden');
    document.getElementById('btn-buy').classList.remove('hidden');
    document.getElementById('btn-skip').classList.remove('hidden');
    document.getElementById('lot-image-wrap').classList.remove('standby');
    document.getElementById('zoom-hint').textContent = I18n.t('auction.zoomHint');
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
          <span class="effect-tooltip"><strong>${item.type === 'upgrade' ? uiUpgradeName(item) : uiBoosterName(item)}</strong><br>${item.type === 'upgrade' ? uiUpgradeDesc(item) : uiBoosterDesc(item)}</span>
        </span>
      `
      )
      .join('');
  },

  renderActiveBoosters(state) {
    this.renderEffectIcons(
      'hud-active-boosters',
      BOOSTERS.filter((b) => state.activeBoosters.has(b.id)).map((b) => ({ ...b, type: 'booster' }))
    );
  },

  renderActiveUpgrades(state) {
    this.renderEffectIcons(
      'hud-active-upgrades',
      META_UPGRADES.filter((u) => state.upgrades.has(u.id)).map((u) => ({ ...u, type: 'upgrade' }))
    );
  },

  buildOrderTagsMarkup(orderOrTags) {
    const criteriaTags = Array.isArray(orderOrTags)
      ? orderOrTags
      : orderOrTags.criteriaTags || [];
    const labels = {
      period: I18n.t('orderTag.period'),
      genre: I18n.t('orderTag.genre'),
      artist: I18n.t('orderTag.artist'),
      artwork: I18n.t('orderTag.artwork'),
    };
    const tags = criteriaTags.map((tag) => {
      let value = tag.value;
      if (tag.type === 'artwork') {
        const art = ARTWORKS.find((a) => a.id === tag.value);
        value = art ? `"${I18n.artwork(art, 'title')}"` : tag.value;
      } else {
        value = I18n.vocab(tag.value);
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
        ? `<div class="order-brief-portrait"><img src="${o.portraitUrl}" alt="${uiCollectorName(o)}"></div>`
        : '';
      card.innerHTML = `
        ${portraitHtml}
        <div class="order-brief-content">
          <div class="order-brief-name">${uiCollectorName(o)}</div>
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

  updateClientBudgetDisplays(clientBudget) {
    document.getElementById('hud-capital').textContent = formatMoney(clientBudget);
    const live = document.getElementById('live-capital');
    if (live) live.textContent = I18n.formatMoneyWithCurrency(clientBudget);
  },

  updateBuyAffordability(price, clientBudget) {
    const btn = document.getElementById('btn-buy');
    if (!btn || btn.classList.contains('hidden')) return;
    if (typeof Game !== 'undefined' && Game.state) {
      const s = Game.state;
      if (s.lotResolved || s.awaitingLotStart || s.fastForwarding) return;
    }
    btn.disabled = price > clientBudget;
  },

  flashInsufficientFunds(kind = 'player') {
    Sound.playError();
    const hint = document.getElementById('insufficient-funds-hint');
    hint.textContent =
      kind === 'client' ? I18n.t('auction.insufficientClientBudget') : I18n.t('auction.insufficientFunds');
    hint.classList.remove('hidden');
    if (this.insufficientFundsTimer) clearTimeout(this.insufficientFundsTimer);
    this.insufficientFundsTimer = setTimeout(() => hint.classList.add('hidden'), 1200);
  },

  renderLot(lot, index, total, initialPrice) {
    document.getElementById('hud-lot-index').textContent = index + 1;
    document.getElementById('hud-lot-total').textContent = total;
    document.getElementById('lot-image-wrap').classList.remove('standby');
    document.getElementById('lot-image').src = lot.imageUrl;
    document.getElementById('familiar-badge').classList.toggle('hidden', !lot.familiar);

    const banner = document.getElementById('lot-result-banner');
    banner.classList.add('hidden');
    banner.className = 'lot-result-banner hidden';

    this.clearLotOutcomeFx();

    document.getElementById('btn-buy').disabled = false;
    document.getElementById('btn-skip').disabled = false;
    document.getElementById('btn-finish-day').disabled = false;
    document.getElementById('btn-buy').classList.remove('hidden');
    document.getElementById('btn-skip').classList.remove('hidden');
    document.getElementById('btn-start-lot').classList.add('hidden');
    document.querySelectorAll('.rival-head.raised').forEach((h) => h.classList.remove('raised'));

    this.pendingValues = uiArtworkFields(lot);
    Object.keys(this.pendingValues).forEach((f) => {
      const el = document.getElementById('field-' + f);
      el.classList.remove('revealed');
      el.querySelector('.field-value').textContent = maskValue(this.pendingValues[f]);
    });

    this.updateLiveEconomics(initialPrice, { animate: false });
    if (typeof Game !== 'undefined' && Game.state) {
      const clientBudget = getClientBudgetRemaining(Game.state);
      this.updateClientBudgetDisplays(clientBudget);
      this.updateBuyAffordability(initialPrice, clientBudget);
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
      fx.textContent = I18n.t('lotResult.fxWon');
    } else if (kind === 'lost') {
      wrap.classList.add('fx-lost');
      fx.className = 'lot-fx lot-fx-lost';
      fx.textContent = I18n.t('lotResult.fxLost');
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

    if (typeof Game !== 'undefined' && Game.state) {
      this.updateBuyAffordability(price, getClientBudgetRemaining(Game.state));
    }

    if (!animate || fromPrice === price) {
      if (this._econAnim) {
        cancelAnimationFrame(this._econAnim);
        this._econAnim = null;
      }
      priceEl.textContent = I18n.formatMoneyWithCurrency(price);
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
      priceEl.textContent = I18n.formatMoneyWithCurrency(p);
      if (t < 1) this._econAnim = requestAnimationFrame(tick);
      else this._econAnim = null;
    };
    this._econAnim = requestAnimationFrame(tick);
  },

  showWaitingHint() {
    const banner = document.getElementById('lot-result-banner');
    banner.textContent = I18n.t('lotResult.waiting');
    banner.className = 'lot-result-banner';
    banner.classList.remove('hidden');
  },

  showLotResult(kind) {
    const texts = {
      won: I18n.t('lotResult.won'),
      lost: I18n.t('lotResult.lost'),
      skipped: I18n.t('lotResult.skipped'),
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
    this._lastReportState = state;
    this._lastReportResult = result;
    this.showScreen('report');
    document.getElementById('report-day-number').textContent = state.day;

    const ordersEl = document.getElementById('report-orders');
    ordersEl.innerHTML = '';
    result.orderStats.forEach((o) => {
      const sign = o.commissionEarned >= 0 ? '+' : '−';
      const status = o.fulfilled ? I18n.t('report.orderFulfilled') : I18n.t('report.orderUnfulfilled');
      const row = document.createElement('div');
      row.className = 'order-report-card' + (o.fulfilled ? '' : ' unfulfilled');
      const portraitHtml = o.portraitUrl
        ? `<div class="order-report-portrait"><img src="${o.portraitUrl}" alt="${uiCollectorName(o)}"></div>`
        : '';
      row.innerHTML = `
        ${portraitHtml}
        <div class="order-report-body">
          <div class="order-report-name">${uiCollectorName(o)} <span class="order-venue-tag">${uiVenueLabel(o.venue)}</span>
            <span class="order-fulfill-tag ${o.fulfilled ? 'ok' : 'bad'}">${status}</span>
          </div>
          <div class="order-report-line">${I18n.t('report.budget')} ${I18n.formatMoneyWithCurrency(o.budget)} · ${I18n.t('report.spent')} ${I18n.formatMoneyWithCurrency(o.spent)} · ${I18n.t('report.clawback')} ${I18n.formatMoneyWithCurrency(o.leftover)}</div>
          <div class="order-report-line">${I18n.t('report.correct')} ${o.correctCount} · ${I18n.t('report.incorrect')} ${o.incorrectCount} · ${I18n.t('report.commission')} ${sign}${I18n.formatMoneyWithCurrency(Math.abs(o.commissionEarned))}</div>
          ${
            o.fulfilled
              ? ''
              : `<div class="order-report-line order-unfulfilled-hint">${I18n.t('report.unfulfilledHint')}</div>`
          }
        </div>
      `;
      ordersEl.appendChild(row);
    });

    const txEl = document.getElementById('report-transactions');
    txEl.innerHTML = '';
    if (!result.purchaseDetails.length) {
      txEl.innerHTML = `<div class="transaction-empty">${I18n.t('report.noPurchases')}</div>`;
    } else {
      result.purchaseDetails.forEach((p) => {
        const row = document.createElement('div');
        row.className = 'transaction-row transaction-row-clickable ' + (p.matched ? 'matched' : 'unmatched');
        row.setAttribute('role', 'button');
        row.tabIndex = 0;
        const sign = p.amount >= 0 ? '+' : '−';
        const artwork = ARTWORKS.find((a) => a.id === p.artworkId);
        const title = artwork ? I18n.artwork(artwork, 'title') : p.titleRu;
        row.innerHTML = `
          <div class="transaction-main">
            <div class="transaction-title">${p.matched ? '✅' : '❌'} ${title}
              <span class="transaction-price">(${I18n.formatMoneyWithCurrency(p.price)})</span>
            </div>
            <div class="transaction-reason">${p.reason}</div>
            <div class="transaction-view-card-hint">${I18n.t('report.viewCard')}</div>
          </div>
          <div class="transaction-amount ${p.amount >= 0 ? 'positive' : 'negative'}">${sign}${I18n.formatMoneyWithCurrency(Math.abs(p.amount))}</div>
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
        ? `<div class="ledger-row negative"><span>${I18n.t('report.ledger.otherSpend')}</span><span>−${I18n.formatMoneyWithCurrency(Math.abs(result.otherSpend))}</span></div>`
        : '';
    const creditLineRow = result.savedByCreditLine
      ? `<div class="ledger-row positive"><span>${I18n.t('report.ledger.creditLine')}</span><span>+${I18n.formatMoneyWithCurrency(result.creditLineCoverage)}</span></div>`
      : '';
    document.getElementById('report-ledger').innerHTML = `
      <div class="ledger-row"><span>${I18n.t('report.ledger.start')}</span><span>${I18n.formatMoneyWithCurrency(result.startingCapital)}</span></div>
      <div class="ledger-row ${result.totalCommission >= 0 ? 'positive' : 'negative'}"><span>${I18n.t('report.ledger.commission')}</span><span>${netSign}${I18n.formatMoneyWithCurrency(Math.abs(result.totalCommission))}</span></div>
      ${otherRow}
      ${creditLineRow}
      <div class="ledger-row total"><span>${I18n.t('report.ledger.end')}</span><span>${I18n.formatMoneyWithCurrency(result.projectedCapital)}</span></div>
    `;

    const verdict = document.getElementById('report-verdict');
    const continueBtn = document.getElementById('btn-report-continue');
    const boostersSection = document.getElementById('report-boosters-section');
    if (result.pass) {
      Sound.playDayPass(result.ordersFulfilled);
      if (result.savedByCreditLine) {
        verdict.textContent = I18n.t('verdict.creditLine', {
          amount: I18n.formatMoneyWithCurrency(result.creditLineCoverage),
        });
        verdict.className = 'report-verdict warn';
      } else if (!result.ordersFulfilled) {
        verdict.textContent = I18n.t('verdict.unfulfilled');
        verdict.className = 'report-verdict warn';
      } else if (state.day >= CAMPAIGN_LENGTH) {
        verdict.textContent = I18n.t('verdict.campaignComplete');
        verdict.className = 'report-verdict pass';
      } else {
        verdict.textContent = I18n.t('verdict.dayPass');
        verdict.className = 'report-verdict pass';
      }
      continueBtn.textContent = I18n.t('report.continue');
      if (state.day < CAMPAIGN_LENGTH) {
        boostersSection.classList.remove('hidden');
        this.refreshBoosterShop(state);
      } else {
        boostersSection.classList.add('hidden');
      }
    } else {
      Sound.playDayFail();
      verdict.textContent = I18n.t('verdict.bankruptcy');
      verdict.className = 'report-verdict fail';
      continueBtn.textContent = I18n.t('report.finish');
      boostersSection.classList.add('hidden');
    }
  },

  updateReportCapital(capital) {
    const row = document.querySelector('#report-ledger .ledger-row.total span:last-child');
    if (row) row.textContent = I18n.formatMoneyWithCurrency(capital);
  },

  refreshBoosterShop(state) {
    const offers = state.boosterOffers.map((id) => BOOSTERS.find((b) => b.id === id)).filter(Boolean);

    const heading = document.getElementById('report-boosters-heading');
    if (heading) {
      heading.textContent = I18n.t('report.boostersCount', {
        owned: state.pendingBoosters.size,
        total: offers.length,
      });
    }

    const el = document.getElementById('report-boosters');
    el.innerHTML = '';
    offers.forEach((b) => {
      const cost = getBoosterCost(b, state);
      const owned = state.pendingBoosters.has(b.id);
      const disabled = owned || state.capital < cost;
      const card = document.createElement('div');
      card.className = 'shop-card' + (owned ? ' owned' : '');
      card.innerHTML = `
        <div class="shop-card-name"><span class="shop-card-icon">${b.icon}</span>${uiBoosterName(b)}</div>
        <div class="shop-card-desc">${uiBoosterDesc(b)}</div>
        <button class="btn btn-secondary" ${disabled ? 'disabled' : ''}>
          ${owned ? I18n.t('shop.boosterOwned') : I18n.t('shop.buyFor', { price: `${formatMoney(cost)} ${I18n.currencySymbol()}` })}
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
    document.getElementById('end-title').textContent = I18n.t('end.careerTitle');
    document.getElementById('end-message').textContent = I18n.t('end.careerMessage', {
      days: CAMPAIGN_LENGTH,
      capital: I18n.formatMoneyWithCurrency(state.capital),
    });
  },

  showGameOver(state, result) {
    Sound.playDayFail();
    this.showScreen('end');
    document.getElementById('end-title').textContent = I18n.t('end.bankruptcyTitle');
    document.getElementById('end-message').textContent = I18n.t('end.bankruptcyMessage', {
      day: state.day,
      capital: I18n.formatMoneyWithCurrency(result.projectedCapital),
    });
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
      hintEl.textContent = review ? I18n.t('purchaseCard.reviewHint') : I18n.t('purchaseCard.pauseHint');
    }
    continueBtn.textContent = review ? I18n.t('purchaseCard.close') : I18n.t('purchaseCard.continue');

    const fields = uiArtworkFields(lot);
    document.getElementById('purchase-card-image').src = lot.imageUrl;
    document.getElementById('purchase-card-image').alt = fields.title;
    document.getElementById('purchase-card-title').textContent = fields.title;
    document.getElementById('purchase-card-artist').textContent = fields.artist;
    document.getElementById('purchase-card-period').textContent = fields.period;
    document.getElementById('purchase-card-genre').textContent = fields.genre;
    document.getElementById('purchase-card-rarity').textContent = I18n.t(`rarity.${lot.rarity}`);
    document.getElementById('purchase-card-price').textContent = I18n.formatMoneyWithCurrency(price);
    document.getElementById('purchase-card-fact').textContent = fields.fact;

    overlayEl.classList.remove('hidden');
    continueBtn.focus();
  },

  closePurchaseCard() {
    if (!this.isPurchaseCardOpen()) return;
    Sound.playCardClose();
    document.getElementById('purchase-card-overlay').classList.add('hidden');
    const hintEl = document.getElementById('purchase-card-pause-hint');
    if (hintEl) hintEl.textContent = I18n.t('purchaseCard.pauseHint');
    document.getElementById('btn-purchase-card-continue').textContent = I18n.t('purchaseCard.continue');
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
