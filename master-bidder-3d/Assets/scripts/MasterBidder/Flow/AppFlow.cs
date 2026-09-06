using System.Collections;
using MasterBidder.Audio;
using MasterBidder.Campaign;
using MasterBidder.Content;
using MasterBidder.Core;
using MasterBidder.Presentation;
using MasterBidder.Services;
using MasterBidder.UI;
using UnityEngine;

namespace MasterBidder.Flow
{
    /// <summary>
    /// Scene entry point: owns GameSession, UI shell, timers, and presentation.
    /// </summary>
    public class AppFlow : MonoBehaviour
    {
        [SerializeField] GameCatalog catalog;
        [SerializeField] AudioCatalog audioCatalog;
        [SerializeField] PaintingVoiceoverLibrary voiceLibrary;
        [SerializeField] PresentationOperator presentation;
        [SerializeField] bool disablePresentationDemoHotkeys = true;

        GameSession _session;
        AuctionTimerHost _timers;
        GameUiShell _ui;
        bool _resolvingPurchase;
        Coroutine _presentRoutine;
        Coroutine _purchaseResolveRoutine;

        public GameSession Session => _session;
        public GameCatalog Catalog => catalog;
        /// <summary>True while cloth/lot presentation is in progress (actions should stay locked).</summary>
        public bool IsPresentingLot => _presentRoutine != null;
        /// <summary>True while waiting on the sold-ticket appear beat after a buy.</summary>
        public bool IsResolvingPurchase => _resolvingPurchase;

        void Awake()
        {
            LocaleService.Init();
            AudioService.EnsureInitialized(this, audioCatalog, voiceLibrary);

            if (presentation == null)
                presentation = FindObjectOfType<PresentationOperator>();

            if (disablePresentationDemoHotkeys && presentation != null)
                presentation.DemoHotkeysEnabled = false;

            _timers = gameObject.GetComponent<AuctionTimerHost>();
            if (_timers == null) _timers = gameObject.AddComponent<AuctionTimerHost>();
            _timers.OnFieldRevealed = PlayRevealVoiceForCurrentLot;

            _ui = gameObject.GetComponent<GameUiShell>();
            if (_ui == null) _ui = gameObject.AddComponent<GameUiShell>();
            _ui.BuildIfNeeded();
            _ui.Bind(this);
        }

        float PlayRevealVoiceForCurrentLot(string fieldId)
        {
            if (_session?.State?.CurrentLot == null || catalog == null) return 0f;
            var data = catalog.FindPainting(_session.State.CurrentLot.Id);
            return AudioService.PlayRevealVoice(data, fieldId);
        }

        void Start()
        {
            if (catalog == null)
            {
                Debug.LogError("[AppFlow] Assign a GameCatalog asset (Master Bidder → Sync MVP Content).");
                _ui.ShowScreen(GameScreen.Intro);
                _ui.Refresh(null);
                return;
            }

            BindSession(new GameSession(catalog));
            HandleScreen(GameScreen.Intro);
            HandleState();
        }

        void OnDestroy()
        {
            UnbindSession();
            _timers?.Unbind();
        }

        void BindSession(GameSession session)
        {
            UnbindSession();
            _session = session;
            _session.OnScreenRequested += HandleScreen;
            _session.OnStateChanged += HandleState;
            _session.OnRivalWon += HandleRivalWon;
            _session.OnPresentLotRequested += OnPresentLotRequested;
            _session.OnAdvanceLotRequested += OnAdvanceLotRequested;
            _timers.Bind(_session);
        }

        void UnbindSession()
        {
            if (_session == null) return;
            _session.OnScreenRequested -= HandleScreen;
            _session.OnStateChanged -= HandleState;
            _session.OnRivalWon -= HandleRivalWon;
            _session.OnPresentLotRequested -= OnPresentLotRequested;
            _session.OnAdvanceLotRequested -= OnAdvanceLotRequested;
        }

        void HandleScreen(GameScreen screen)
        {
            CancelPurchaseResolve();
            _ui.HidePurchaseCard();
            _ui.ShowScreen(screen);
            _ui.Refresh(_session);

            // Painting inspect/zoom only during auction — not on brief/order select.
            if (presentation?.InspectCamera != null)
                presentation.InspectCamera.InputEnabled = screen == GameScreen.Auction;

            if (screen == GameScreen.Auction && _session?.State != null)
            {
                var order = _session.State.DayOrders.Count > 0
                    ? _session.State.DayOrders[0]
                    : _session.State.PendingOrder;
                if (_session.State.AwaitingLotStart && order != null)
                    _ui.ShowCollectorPopup(order, catalog?.FindCollector(order.CollectorId));
            }
        }

        void HandleState() => _ui.Refresh(_session);

        void HandleRivalWon()
        {
            AudioService.StopTension();
            AudioService.PlayRivalRaise();
            AudioService.PlayOutcome("lost");
            _ui.RaiseRandomRival();
            // Ticket appear replaces the old fixed resolution pause.
            _timers.CancelResolution();
            BeginPurchaseResolve();
        }

        void OnPresentLotRequested()
        {
            if (_presentRoutine != null) StopCoroutine(_presentRoutine);
            _presentRoutine = StartCoroutine(PresentThenStartTimers());
        }

        void OnAdvanceLotRequested()
        {
            // Timer host resets lot state on this event; wait a frame then present.
            if (_presentRoutine != null) StopCoroutine(_presentRoutine);
            _presentRoutine = StartCoroutine(PresentThenStartTimers());
        }

        IEnumerator PresentThenStartTimers()
        {
            try
            {
                // Let AuctionTimerHost.OnAdvance run PresentLotLogicReset first.
                yield return null;

                if (_session?.State == null) yield break;
                if (_session.State.CurrentLotIndex >= _session.State.Lots.Count) yield break;

                bool done = false;
                PresentCurrentLotVisual(() => done = true);
                while (!done) yield return null;

                if (_session?.State == null) yield break;
                if (_session.State.LotResolved) yield break;
                if (_session.State.CurrentLotIndex >= _session.State.Lots.Count) yield break;

                _timers.StartLotTimers();
            }
            finally
            {
                _presentRoutine = null;
                if (_session != null)
                    _ui?.Refresh(_session);
            }
        }

        public void OnContinueCareer()
        {
            EnsureSession();
            if (!_session.ContinueCampaign())
                Debug.LogWarning("[AppFlow] No save to continue.");
        }

        public void OnStartCareer()
        {
            EnsureSession();
            _session.StartCampaign();
        }

        public void OnResetProgress()
        {
            EnsureSession();
            _session.ResetProgress();
            EnsureSession();
            HandleScreen(GameScreen.Intro);
        }

        public void OnEnterHall()
        {
            if (_session?.State == null) return;
            _session.BeginAuction();
        }

        public void OnCollectorPopupStart()
        {
            _ui.HideCollectorPopup();
            OnStartLot();
        }

        public void OnStartLot()
        {
            _ui.HideCollectorPopup();
            _session?.StartCurrentLot();
        }

        public void OnBuy()
        {
            if (_session == null || _resolvingPurchase) return;
            if (!_session.TryBuy(out bool insufficient))
            {
                if (insufficient)
                {
                    AudioService.PlayError();
                    _ui.FlashInsufficientFunds();
                }
                return;
            }

            _timers.CancelResolution();
            AudioService.StopTension();
            AudioService.StopVoiceover();
            AudioService.PlayOutcome("won");
            _ui.Refresh(_session);
            BeginPurchaseResolve();
        }

        public void OnPurchaseCardDismiss()
        {
            // Purchase card removed — kept for UI bindings; advance is driven by ticket resolve.
            if (!_resolvingPurchase) return;
        }

        public void OnSkip()
        {
            if (_session == null || _resolvingPurchase) return;
            var state = _session.State;
            if (state == null) return;
            // Match MVP: ignore re-entry while already skipping / resolved (spam-safe).
            if (state.AwaitingLotStart || state.LotResolved || state.FastForwarding) return;
            if (IsPresentingLot) return;

            // Capture before BeginSkip / fast-forward mutates reveal progress.
            int revealStepBeforeSkip = state.RevealStep;
            int titleIndex = System.Array.IndexOf(CampaignConfig.RevealableFields, "title");
            bool titleAlreadySpoken = titleIndex >= 0 && revealStepBeforeSkip > titleIndex;

            if (!_session.BeginSkip()) return;

            AudioService.StopVoiceover();
            AudioService.PlaySkip();
            _timers.StartSkipFastReveal();
            // Skip: speak title only if it was not already revealed (and voiced).
            if (!titleAlreadySpoken && catalog != null && state.CurrentLot != null)
            {
                var data = catalog.FindPainting(state.CurrentLot.Id);
                AudioService.PlayVoiceField(data, PaintingVoiceField.Title);
            }
        }

        public void OnFinishDay()
        {
            if (_resolvingPurchase) return;
            _session?.FinishDayEarly();
        }

        public void OnReportContinue() => _session?.ContinueAfterReport();

        public void OnRestart()
        {
            EnsureSession();
            _session.StartCampaign();
        }

        public void OnSetLanguage(string lang)
        {
            LocaleService.SetLanguage(lang);
            _ui.Refresh(_session);
        }

        public void BuyUpgrade(string id)
        {
            AudioService.PlayUpgrade();
            _session?.BuyUpgrade(id);
        }

        public void BuyBooster(string id)
        {
            AudioService.PlayUpgrade();
            _session?.BuyBooster(id);
        }

        public void SelectBranch(string id)
        {
            AudioService.PlaySelect();
            _session?.SelectBranch(id);
        }

        void EnsureSession()
        {
            if (_session == null && catalog != null)
                BindSession(new GameSession(catalog));
            else if (_session?.State == null && catalog != null)
                BindSession(new GameSession(catalog));
        }

        void PresentCurrentLotVisual(System.Action onComplete = null)
        {
            if (_session?.State?.CurrentLot == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (_session.State.CurrentLotIndex >= _session.State.Lots.Count)
            {
                onComplete?.Invoke();
                return;
            }

            string id = _session.State.CurrentLot.Id;
            var data = catalog != null ? catalog.FindPainting(id) : null;

            if (presentation == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (data != null)
                presentation.PresentPaintingData(data, onComplete);
            else
                presentation.PresentPaintingById(id, onComplete);
        }

        void BeginPurchaseResolve()
        {
            if (_purchaseResolveRoutine != null)
                StopCoroutine(_purchaseResolveRoutine);
            _purchaseResolveRoutine = StartCoroutine(PurchaseResolveRoutine());
        }

        void CancelPurchaseResolve()
        {
            if (_purchaseResolveRoutine != null)
            {
                StopCoroutine(_purchaseResolveRoutine);
                _purchaseResolveRoutine = null;
            }

            _resolvingPurchase = false;
        }

        IEnumerator PurchaseResolveRoutine()
        {
            _resolvingPurchase = true;
            _ui?.Refresh(_session);
            presentation?.ExitInspectMode();

            var ticket = presentation != null ? presentation.Ticket : null;
            if (ticket != null)
            {
                yield return ticket.AppearAndWait();
                yield return new WaitForSeconds(1f);
            }
            else
            {
                yield return new WaitForSeconds(CampaignConfig.ResolutionPauseSeconds);
            }

            _resolvingPurchase = false;
            _purchaseResolveRoutine = null;
            _session?.AdvanceLot();
        }
    }
}
