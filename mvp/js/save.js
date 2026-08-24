// Career progress persistence via localStorage.
// Saves between days (brief) and at end of auction (report); never mid-lot.

const SaveGame = {
  KEY: 'master-bidder-save-v1',
  VERSION: 1,

  hasSave() {
    return this.loadRaw() != null;
  },

  clear() {
    try {
      localStorage.removeItem(this.KEY);
    } catch (_) {}
  },

  loadRaw() {
    try {
      const raw = localStorage.getItem(this.KEY);
      if (!raw) return null;
      const data = JSON.parse(raw);
      if (!data || data.version !== this.VERSION) return null;
      if (data.phase !== 'brief' && data.phase !== 'report') return null;
      return data;
    } catch (_) {
      return null;
    }
  },

  write(state, phase) {
    if (!state || (phase !== 'brief' && phase !== 'report')) return;
    try {
      localStorage.setItem(this.KEY, JSON.stringify(this.serialize(state, phase)));
    } catch (_) {}
  },

  serialize(state, phase) {
    return {
      version: this.VERSION,
      phase,
      day: state.day,
      capital: state.capital,
      dayStartCapital: state.dayStartCapital,
      clientBudgetRemaining: state.clientBudgetRemaining ?? 0,
      seenArtworkIds: Array.from(state.seenArtworkIds || []),
      artworkPurchaseDays: { ...(state.artworkPurchaseDays || {}) },
      upgrades: Array.from(state.upgrades || []),
      pendingBoosters: Array.from(state.pendingBoosters || []),
      activeBoosters: Array.from(state.activeBoosters || []),
      boosterOffers: [...(state.boosterOffers || [])],
      lotMasterLucky: !!state.lotMasterLucky,
      creditLineUsed: !!state.creditLineUsed,
      dayConfig: state.dayConfig ? { ...state.dayConfig } : null,
      dayOrders: (state.dayOrders || []).map((o) => this.clonePlain(o)),
      lots: this.serializeLots(state.lots),
      pendingOrder: state.pendingOrder ? this.clonePlain(state.pendingOrder) : null,
      pendingVenueKey: state.pendingVenueConfig?.key || state.currentVenue || 'regular',
      currentLotIndex: state.currentLotIndex || 0,
      purchasesToday: (state.purchasesToday || []).map((p) => this.clonePlain(p)),
      pendingResult: state.pendingResult ? this.clonePlain(state.pendingResult) : null,
      branchProgress: { ...(state.branchProgress || {}) },
      selectedBranchId: state.selectedBranchId || COLLECTORS[0].id,
      currentVenue: state.currentVenue || 'regular',
    };
  },

  serializeLots(lots) {
    return (lots || []).map((lot) => ({
      id: lot.id,
      basePriceJittered: lot.basePriceJittered,
      familiar: !!lot.familiar,
    }));
  },

  clonePlain(value) {
    return JSON.parse(JSON.stringify(value));
  },

  /** Rebuild Game.state fields from a saved snapshot. Caller must Game.init() first. */
  hydrate(state, data) {
    state.day = data.day;
    state.capital = data.capital;
    state.dayStartCapital = data.dayStartCapital;
    state.clientBudgetRemaining = data.clientBudgetRemaining || 0;
    state.seenArtworkIds = new Set(data.seenArtworkIds || []);
    state.artworkPurchaseDays = { ...(data.artworkPurchaseDays || {}) };
    state.upgrades = new Set(data.upgrades || []);
    state.pendingBoosters = new Set(data.pendingBoosters || []);
    state.activeBoosters = new Set(data.activeBoosters || []);
    state.boosterOffers = [...(data.boosterOffers || [])];
    state.lotMasterLucky = !!data.lotMasterLucky;
    state.creditLineUsed = !!data.creditLineUsed;
    state.dayConfig = data.dayConfig ? { ...data.dayConfig } : getWorldConfig(data.day, state);
    state.dayOrders = (data.dayOrders || []).map((o) => this.clonePlain(o));
    state.lots = this.hydrateLots(data.lots || [], state.seenArtworkIds);
    state.pendingOrder = data.pendingOrder ? this.clonePlain(data.pendingOrder) : null;
    state.pendingVenueConfig = VENUES[data.pendingVenueKey] || VENUES.regular;
    state.currentLotIndex = data.currentLotIndex || 0;
    state.purchasesToday = (data.purchasesToday || []).map((p) => this.clonePlain(p));
    state.pendingResult = data.pendingResult ? this.clonePlain(data.pendingResult) : null;
    state.branchProgress = { ...(data.branchProgress || {}) };
    state.selectedBranchId = data.selectedBranchId || COLLECTORS[0].id;
    state.currentVenue = data.currentVenue || data.pendingVenueKey || 'regular';
    state.lotResolved = false;
    state.revealStep = 0;
    state.revealTimers = [];
    state.rivalTimer = null;
    state.fastForwarding = false;
    state.awaitingLotStart = false;
  },

  hydrateLots(savedLots, seenSet) {
    return savedLots
      .map((saved) => {
        const artwork = ARTWORKS.find((a) => a.id === saved.id);
        if (!artwork) return null;
        return {
          ...artwork,
          basePriceJittered: saved.basePriceJittered,
          familiar: saved.familiar ?? seenSet.has(artwork.id),
        };
      })
      .filter(Boolean);
  },
};
