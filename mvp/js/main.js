document.addEventListener('DOMContentLoaded', () => {
  UI.init();

  document.getElementById('btn-start-campaign').addEventListener('click', () => Game.startCampaign());
  document.getElementById('btn-start-day').addEventListener('click', () => Game.beginAuction());
  document.getElementById('btn-buy').addEventListener('click', () => Game.onBuyClicked());
  document.getElementById('btn-skip').addEventListener('click', () => Game.onSkipClicked());
  document.getElementById('btn-report-continue').addEventListener('click', () => Game.continueAfterReport());
  document.getElementById('btn-restart-campaign').addEventListener('click', () => Game.startCampaign());

  document.getElementById('lot-image-wrap').addEventListener('click', () => UI.openZoom());
  document.getElementById('zoom-modal').addEventListener('click', () => UI.closeZoom());

  document.addEventListener('keydown', (e) => {
    if (e.code === 'Space') {
      const auctionActive = document.getElementById('screen-auction').classList.contains('active');
      if (auctionActive) {
        e.preventDefault();
        Game.onBuyClicked();
      }
    }
  });
});
