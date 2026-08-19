// Core game logic: day/venue setup, lot presentation timing, buy-race resolution,
// and end-of-day settlement. No DOM access here except through UI.* calls.

function shuffle(arr) {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

function randRange(min, max) {
  return min + Math.random() * (max - min);
}

function formatMoney(n) {
  return Math.round(n).toLocaleString('ru-RU');
}

// Hides a not-yet-revealed field's value behind a redacted placeholder of
// roughly the same length, so nothing can be read before it's announced.
function maskValue(text) {
  return '•'.repeat(Math.max(6, Math.min(String(text).length, 28)));
}

// A lot matches an order's criteria only if EVERY tag matches (AND logic).
// A 'trophy' order (type: 'artwork') matches only that exact artwork id.
// Works on either a full ARTWORKS entry or a purchased-lot snapshot — both
// carry id/periodRu/genreRu/artistRu.
function matchesCriteria(artwork, criteriaTags) {
  return criteriaTags.every((tag) => {
    if (tag.type === 'artwork') return artwork.id === tag.value;
    if (tag.type === 'period') return artwork.periodRu === tag.value;
    if (tag.type === 'genre') return artwork.genreRu === tag.value;
    if (tag.type === 'artist') return artwork.artistRu === tag.value;
    return false;
  });
}

function describeCriteria(tags) {
  const labels = { period: 'Период', genre: 'Жанр', artist: 'Автор' };
  return tags.map((t) => `${labels[t.type]}: ${t.value}`).join(' И ');
}

function getBranchBudgetMultiplierBonus(state) {
  return state.upgrades.has('gallery-connections') ? GALLERY_CONNECTIONS_BUDGET_BONUS : 0;
}

// Builds the day's single order for a collector branch. Tags come from that
// specific day's authored mission (collector.missions[missionIndex], clamped
// to the branch's last day past the ceiling — "mastery" plateau); trophy
// chance and venue tier come from branchCfg (see campaign.js getBranchMissionConfig).
function buildOrder(collector, branchCfg, venueConfig, state, tags) {
  const wantsTrophy = venueConfig.key !== 'local' && branchCfg.trophyChance > 0 && Math.random() < branchCfg.trophyChance;
  const budgetMultiplier = branchCfg.branchBudgetMultiplier + getBranchBudgetMultiplierBonus(state);
  const budget = Math.round((collector.baseBudget * budgetMultiplier * venueConfig.budgetFactor) / 100) * 100;

  if (wantsTrophy) {
    const candidates = ARTWORKS.filter(
      (a) => venueConfig.rarityPool.includes(a.rarity) && matchesCriteria(a, tags)
    );
    if (candidates.length) {
      const target = candidates[Math.floor(Math.random() * candidates.length)];
      return {
        nameRu: collector.nameRu,
        taglineRu: collector.taglineRu,
        criteriaTags: [{ type: 'artwork', value: target.id }],
        criteriaLabel: `Именно эта работа: «${target.titleRu}» (${target.artistRu})`,
        budget,
        personalModifier: collector.personalModifier,
        venue: venueConfig.key,
      };
    }
  }

  return {
    nameRu: collector.nameRu,
    taglineRu: collector.taglineRu,
    criteriaTags: tags,
    criteriaLabel: describeCriteria(tags),
    budget,
    personalModifier: collector.personalModifier,
    venue: venueConfig.key,
  };
}

// Builds the single order for the day, from the branch the player selected on
// the brief screen. The branch's own mission counter (never reset) decides
// which authored day's tags apply, plus trophy chance/venue tier — see
// campaign.js getBranchMissionConfig.
function buildDayOrder(state) {
  const collector = COLLECTORS.find((c) => c.id === state.selectedBranchId);
  const missionIndex = state.branchProgress[collector.id] || 0;
  const branchCfg = getBranchMissionConfig(missionIndex, collector.missions.length);
  const venueConfig = VENUES[branchCfg.venueTier];
  const dayIndex = Math.min(missionIndex, collector.missions.length - 1);
  const tags = collector.missions[dayIndex].tags;
  return { order: buildOrder(collector, branchCfg, venueConfig, state, tags), venueConfig };
}

function jitterPrice(base) {
  const factor = 0.85 + Math.random() * 0.3;
  return Math.round((base * factor) / 100) * 100;
}

// Lots are drawn from the venue's own rarity pool (Local skips epic, Elite
// skips common) — venues differ in content, not just in risk.
function drawLots(count, seenSet, rarityPool) {
  const candidates = ARTWORKS.filter((a) => rarityPool.includes(a.rarity));
  const shuffled = shuffle(candidates.slice());
  const picked = shuffled.slice(0, Math.min(count, shuffled.length));
  return picked.map((a) => ({
    ...a,
    basePriceJittered: jitterPrice(a.basePrice),
    familiar: seenSet.has(a.id),
  }));
}

// Price rises one step per revealed field; the same step drives the speed
// multiplier down. There is exactly one multiplier behind both effects.
function computeLivePrice(lot, step) {
  return Math.round((lot.basePriceJittered * (1 + step * PRICE_STEP_PCT)) / 100) * 100;
}

function computeSpeedMultiplier(step, floor) {
  const maxStep = REVEALABLE_FIELDS.length;
  const t = Math.min(step, maxStep) / maxStep;
  return Math.max(floor, 1 - t * (1 - floor));
}

function getSpeedFloor(state) {
  return SPEED_MULTIPLIER_FLOOR + (state.upgrades.has('fast-appraisal') ? 0.1 : 0);
}

// Settlement happens once, at the end of the day, across every purchase made
// in every venue session played that day. There is no more rating — the only
// question is whether the balance stays non-negative (see GAME_DESIGN.md,
// Loss Condition & Campaign Progression).
function computeSettlement(state) {
  const { dayOrders, purchasesToday, capital, dayStartCapital } = state;
  const speedFloor = getSpeedFloor(state);

  const orderStats = dayOrders.map((o) => ({
    ...o,
    spent: 0,
    commissionEarned: 0,
    correctCount: 0,
    incorrectCount: 0,
  }));

  let totalCommission = 0;
  const purchaseDetails = [];

  purchasesToday.forEach((p) => {
    const matchingIdx = [];
    dayOrders.forEach((o, idx) => {
      if (matchesCriteria(p, o.criteriaTags)) matchingIdx.push(idx);
    });

    let creditIdx;
    let matched;
    if (matchingIdx.length > 0) {
      // Conflicting interest: randomly credit exactly one interested collector.
      creditIdx = matchingIdx[Math.floor(Math.random() * matchingIdx.length)];
      matched = true;
    } else {
      // No order wanted this lot — it's still taken by someone, at a reduced
      // rate. The player never keeps a painting (see GAME_DESIGN.md, Orders &
      // Budget). Prefer an order from the same venue session, if any.
      const sameVenueIdx = dayOrders.map((o, idx) => idx).filter((idx) => dayOrders[idx].venue === p.venue);
      const pool = sameVenueIdx.length ? sameVenueIdx : dayOrders.map((_, idx) => idx);
      creditIdx = pool[Math.floor(Math.random() * pool.length)];
      matched = false;
    }

    const order = orderStats[creditIdx];
    order.spent += p.price;

    const rarityValue = RARITY_COMMISSION_VALUE[p.rarity];
    const speedMultiplier = computeSpeedMultiplier(p.revealStep, speedFloor);
    let fitCoefficient;
    if (matched) {
      fitCoefficient = 1;
      order.correctCount += 1;
    } else {
      fitCoefficient = state.dayConfig.incorrectFitCoefficient;
      const venueDef = VENUES[p.venue];
      const noFine = (venueDef && venueDef.guaranteedNonNegativeFine) || state.insuranceActiveToday;
      if (noFine) fitCoefficient = Math.max(0, fitCoefficient);
      order.incorrectCount += 1;
    }

    const commission = Math.round(rarityValue * speedMultiplier * fitCoefficient * order.personalModifier);
    order.commissionEarned += commission;
    totalCommission += commission;

    purchaseDetails.push({
      titleRu: p.titleRu,
      price: p.price,
      matched,
      amount: commission,
      reason: matched
        ? `Подошло заказчику «${order.nameRu}» — ${order.criteriaLabel}.`
        : `Не подошло ни одному заказу — списано заказчику «${order.nameRu}» по сниженной ставке.`,
    });
  });

  let totalClawback = 0;
  orderStats.forEach((o) => {
    o.leftover = Math.max(0, o.budget - o.spent);
    totalClawback += o.leftover;
  });

  const net = Math.round(totalCommission - totalClawback);
  const projectedCapital = capital + net;
  const pass = projectedCapital >= 0;
  // dayNet is the true start-to-end delta for the ledger; it also folds in
  // spends that aren't visible above (elite ticket cost, personal overspend
  // beyond an order's budget) so the ledger always reconciles exactly.
  const dayNet = projectedCapital - dayStartCapital;
  const otherSpend = dayNet - net;

  return {
    orderStats,
    purchaseDetails,
    totalCommission: Math.round(totalCommission),
    totalClawback: Math.round(totalClawback),
    otherSpend,
    net,
    startingCapital: dayStartCapital,
    projectedCapital,
    pass,
  };
}

const Game = {
  state: null,

  init() {
    this.state = {
      day: 1,
      capital: STARTING_CAPITAL,
      dayStartCapital: STARTING_CAPITAL,
      seenArtworkIds: new Set(),
      upgrades: new Set(),
      pendingBoosters: new Set(),
      insuranceActiveToday: false,
      dayConfig: null,
      dayOrders: [],
      lots: [],
      pendingOrder: null,
      pendingVenueConfig: null,
      currentLotIndex: 0,
      purchasesToday: [],
      lotResolved: false,
      revealStep: 0,
      revealTimers: [],
      rivalTimer: null,
      pendingResult: null,
      branchProgress: {},
      selectedBranchId: COLLECTORS[0].id,
      currentVenue: 'regular',
    };
  },

  startCampaign() {
    this.init();
    this.startDay();
  },

  prepareDayLots() {
    const { order, venueConfig } = buildDayOrder(this.state);
    this.state.pendingOrder = order;
    this.state.pendingVenueConfig = venueConfig;
    this.state.lots = drawLots(venueConfig.lotsCount(), this.state.seenArtworkIds, venueConfig.rarityPool);
    this.state.currentLotIndex = 0;
    ImageCache.preloadUrls(
      this.state.lots.map((lot) => lot.imageUrl),
      (progress) => UI.updateBriefPreload(progress)
    );
  },

  startDay() {
    this.state.dayStartCapital = this.state.capital;
    const cfg = getWorldConfig(this.state.day);
    this.state.dayConfig = cfg;
    this.state.dayOrders = [];
    this.state.purchasesToday = [];
    this.prepareDayLots();
    UI.showBrief(this.state, cfg);
  },

  selectBranch(id) {
    if (!COLLECTORS.some((c) => c.id === id)) return;
    this.state.selectedBranchId = id;
    this.prepareDayLots();
    UI.refreshBranchChoice(this.state);
  },

  buyUpgrade(id) {
    const upgrade = META_UPGRADES.find((u) => u.id === id);
    if (!upgrade || this.state.upgrades.has(id)) return;
    if (this.state.capital < upgrade.cost) {
      UI.flashInsufficientFunds();
      return;
    }
    this.state.capital -= upgrade.cost;
    this.state.upgrades.add(id);
    UI.updateBriefCapital(this.state.capital);
    UI.refreshUpgradeShop(this.state);
  },

  buyBooster(id) {
    const booster = BOOSTERS.find((b) => b.id === id);
    if (!booster || this.state.pendingBoosters.has(id)) return;
    const cost = booster.cost(this.state.day + 1);
    if (this.state.capital < cost) {
      UI.flashInsufficientFunds();
      return;
    }
    this.state.capital -= cost;
    this.state.pendingBoosters.add(id);
    UI.updateReportCapital(this.state.capital);
    UI.refreshBoosterShop(this.state);
  },

  creditOrders(orders) {
    const total = orders.reduce((sum, o) => sum + o.budget, 0);
    this.state.capital += total;
  },

  beginAuction() {
    if (!ImageCache.isReady()) return;
    const order = this.state.pendingOrder;
    const venueConfig = this.state.pendingVenueConfig;
    this.creditOrders([order]);
    this.state.dayOrders.push(order);
    this.state.currentVenue = venueConfig.key;
    this.state.currentLotIndex = 0;
    UI.showAuctionScreen(this.state);
    this.presentLot();
  },

  presentLot() {
    const { lots, currentLotIndex } = this.state;
    if (currentLotIndex >= lots.length) {
      this.finishDay();
      return;
    }

    this.state.lotResolved = false;
    this.state.revealStep = 0;
    const lot = lots[currentLotIndex];
    const floor = getSpeedFloor(this.state);
    UI.renderLot(lot, currentLotIndex, lots.length, computeLivePrice(lot, 0), computeSpeedMultiplier(0, floor));

    this.state.revealTimers = REVEALABLE_FIELDS.map((f, i) =>
      setTimeout(() => {
        this.state.revealStep = i + 1;
        UI.revealField(f);
        UI.updateLiveEconomics(computeLivePrice(lot, this.state.revealStep), computeSpeedMultiplier(this.state.revealStep, floor));
      }, REVEAL_INTERVAL_MS * (i + 1))
    );
    this.state.revealTimers.push(
      setTimeout(() => {
        if (!this.state.lotResolved) UI.showWaitingHint();
      }, REVEAL_INTERVAL_MS * REVEALABLE_FIELDS.length + 400)
    );

    const cfg = this.state.dayConfig;
    const venueConfig = VENUES[this.state.currentVenue];
    const rivalDelayMs = 1000 * randRange(cfg.rivalMinSec, cfg.rivalMaxSec) * venueConfig.rivalSpeedFactor;
    this.state.rivalTimer = setTimeout(() => this.onRivalWins(), rivalDelayMs);
  },

  clearLotTimers() {
    this.state.revealTimers.forEach(clearTimeout);
    this.state.revealTimers = [];
    if (this.state.rivalTimer) clearTimeout(this.state.rivalTimer);
    this.state.rivalTimer = null;
  },

  onBuyClicked() {
    if (this.state.lotResolved) return;
    const lot = this.state.lots[this.state.currentLotIndex];
    const price = computeLivePrice(lot, this.state.revealStep);
    if (price > this.state.capital) {
      UI.flashInsufficientFunds();
      return;
    }
    this.clearLotTimers();
    this.state.lotResolved = true;
    this.state.capital -= price;
    this.state.purchasesToday.push({
      id: lot.id,
      titleRu: lot.titleRu,
      periodRu: lot.periodRu,
      genreRu: lot.genreRu,
      artistRu: lot.artistRu,
      rarity: lot.rarity,
      price,
      revealStep: this.state.revealStep,
      venue: this.state.currentVenue,
    });
    UI.showLotResult('won');
    UI.updateCapitalDisplays(this.state.capital);
    setTimeout(() => this.advanceLot(), RESOLUTION_PAUSE_MS);
  },

  onRivalWins() {
    if (this.state.lotResolved) return;
    this.clearLotTimers();
    this.state.lotResolved = true;
    UI.raiseRandomHand();
    setTimeout(() => {
      UI.showLotResult('lost');
      setTimeout(() => this.advanceLot(), RESOLUTION_PAUSE_MS);
    }, 350);
  },

  onSkipClicked() {
    if (this.state.lotResolved) return;
    this.clearLotTimers();
    this.state.lotResolved = true;
    UI.showLotResult('skipped');
    setTimeout(() => this.advanceLot(), RESOLUTION_PAUSE_MS);
  },

  advanceLot() {
    this.state.currentLotIndex += 1;
    this.presentLot();
  },

  finishDay() {
    const result = computeSettlement(this.state);
    this.state.pendingResult = result;
    if (result.pass) {
      this.state.capital = result.projectedCapital;
    }
    UI.showReport(this.state, result);
  },

  continueAfterReport() {
    const result = this.state.pendingResult;
    if (!result.pass) {
      UI.showGameOver(this.state, result);
      return;
    }
    this.state.insuranceActiveToday = this.state.pendingBoosters.has('insurance');
    this.state.pendingBoosters = new Set();
    this.state.purchasesToday.forEach((p) => this.state.seenArtworkIds.add(p.id));
    const branchId = this.state.selectedBranchId;
    this.state.branchProgress[branchId] = (this.state.branchProgress[branchId] || 0) + 1;
    this.state.day += 1;
    if (this.state.day > CAMPAIGN_LENGTH) {
      UI.showCampaignEnd(this.state);
      return;
    }
    this.startDay();
  },
};
