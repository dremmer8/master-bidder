document.addEventListener('DOMContentLoaded', () => {
  I18n.init();
  I18n.applyStaticTexts();

  const langSelect = document.getElementById('lang-select');
  langSelect.value = I18n.getLocale();
  langSelect.addEventListener('change', () => {
    I18n.setLocale(langSelect.value);
    UI.onLocaleChange();
    updateSoundToggleUI();
    updateCurrencySymbols();
  });

  function updateCurrencySymbols() {
    document.querySelectorAll('.currency-symbol').forEach((el) => {
      el.textContent = I18n.currencySymbol();
    });
  }
  updateCurrencySymbols();

  window.addEventListener('localechange', () => {
    langSelect.value = I18n.getLocale();
    updateCurrencySymbols();
  });

  UI.init();
  Sound.init();

  const soundToggleBtn = document.getElementById('btn-sound-toggle');
  const updateSoundToggleUI = () => {
    const muted = Sound.isMuted();
    soundToggleBtn.textContent = muted ? '🔇' : '🔊';
    soundToggleBtn.setAttribute('aria-pressed', String(muted));
    soundToggleBtn.title = I18n.t(muted ? 'sound.unmute' : 'sound.mute');
  };
  updateSoundToggleUI();
  soundToggleBtn.addEventListener('click', () => {
    Sound.toggleMute();
    updateSoundToggleUI();
    if (!Sound.isMuted()) Sound.playClick();
  });
  document.addEventListener('pointerdown', () => Sound.ensureUnlocked(), { once: true });
  document.addEventListener('keydown', () => Sound.ensureUnlocked(), { once: true });

  document.getElementById('btn-continue-campaign').addEventListener('click', () => {
    Sound.playClick();
    Game.continueCampaign();
  });
  document.getElementById('btn-start-campaign').addEventListener('click', () => {
    Sound.playClick();
    if (SaveGame.hasSave() && !window.confirm(I18n.t('intro.newCareerConfirm'))) return;
    Game.startCampaign();
    UI.refreshIntroContinue();
  });
  document.getElementById('btn-start-day').addEventListener('click', () => {
    Sound.playClick();
    Game.beginAuction();
  });
  document.getElementById('btn-reset-progress').addEventListener('click', () => {
    Sound.playClick();
    if (!window.confirm(I18n.t('brief.resetConfirm'))) return;
    Game.resetProgress();
  });
  document.getElementById('btn-start-lot').addEventListener('click', () => {
    Sound.playClick();
    Game.startCurrentLot();
  });
  document.getElementById('btn-buy').addEventListener('click', () => Game.onBuyClicked());
  document.getElementById('btn-skip').addEventListener('click', () => Game.onSkipClicked());
  document.getElementById('btn-finish-day').addEventListener('click', () => {
    Sound.playClick();
    Game.onFinishDayClicked();
  });
  document.getElementById('btn-report-continue').addEventListener('click', () => {
    Sound.playClick();
    Game.continueAfterReport();
  });
  document.getElementById('btn-restart-campaign').addEventListener('click', () => {
    Sound.playClick();
    Game.startCampaign();
    UI.refreshIntroContinue();
  });

  document.getElementById('lot-image-wrap').addEventListener('click', () => UI.openZoom());
  document.getElementById('zoom-modal').addEventListener('click', () => UI.closeZoom());

  const purchaseCardImageWrap = document.getElementById('purchase-card-image-wrap');
  if (purchaseCardImageWrap) {
    purchaseCardImageWrap.addEventListener('click', (e) => {
      e.stopPropagation();
      UI.openPurchaseCardZoom();
    });
    purchaseCardImageWrap.addEventListener('keydown', (e) => {
      if (e.code === 'Enter' || e.code === 'Space') {
        e.preventDefault();
        e.stopPropagation();
        UI.openPurchaseCardZoom();
      }
    });
  }

  document.getElementById('btn-purchase-card-collapse')?.addEventListener('click', () => UI.closePurchaseCard());
  document.getElementById('btn-purchase-card-continue')?.addEventListener('click', () => UI.closePurchaseCard());

  document.addEventListener('keydown', (e) => {
    if (UI.isZoomOpen()) {
      if (e.code === 'Escape' || e.code === 'Space') {
        e.preventDefault();
        UI.closeZoom();
      }
      return;
    }
    if (UI.isPurchaseCardOpen()) {
      if (e.code === 'Escape' || e.code === 'Space' || e.code === 'Enter') {
        e.preventDefault();
        UI.closePurchaseCard();
      }
      return;
    }
    if (e.code === 'Space') {
      const auctionActive = document.getElementById('screen-auction').classList.contains('active');
      const startLotBtn = document.getElementById('btn-start-lot');
      if (auctionActive && startLotBtn && !startLotBtn.classList.contains('hidden')) {
        e.preventDefault();
        Game.startCurrentLot();
        return;
      }
      if (auctionActive) {
        e.preventDefault();
        Game.onBuyClicked();
      }
    }
    if (e.code === 'Enter') {
      const auctionActive = document.getElementById('screen-auction').classList.contains('active');
      const startLotBtn = document.getElementById('btn-start-lot');
      if (auctionActive && startLotBtn && !startLotBtn.classList.contains('hidden')) {
        e.preventDefault();
        Game.startCurrentLot();
      }
    }
  });
});
