using System.Collections.Generic;
using MasterBidder.Audio;
using MasterBidder.Campaign;
using MasterBidder.Content;
using MasterBidder.Core;
using MasterBidder.Flow;
using MasterBidder.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Runtime UI controller. Instantiates editable prefabs from Assets/content/ui/
    /// (generated via Master Bidder → Generate UI Prefabs) and binds game actions.
    /// Layout, spacing, and colors live on the prefabs — this class fills dynamic
    /// content, toggles visibility, applies state-driven tints, and assigns
    /// Playfair/Manrope typography via GameUiStyle.ApplyTypography.
    /// </summary>
    public class GameUiShell : MonoBehaviour
    {
        [SerializeField] GameObject gameUiPrefab;
        [SerializeField] GameObject collectorCardPrefab;
        [SerializeField] GameObject upgradeRowPrefab;
        [SerializeField] GameObject boosterRowPrefab;
        [SerializeField] GameObject purchaseTagPrefab;

        AppFlow _flow;
        GameUiBindings _b;
        Canvas _canvas;

        GameObject _intro, _brief, _auction, _report, _end;
        GameObject _collectorPopup, _purchaseCard, _tutorial;

        readonly List<GameObject> _collectorCards = new List<GameObject>();
        readonly List<GameObject> _upgradeRows = new List<GameObject>();
        readonly List<GameObject> _boosterRows = new List<GameObject>();
        readonly List<GameObject> _effectIcons = new List<GameObject>();

        float _fundsFlashUntil;
        bool _purchaseCardVisible;
        int _lastRaisedRival = -1;
        float _rivalRaiseUntil;
        bool _reportSoundPlayed;
        bool _briefShowUpgrades;
        bool _reportShowBoosters;
        readonly List<GameObject> _purchaseTags = new List<GameObject>();

        static readonly string[] FieldIds = { "genre", "period", "artist", "fact", "title" };

        public bool IsPurchaseCardVisible => _purchaseCardVisible;
        public bool IsCollectorPopupVisible => _collectorPopup != null && _collectorPopup.activeSelf;

        public void Bind(AppFlow flow) => _flow = flow;

        public void BuildIfNeeded()
        {
            if (_canvas != null) return;

            ResolvePrefabsIfNeeded();
            EnsureEventSystem();

            GameObject instance;
            if (gameUiPrefab != null)
            {
                instance = Instantiate(gameUiPrefab, transform);
                instance.name = "GameUI";
            }
            else
            {
                Debug.LogWarning(
                    "[GameUiShell] GameUI prefab missing — building default hierarchy. " +
                    "Run Master Bidder → Generate UI Prefabs to edit UI in the Inspector.");
                instance = GameUiHierarchyFactory.BuildGameUi();
                instance.transform.SetParent(transform, false);
            }

            _b = instance.GetComponent<GameUiBindings>();
            // Legacy uGUI Text prefabs lose wired TMP fields after migration — rebuild once.
            if (_b != null && _b.chromeTitle == null && _b.introTitle == null)
            {
                Debug.LogWarning(
                    "[GameUiShell] GameUI prefab has no TextMeshPro bindings — rebuilding. " +
                    "Run Master Bidder → Generate UI Prefabs to refresh Assets/content/ui.");
                Destroy(instance);
                instance = GameUiHierarchyFactory.BuildGameUi();
                instance.transform.SetParent(transform, false);
                instance.name = "GameUI";
                _b = instance.GetComponent<GameUiBindings>();
            }

            if (_b == null)
            {
                Debug.LogError("[GameUiShell] GameUI root is missing GameUiBindings.");
                Destroy(instance);
                return;
            }

            GameUiSprites.Warmup();
            _canvas = _b.canvas != null ? _b.canvas : instance.GetComponent<Canvas>();
            ApplyBindings(_b);
            GameUiStyle.ApplyTypography(_b);
            ResolveChromeBindings();
            if (_b.briefDay != null)
                GameUiStyle.ApplyUiFont(_b.briefDay, bold: true);
            ResolveEffectsHudRefs();
            ApplyBriefPanelMode();
            ApplyReportPanelMode();
            WireListeners();
        }

        /// <summary>
        /// Wire chrome status / effects refs without touching prefab layout or fonts.
        /// Creates missing nodes only as a fallback for broken/legacy prefabs.
        /// </summary>
        void ResolveChromeBindings()
        {
            if (_b == null) return;
            var chrome = EffectsChromeParent();
            if (chrome == null) return;

            HideChromeChild(chrome, "DayBar");
            HideChromeChild(chrome, "CapBar");
            if (_brief != null)
            {
                HideChromeChild(_brief.transform, "DayBar");
                HideChromeChild(_brief.transform, "CapBar");
            }

            Transform status = chrome.Find("ChromeStatus") ?? chrome.Find("BriefStatus");
            if (status == null)
            {
                var go = new GameObject("ChromeStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(chrome, false);
                status = go.transform;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-16f, 0f);
                rt.sizeDelta = new Vector2(360f, 38f);
                GameUiStyle.ApplySliced(go.GetComponent<Image>(), GameUiSprites.BarCurrency, GameUiStyle.SpriteReady);
            }
            else if (status.name == "BriefStatus")
            {
                status.name = "ChromeStatus";
            }

            var lang = chrome.Find("Lang");
            if (lang != null)
            {
                status.SetSiblingIndex(lang.GetSiblingIndex());
                lang.gameObject.SetActive(false);
            }

            TextMeshProUGUI statusText = status.Find("Status")?.GetComponent<TextMeshProUGUI>();
            if (statusText == null && _b.briefDay != null)
            {
                if (_b.briefDay.transform.parent != status)
                    _b.briefDay.transform.SetParent(status, false);
                _b.briefDay.gameObject.name = "Status";
                statusText = _b.briefDay;
            }
            if (statusText == null)
            {
                var tGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(status, false);
                statusText = tGo.GetComponent<TextMeshProUGUI>();
                GameUiStyle.ApplyUiFont(statusText, bold: true);
                statusText.fontSize = 15;
                statusText.color = GameUiStyle.OnDark;
                statusText.alignment = TextAlignmentOptions.Center;
                StretchUi(statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 0), new Vector2(-12, 0));
            }

            _b.briefDay = statusText;
            if (_b.briefCapital == null)
                _b.briefCapital = statusText;
            else if (_b.briefCapital != statusText)
                _b.briefCapital.gameObject.SetActive(false);

            if (_b.langDropdown != null)
                _b.langDropdown.gameObject.SetActive(false);

            status.gameObject.SetActive(true);
        }

        static void HideChromeChild(Transform parent, string name)
        {
            if (parent == null) return;
            var t = parent.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        void ApplyBriefPanelMode()
        {
            if (_b == null) return;

            Transform sidebar = _brief != null
                ? (_brief.transform.Find("Sidebar") ?? _brief.transform.Find("Workshop"))
                : null;
            var collectorScroll = sidebar != null ? sidebar.Find("CollectorScroll") : null;
            var upgradeScroll = sidebar != null ? sidebar.Find("UpgradeScroll") : null;

            if (collectorScroll != null) collectorScroll.gameObject.SetActive(!_briefShowUpgrades);
            if (upgradeScroll != null) upgradeScroll.gameObject.SetActive(_briefShowUpgrades);

            if (_b.briefClientHeading != null)
            {
                _b.briefClientHeading.text = LocaleService.T("brief.ordersHeading");
                _b.briefClientHeading.gameObject.SetActive(!_briefShowUpgrades);
            }
            if (_b.briefWorkshopHeading != null)
            {
                _b.briefWorkshopHeading.text = LocaleService.T("brief.workshop");
                _b.briefWorkshopHeading.gameObject.SetActive(_briefShowUpgrades);
            }

            if (_b.briefPanelToggleLabel != null)
                _b.briefPanelToggleLabel.text = _briefShowUpgrades
                    ? LocaleService.T("brief.toOrders")
                    : LocaleService.T("brief.toUpgrades");
        }

        void ToggleBriefPanelMode()
        {
            _briefShowUpgrades = !_briefShowUpgrades;
            ApplyBriefPanelMode();
        }

        void ApplyReportPanelMode()
        {
            if (_b == null || _report == null) return;
            var sidebar = _report.transform.Find("Sidebar");
            var purchaseScroll = sidebar != null ? sidebar.Find("PurchaseScroll") : null;
            var boosterScroll = sidebar != null ? sidebar.Find("BoosterScroll") : null;

            if (purchaseScroll != null) purchaseScroll.gameObject.SetActive(!_reportShowBoosters);
            if (boosterScroll != null) boosterScroll.gameObject.SetActive(_reportShowBoosters);

            if (_b.reportTitle != null)
            {
                _b.reportTitle.text = LocaleService.T("report.tagsHeading");
                _b.reportTitle.gameObject.SetActive(!_reportShowBoosters);
            }
            if (_b.boosterHeading != null)
            {
                _b.boosterHeading.text = LocaleService.T("report.boosters");
                _b.boosterHeading.gameObject.SetActive(_reportShowBoosters);
            }
            if (_b.reportPanelToggleLabel != null)
                _b.reportPanelToggleLabel.text = _reportShowBoosters
                    ? LocaleService.T("report.toTags")
                    : LocaleService.T("report.toBoosters");
        }

        void ToggleReportPanelMode()
        {
            _reportShowBoosters = !_reportShowBoosters;
            ApplyReportPanelMode();
        }

        static void StretchUi(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            if (rt == null) return;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        static GameObject SpawnUiWidget(GameObject prefab, Transform parent, System.Func<GameObject> build)
        {
            GameObject go = null;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, parent);
                // Legacy Text prefabs lose TMP field wiring after migration.
                if (go.GetComponentInChildren<TextMeshProUGUI>(true) == null)
                {
                    Object.Destroy(go);
                    go = null;
                }
            }

            if (go == null)
            {
                go = build();
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        void ResolvePrefabsIfNeeded()
        {
#if UNITY_EDITOR
            const string ui = "Assets/content/ui";
            if (gameUiPrefab == null)
                gameUiPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/GameUI.prefab");
            if (collectorCardPrefab == null)
                collectorCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/CollectorCard.prefab");
            if (upgradeRowPrefab == null)
                upgradeRowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/UpgradeRow.prefab");
            if (boosterRowPrefab == null)
                boosterRowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/BoosterRow.prefab");
            if (purchaseTagPrefab == null)
                purchaseTagPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/PurchaseTag.prefab");
#endif
        }

        void ApplyBindings(GameUiBindings b)
        {
            _intro = b.intro;
            _brief = b.brief;
            _auction = b.auction;
            _report = b.report;
            _end = b.end;
            _collectorPopup = b.collectorPopup;
            _purchaseCard = b.purchaseCard;
            _tutorial = b.tutorial;

            if (_collectorPopup != null) _collectorPopup.SetActive(false);
            if (_purchaseCard != null) _purchaseCard.SetActive(false);
            if (_tutorial != null) _tutorial.SetActive(false);
        }

        void WireListeners()
        {
            if (_b.btnContinue != null)
                _b.btnContinue.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnContinueCareer(); });
            if (_b.btnStart != null)
                _b.btnStart.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnStartCareer(); });
            if (_b.btnReset != null)
                _b.btnReset.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnResetProgress(); });
            if (_b.btnBriefPanelToggle != null)
                _b.btnBriefPanelToggle.onClick.AddListener(() => { AudioService.PlayClick(); ToggleBriefPanelMode(); });
            if (_b.btnEnterHall != null)
                _b.btnEnterHall.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnEnterHall(); });
            if (_b.btnStartLot != null)
                _b.btnStartLot.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnStartLot(); });
            if (_b.btnBuy != null)
                _b.btnBuy.onClick.AddListener(() => _flow?.OnBuy());
            if (_b.btnSkip != null)
                _b.btnSkip.onClick.AddListener(() => _flow?.OnSkip());
            if (_b.btnFinishDay != null)
                _b.btnFinishDay.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnFinishDay(); });
            if (_b.btnPopupStart != null)
                _b.btnPopupStart.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnCollectorPopupStart(); });
            if (_b.btnPcContinue != null)
                _b.btnPcContinue.onClick.AddListener(() => _flow?.OnPurchaseCardDismiss());
            if (_b.btnReportContinue != null)
                _b.btnReportContinue.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnReportContinue(); });
            if (_b.btnReportPanelToggle != null)
                _b.btnReportPanelToggle.onClick.AddListener(() => { AudioService.PlayClick(); ToggleReportPanelMode(); });
            if (_b.btnRestart != null)
                _b.btnRestart.onClick.AddListener(() => { AudioService.PlayClick(); _flow?.OnRestart(); });

            if (_b.langDropdown != null)
            {
                _b.langDropdown.value = LocaleService.Language == "en" ? 1 : 0;
                _b.langDropdown.onValueChanged.AddListener(i => _flow?.OnSetLanguage(i == 1 ? "en" : "ru"));
            }
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        public void ShowScreen(GameScreen screen)
        {
            if (_intro) _intro.SetActive(screen == GameScreen.Intro);
            if (_brief) _brief.SetActive(screen == GameScreen.Brief);
            if (_auction) _auction.SetActive(screen == GameScreen.Auction);
            if (_report) _report.SetActive(screen == GameScreen.Report);
            if (_end) _end.SetActive(screen == GameScreen.End);
            if (screen != GameScreen.Auction)
            {
                HideCollectorPopup();
                HidePurchaseCard();
                if (_tutorial) _tutorial.SetActive(false);
                HideEffectTooltip();
            }

            // Shared Chrome: title + effects + day/capital. Same on every gameplay screen.
            var chrome = EffectsChromeParent();
            ResolveChromeBindings();

            var chromeEffects = chrome != null ? chrome.Find("EffectsHud") : null;
            bool gameplay = screen == GameScreen.Brief || screen == GameScreen.Auction
                            || screen == GameScreen.Report;
            if (chromeEffects != null)
                chromeEffects.gameObject.SetActive(gameplay);

            var chromeStatus = chrome != null
                ? (chrome.Find("ChromeStatus") ?? chrome.Find("BriefStatus"))
                : null;
            if (chromeStatus != null)
                chromeStatus.gameObject.SetActive(gameplay);

            if (_b.langDropdown != null)
                _b.langDropdown.gameObject.SetActive(false);
            var lang = chrome != null ? chrome.Find("Lang") : null;
            if (lang != null) lang.gameObject.SetActive(false);

            if (_b.chromeTitle != null)
                _b.chromeTitle.gameObject.SetActive(true);

            if (screen == GameScreen.End)
                AudioService.PlayCampaignEnd();

            if (screen != GameScreen.Report)
                _reportSoundPlayed = false;

            // Shared ActiveClient lives outside screen roots; hide on intro/end only.
            bool showActiveClientChrome = screen == GameScreen.Brief
                                          || screen == GameScreen.Auction
                                          || screen == GameScreen.Report;
            if (!showActiveClientChrome && _b?.activeClient != null)
                _b.activeClient.SetActive(false);
        }

        public void ShowCollectorPopup(DayOrder order, CollectorData collector = null)
        {
            if (_collectorPopup == null || order == null || _b == null) return;
            _b.popupName.text = order.NameRu;
            _b.popupTagline.text = order.TaglineRu ?? "";
            _b.popupSpeech.text = LocaleService.T("collectorPopup.speech");
            _b.popupTags.text = order.CriteriaLabel;
            _b.popupWarning.text = LocaleService.T("collectorPopup.warning");
            _b.popupStartLabel.text = LocaleService.T("collectorPopup.start");
            if (_b.popupPortrait != null)
            {
                if (collector?.portrait != null)
                {
                    _b.popupPortrait.sprite = collector.portrait;
                    _b.popupPortrait.color = Color.white;
                }
                else
                {
                    _b.popupPortrait.sprite = null;
                    _b.popupPortrait.color = GameUiStyle.PanelLight;
                }
            }
            _collectorPopup.SetActive(true);
        }

        public void RaiseRandomRival()
        {
            if (_b?.rivalHeads == null || _b.rivalHeads.Length == 0) return;
            ResetRivalHeads();
            int idx = Random.Range(0, _b.rivalHeads.Length);
            _lastRaisedRival = idx;
            _rivalRaiseUntil = Time.unscaledTime + 1.6f;
            if (_b.rivalHeads[idx] != null)
                _b.rivalHeads[idx].color = GameUiStyle.Accent;
        }

        void ResetRivalHeads()
        {
            if (_b?.rivalHeads == null) return;
            for (int i = 0; i < _b.rivalHeads.Length; i++)
            {
                if (_b.rivalHeads[i] != null)
                    _b.rivalHeads[i].color = GameUiStyle.RivalIdle;
            }
            _lastRaisedRival = -1;
        }

        public void HideCollectorPopup()
        {
            if (_collectorPopup != null) _collectorPopup.SetActive(false);
        }

        public void ShowPurchaseCard(PresentedLot lot, int price)
        {
            if (_purchaseCard == null || lot == null || _b == null) return;
            _b.pcTitle.text = lot.TitleRu;
            _b.pcArtist.text = lot.ArtistRu;
            _b.pcMeta.text =
                $"{LocaleService.T("auction.field.period")}: {lot.PeriodRu}\n" +
                $"{LocaleService.T("auction.field.genre")}: {lot.GenreRu}\n" +
                $"{LocaleService.T("rarity." + GameCatalog.RarityToString(lot.Rarity))}\n" +
                $"{price:N0} ₽";
            _b.pcFact.text = lot.FactRu;
            _b.pcContinueLabel.text = LocaleService.T("purchase.continue");
            _purchaseCard.SetActive(true);
            _purchaseCardVisible = true;
        }

        public void HidePurchaseCard()
        {
            if (_purchaseCard != null) _purchaseCard.SetActive(false);
            _purchaseCardVisible = false;
        }

        public void FlashInsufficientFunds()
        {
            _fundsFlashUntil = Time.unscaledTime + 1.4f;
            if (_b?.fundsHint != null)
            {
                _b.fundsHint.text = LocaleService.T("auction.insufficient");
                _b.fundsHint.color = GameUiStyle.Bad;
                SetToastActive(_b.fundsHint, true);
            }
        }

        static void SetToastActive(TextMeshProUGUI label, bool active)
        {
            if (label == null) return;
            var root = label.transform.parent != null &&
                       (label.transform.parent.name == "BannerBg" || label.transform.parent.name == "FundsBg")
                ? label.transform.parent.gameObject
                : label.gameObject;
            root.SetActive(active);
        }

        static void StyleResultToast(TextMeshProUGUI label, string result)
        {
            if (label == null) return;
            var bg = label.transform.parent != null ? label.transform.parent.GetComponent<Image>() : null;
            if (result == "won")
            {
                label.color = GameUiStyle.Good;
                if (bg != null) GameUiStyle.ApplySliced(bg, GameUiSprites.ToastSuccess);
            }
            else if (result == "lost")
            {
                label.color = GameUiStyle.Bad;
                if (bg != null) GameUiStyle.ApplySliced(bg, GameUiSprites.ToastError);
            }
            else
            {
                label.color = GameUiStyle.Dim;
                if (bg != null) GameUiStyle.ApplySliced(bg, GameUiSprites.ToastWarn);
            }
        }

        public void Refresh(GameSession session)
        {
            if (_b == null) return;
            if (_b.chromeTitle) _b.chromeTitle.text = LocaleService.T("chrome.title");
            RefreshChromeStatus(session);
            RefreshIntro(session);
            RefreshBrief(session);
            RefreshAuction(session);
            RefreshReport(session);
            RefreshEnd(session);
        }

        void RefreshChromeStatus(GameSession session)
        {
            if (_b?.briefDay == null) return;
            var state = session?.State;
            int day = state?.Day ?? 1;
            int capital = state?.Capital ?? CampaignConfig.StartingCapital;
            _b.briefDay.text =
                $"{LocaleService.T("brief.day")} {day} / {CampaignConfig.CampaignLength}" +
                $"   ·   {LocaleService.T("brief.capital")} {capital:N0} ₽";
            if (_b.briefCapital != null && _b.briefCapital != _b.briefDay)
                _b.briefCapital.gameObject.SetActive(false);
        }

        void RefreshIntro(GameSession session)
        {
            if (_b?.introTitle == null) return;
            _b.introTitle.text = LocaleService.T("intro.title");
            _b.introSubtitle.text = LocaleService.T("intro.subtitle");
            _b.introLede.text = LocaleService.T("intro.lede");
            _b.introRules.text = "• " + LocaleService.T("intro.rule1") + "\n• " + LocaleService.T("intro.rule2")
                               + "\n• " + LocaleService.T("intro.rule3") + "\n• " + LocaleService.T("intro.rule4");
            bool hasSave = SaveService.HasSave();
            _b.btnContinue.gameObject.SetActive(hasSave);
            _b.continueLabel.text = LocaleService.T("intro.continue");
            _b.startLabel.text = hasSave ? LocaleService.T("intro.newCareer") : LocaleService.T("intro.start");
        }

        void RefreshBrief(GameSession session)
        {
            if (_b?.briefDay == null) return;

            if (_b.enterLabel != null) _b.enterLabel.text = LocaleService.T("brief.enterHall");
            if (_b.resetLabel != null) _b.resetLabel.text = LocaleService.T("brief.resetProgress");
            ApplyBriefPanelMode();

            var state = session?.State;
            bool hasOrder = state?.PendingOrder != null && !string.IsNullOrEmpty(state.SelectedBranchId);
            if (_b.btnEnterHall != null)
            {
                _b.btnEnterHall.interactable = hasOrder;
                GameUiStyle.SetSpriteEnabled(_b.btnEnterHall.GetComponent<Image>(), hasOrder);
            }

            RefreshActiveClientPanel(session);
            RebuildCollectorCards(session);
            RebuildUpgradeRows(session);
            RebuildActiveEffects(state);
        }

        void RefreshActiveClientPanel(GameSession session, DayOrder orderOverride = null)
        {
            if (_b?.activeClient == null) return;
            var state = session?.State;
            var catalog = _flow?.Catalog;
            var order = orderOverride ?? state?.PendingOrder;

            CollectorData collector = null;
            if (order != null && catalog != null)
                collector = catalog.FindCollector(order.CollectorId);
            if (collector == null && state != null && catalog != null)
                collector = catalog.FindCollector(state.SelectedBranchId);

            // Auction passes an order override and shows whenever a day order exists.
            // Brief requires both a selected collector and a pending order.
            bool show = orderOverride != null
                ? order != null
                : collector != null && order != null;

            _b.activeClient.SetActive(show);
            if (!show) return;

            if (_b.activeName != null)
                _b.activeName.text = collector?.nameRu ?? order.NameRu ?? "";

            if (_b.activeTags != null)
                _b.activeTags.text = order.CriteriaLabel ?? "";

            if (_b.activePortrait != null)
            {
                if (collector?.portrait != null)
                {
                    _b.activePortrait.sprite = collector.portrait;
                    _b.activePortrait.color = Color.white;
                    _b.activePortrait.enabled = true;
                }
                else
                {
                    _b.activePortrait.sprite = null;
                    _b.activePortrait.color = GameUiStyle.PanelLight;
                }
            }
        }

        void RebuildCollectorCards(GameSession session)
        {
            if (_b?.collectorList == null) return;
            ClearList(_collectorCards, _b.collectorList);
            var catalog = _flow?.Catalog;
            if (catalog?.collectors == null || session?.State == null) return;
            string selected = session.State.SelectedBranchId;
            foreach (var c in catalog.collectors)
            {
                if (c == null) continue;
                int progress = session.State.BranchProgress.TryGetValue(c.collectorId, out int p) ? p : 0;
                bool isSelected = c.collectorId == selected;
                var go = SpawnUiWidget(collectorCardPrefab, _b.collectorList, GameUiHierarchyFactory.BuildCollectorCard);
                go.name = "C_" + c.collectorId;
                var view = go.GetComponent<CollectorCardView>();
                if (view == null) continue;
                GameUiStyle.ApplyWidgetTypography(view);

                if (view.background != null)
                {
                    if (view.background.sprite != null)
                        view.background.color = isSelected ? GameUiStyle.SelectedTint : GameUiStyle.SpriteReady;
                    else
                        view.background.color = isSelected
                            ? new Color(GameUiStyle.Accent.r, GameUiStyle.Accent.g, GameUiStyle.Accent.b, 0.35f)
                            : GameUiStyle.PanelLight;
                }

                if (view.portraitRoot != null)
                    view.portraitRoot.SetActive(c.portrait != null);
                if (c.portrait != null && view.portrait != null)
                {
                    view.portrait.sprite = c.portrait;
                    view.portrait.color = Color.white;
                }

                if (view.label != null)
                    view.label.text = $"{c.nameRu}\n{LocaleService.T("brief.mission")} {progress + 1}/{c.LadderLength}";

                if (view.button != null)
                {
                    view.button.onClick.RemoveAllListeners();
                    string id = c.collectorId;
                    view.button.onClick.AddListener(() => _flow?.SelectBranch(id));
                }

                _collectorCards.Add(go);
            }
        }

        void RebuildUpgradeRows(GameSession session)
        {
            if (_b?.upgradeList == null) return;
            ClearList(_upgradeRows, _b.upgradeList);
            if (session?.State == null) return;
            foreach (var u in CampaignConfig.MetaUpgrades)
            {
                bool owned = session.State.Upgrades.Contains(u.Id);
                bool canBuy = !owned && session.State.Capital >= u.Cost;
                var go = SpawnUiWidget(upgradeRowPrefab, _b.upgradeList, GameUiHierarchyFactory.BuildUpgradeRow);
                go.name = "U_" + u.Id;
                var view = go.GetComponent<UpgradeRowView>();
                if (view == null) continue;
                GameUiStyle.ApplyWidgetTypography(view);

                EnsureRowIcon(view, go.transform, 48f);
                if (view.icon != null)
                {
                    view.icon.sprite = GameUiMetaIcons.Get(u.Id);
                    view.icon.enabled = view.icon.sprite != null;
                    view.icon.color = owned ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                }

                if (view.label != null)
                {
                    view.label.text = $"{u.NameRu} — {u.Cost:N0} ₽\n{u.DescRu}";
                    view.label.color = owned ? GameUiStyle.Dim : GameUiStyle.TextColor;
                }

                if (view.buyLabel != null)
                    view.buyLabel.text = owned ? LocaleService.T("brief.owned") : LocaleService.T("brief.buy");
                if (view.buyButton != null)
                {
                    view.buyButton.interactable = canBuy;
                    GameUiStyle.SetSpriteEnabled(view.buyButton.GetComponent<Image>(), canBuy);
                    view.buyButton.onClick.RemoveAllListeners();
                    string uid = u.Id;
                    view.buyButton.onClick.AddListener(() => _flow?.BuyUpgrade(uid));
                }

                _upgradeRows.Add(go);
            }
        }

        void RefreshAuction(GameSession session)
        {
            if (_b?.aucHud == null) return;
            var state = session?.State;
            if (state == null) return;

            var venue = CampaignConfig.GetVenue(state.CurrentVenue);
            _b.aucHud.text =
                $"{LocaleService.T("auction.day")} {state.Day} · {LocaleService.T("auction.venue")} {venue.LabelRu} · " +
                $"{LocaleService.T("auction.lot")} {state.CurrentLotIndex + 1}/{state.Lots.Count}";

            var order = state.DayOrders.Count > 0 ? state.DayOrders[0] : state.PendingOrder;
            RefreshActiveClientPanel(session, order);

            var lot = state.CurrentLot;
            int price = lot != null
                ? AuctionRules.ComputeLivePrice(lot, state.RevealStep, AuctionRules.GetPriceStepPct(state))
                : 0;
            if (_b.livePrice != null)
                _b.livePrice.text = $"{price:N0} ₽";
            if (_b.liveBudget != null)
                _b.liveBudget.text = $"{LocaleService.T("auction.budgetLeft")}: {state.ClientBudgetRemaining:N0} ₽";
            if (_b.liveSpeed != null)
                _b.liveSpeed.gameObject.SetActive(false);
            if (_b.orderCard != null)
                _b.orderCard.gameObject.SetActive(false);

            RefreshRevealFields(state, lot, order);
            if (_b.familiarBadge != null)
            {
                _b.familiarBadge.gameObject.SetActive(lot != null && lot.Familiar);
                _b.familiarBadge.text = LocaleService.T("auction.familiar");
            }

            RebuildActiveEffects(state);

            bool showBanner = !string.IsNullOrEmpty(state.LastLotResult);
            SetToastActive(_b.resultBanner, showBanner);
            if (showBanner)
            {
                if (state.LastLotResult == "won")
                    _b.resultBanner.text = LocaleService.T("auction.won");
                else if (state.LastLotResult == "lost")
                    _b.resultBanner.text = LocaleService.T("auction.lost");
                else
                    _b.resultBanner.text = LocaleService.T("auction.skip");
                StyleResultToast(_b.resultBanner, state.LastLotResult);
            }

            if (_b.fundsHint != null && Time.unscaledTime > _fundsFlashUntil)
                SetToastActive(_b.fundsHint, false);

            bool standby = state.AwaitingLotStart || IsCollectorPopupVisible;
            bool presenting = _flow != null && _flow.IsPresentingLot;
            bool resolvingPurchase = _flow != null && _flow.IsResolvingPurchase;
            bool busy = state.LotResolved || state.FastForwarding || resolvingPurchase || presenting;
            var day1Tut = session.GetDay1TutorialStep(state.CurrentLotIndex);
            // Match session gates: coaching lots only unlock the taught action after the coach appears.
            bool buyAllowed = day1Tut == TutorialStep.None
                || (day1Tut == TutorialStep.BuyMatch && state.TutorialPaused && state.TutorialStep == TutorialStep.BuyMatch);
            bool skipAllowed = day1Tut == TutorialStep.None
                || (day1Tut == TutorialStep.SkipMiss && state.TutorialPaused && state.TutorialStep == TutorialStep.SkipMiss);
            _b.btnStartLot.gameObject.SetActive(state.AwaitingLotStart && !IsCollectorPopupVisible);
            _b.btnSkip.gameObject.SetActive(!state.AwaitingLotStart);
            _b.btnFinishDay.gameObject.SetActive(!state.AwaitingLotStart);
            _b.btnBuy.gameObject.SetActive(!state.AwaitingLotStart);
            _b.btnBuy.interactable = !standby && !busy && buyAllowed;
            _b.btnSkip.interactable = !standby && !busy && skipAllowed;
            _b.btnFinishDay.interactable = !state.TutorialPaused && !busy;

            _b.startLotLabel.text = LocaleService.T("auction.startLot");
            _b.buyLabel.text = LocaleService.T("auction.buy");
            _b.skipLabel.text = LocaleService.T("auction.skip");
            _b.finishLabel.text = LocaleService.T("auction.finishDay");

            bool showTut = state.TutorialPaused && state.TutorialStep != TutorialStep.None;
            if (_tutorial != null)
            {
                _tutorial.SetActive(showTut && _auction.activeSelf);
                if (showTut)
                {
                    _b.tutorialText.text = state.TutorialStep == TutorialStep.BuyMatch
                        ? LocaleService.T("tutorial.buyMatch")
                        : LocaleService.T("tutorial.skipMiss");
                }
            }
        }

        void RefreshRevealFields(GameState state, PresentedLot lot, DayOrder order)
        {
            for (int i = 0; i < 5; i++)
            {
                string id = FieldIds[i];
                _b.fieldLabels[i].text = LocaleService.T("auction.field." + id);
                string raw = lot == null ? "—" : FieldValue(lot, id);
                bool revealed = state.RevealStep > i
                                || (!string.IsNullOrEmpty(state.FreeRevealedField) && state.FreeRevealedField == id);
                _b.fieldValues[i].text = revealed ? raw : AuctionRules.MaskValue(raw);
                _b.fieldValues[i].color = revealed ? GameUiStyle.TextColor : GameUiStyle.Dim;
                if (revealed)
                    GameUiStyle.ApplyUiFont(_b.fieldValues[i], bold: true);
                else
                    GameUiStyle.ApplyUiFont(_b.fieldValues[i]);

                bool isTarget = order != null && IsOrderTarget(order, id);
                if (isTarget)
                    _b.fieldRows[i].color = new Color(GameUiStyle.Accent.r, GameUiStyle.Accent.g, GameUiStyle.Accent.b, revealed ? 0.34f : 0.22f);
                else if (revealed)
                    _b.fieldRows[i].color = new Color(0.2f, 0.16f, 0.12f, 0.12f);
                else
                    _b.fieldRows[i].color = i % 2 == 0
                        ? new Color(0.2f, 0.16f, 0.12f, 0.08f)
                        : new Color(0.2f, 0.16f, 0.12f, 0.03f);
            }
        }

        static string FieldValue(PresentedLot lot, string id)
        {
            switch (id)
            {
                case "genre": return lot.GenreRu;
                case "period": return lot.PeriodRu;
                case "artist": return lot.ArtistRu;
                case "fact": return lot.FactRu;
                case "title": return lot.TitleRu;
                default: return "—";
            }
        }

        static bool IsOrderTarget(DayOrder order, string fieldId)
        {
            if (order?.CriteriaTags == null) return false;
            for (int i = 0; i < order.CriteriaTags.Length; i++)
            {
                var t = order.CriteriaTags[i];
                if (t.Type == CriterionType.Artwork && fieldId == "title") return true;
                if (t.Type == CriterionType.Genre && fieldId == "genre") return true;
                if (t.Type == CriterionType.Period && fieldId == "period") return true;
                if (t.Type == CriterionType.Artist && fieldId == "artist") return true;
            }
            return false;
        }

        void RefreshReport(GameSession session)
        {
            if (_b == null) return;

            var state = session?.State;
            var r = state?.PendingResult;
            if (r == null)
            {
                if (_b.reportStamp != null) _b.reportStamp.SetActive(false);
                ClearList(_purchaseTags, _b.purchaseTagList);
                return;
            }

            if (!_reportSoundPlayed)
            {
                _reportSoundPlayed = true;
                if (r.Pass) AudioService.PlayDayPass();
                else AudioService.PlayDayFail();
            }

            // Client stamp on the room floor.
            if (_b.reportStamp != null) _b.reportStamp.SetActive(true);
            if (_b.reportStampLabel != null)
            {
                _b.reportStampLabel.text = LocaleService.T(r.OrdersFulfilled ? "report.stampOk" : "report.stampBad");
                _b.reportStampLabel.color = r.OrdersFulfilled ? GameUiStyle.Good : GameUiStyle.Bad;
            }
            if (_b.reportStampDetail != null)
            {
                string line = r.OrdersFulfilled
                    ? LocaleService.T("report.fulfilled")
                    : LocaleService.T("report.unfulfilled");
                if (r.SavedByCreditLine) line += "\n" + LocaleService.T("report.creditLine");
                line += $"\n{LocaleService.T("report.commission")} {r.TotalCommission:N0} ₽";
                _b.reportStampDetail.text = line;
            }

            RebuildPurchaseTags(r);
            bool showBoosters = r.Pass && state.Day < CampaignConfig.CampaignLength;
            if (!showBoosters) _reportShowBoosters = false;
            RebuildBoosterRows(session, showBoosters);
            if (_b.btnReportPanelToggle != null)
                _b.btnReportPanelToggle.gameObject.SetActive(showBoosters);
            ApplyReportPanelMode();

            if (_b.reportContinueLabel != null)
            {
                _b.reportContinueLabel.text = r.Pass
                    ? LocaleService.T("report.continue")
                    : LocaleService.T("report.finish");
            }
        }

        void RebuildPurchaseTags(SettlementResult r)
        {
            ClearList(_purchaseTags, _b.purchaseTagList);
            if (_b.purchaseTagList == null || r == null) return;

            if (r.PurchaseDetails == null || r.PurchaseDetails.Length == 0)
            {
                var empty = SpawnUiWidget(purchaseTagPrefab, _b.purchaseTagList, GameUiHierarchyFactory.BuildPurchaseTag);
                var view = empty.GetComponent<PurchaseTagView>();
                if (view != null)
                {
                    GameUiStyle.ApplyWidgetTypography(view);
                    if (view.title != null) view.title.text = LocaleService.T("report.noPurchases");
                    if (view.meta != null) view.meta.text = "";
                    if (view.stamp != null) view.stamp.text = "";
                }
                _purchaseTags.Add(empty);
                return;
            }

            for (int i = 0; i < r.PurchaseDetails.Length; i++)
            {
                var d = r.PurchaseDetails[i];
                var go = SpawnUiWidget(purchaseTagPrefab, _b.purchaseTagList, GameUiHierarchyFactory.BuildPurchaseTag);
                go.name = "Tag_" + i;
                var view = go.GetComponent<PurchaseTagView>();
                if (view == null) continue;
                GameUiStyle.ApplyWidgetTypography(view);

                if (view.title != null)
                {
                    view.title.text = d.TitleRu ?? "";
                    view.title.color = GameUiStyle.TextColor;
                }
                if (view.meta != null)
                {
                    view.meta.text = $"{d.Price:N0} ₽  ·  {LocaleService.T("report.commission")} {d.Amount:N0} ₽";
                    if (!string.IsNullOrEmpty(d.Reason))
                        view.meta.text += "\n" + d.Reason;
                }
                if (view.stamp != null)
                {
                    view.stamp.text = d.Matched
                        ? LocaleService.T("report.correct")
                        : LocaleService.T("report.incorrect");
                    view.stamp.color = d.Matched ? GameUiStyle.Good : GameUiStyle.Bad;
                }
                if (view.background != null)
                {
                    if (view.background.sprite != null)
                        view.background.color = d.Matched ? GameUiStyle.SelectedTint : GameUiStyle.SpriteReady;
                    else
                        view.background.color = d.Matched
                            ? new Color(GameUiStyle.Good.r, GameUiStyle.Good.g, GameUiStyle.Good.b, 0.12f)
                            : new Color(GameUiStyle.Bad.r, GameUiStyle.Bad.g, GameUiStyle.Bad.b, 0.1f);
                }

                _purchaseTags.Add(go);
            }
        }

        void RebuildBoosterRows(GameSession session, bool show)
        {
            ClearList(_boosterRows, _b.boosterList);
            if (_b.boosterHeading != null)
                _b.boosterHeading.text = show ? LocaleService.T("report.boosters") : "";
            if (!show || session?.State == null) return;
            foreach (var id in session.State.BoosterOffers)
            {
                BoosterDef def = null;
                for (int i = 0; i < CampaignConfig.Boosters.Length; i++)
                {
                    if (CampaignConfig.Boosters[i].Id == id) { def = CampaignConfig.Boosters[i]; break; }
                }
                if (def == null) continue;
                int cost = AuctionRules.GetBoosterCost(def, session.State);
                bool owned = session.State.PendingBoosters.Contains(id);
                bool canBuy = !owned && session.State.Capital >= cost
                              && session.State.PendingBoosters.Count < CampaignConfig.GetMaxDailyBoosters(
                                  session.State.Upgrades.Contains("personal-secretary"));

                var go = SpawnUiWidget(boosterRowPrefab, _b.boosterList, GameUiHierarchyFactory.BuildBoosterRow);
                go.name = "B_" + id;
                var view = go.GetComponent<BoosterRowView>();
                if (view == null) continue;
                GameUiStyle.ApplyWidgetTypography(view);

                EnsureRowIcon(view, go.transform, 56f);
                if (view.icon != null)
                {
                    view.icon.sprite = GameUiMetaIcons.Get(id);
                    view.icon.enabled = view.icon.sprite != null;
                    view.icon.color = owned ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                }

                if (view.label != null)
                    view.label.text = $"{def.NameRu} — {cost:N0} ₽\n{def.DescRu}";

                if (view.buyLabel != null)
                    view.buyLabel.text = owned ? LocaleService.T("report.ownedBooster") : LocaleService.T("report.buyBooster");
                if (view.buyButton != null)
                {
                    view.buyButton.interactable = canBuy;
                    GameUiStyle.SetSpriteEnabled(view.buyButton.GetComponent<Image>(), canBuy);
                    view.buyButton.onClick.RemoveAllListeners();
                    string bid = id;
                    view.buyButton.onClick.AddListener(() => _flow?.BuyBooster(bid));
                }

                _boosterRows.Add(go);
            }
        }

        void RefreshEnd(GameSession session)
        {
            if (_b?.endTitle == null) return;
            var state = session?.State;
            bool bankrupt = state?.PendingResult != null && !state.PendingResult.Pass;
            _b.endTitle.text = bankrupt ? LocaleService.T("end.bankruptTitle") : LocaleService.T("end.careerTitle");
            _b.restartLabel.text = LocaleService.T("end.restart");
        }

        void Update()
        {
            if (_flow == null) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (_flow.IsResolvingPurchase)
                    return;
                if (IsCollectorPopupVisible)
                {
                    _flow.OnCollectorPopupStart();
                    return;
                }
                if (_auction != null && _auction.activeSelf)
                {
                    var state = _flow.Session?.State;
                    if (state == null) return;
                    if (state.AwaitingLotStart) _flow.OnStartLot();
                    else _flow.OnBuy();
                }
            }

            if (_lastRaisedRival >= 0 && Time.unscaledTime > _rivalRaiseUntil)
                ResetRivalHeads();
        }

        Transform EffectsChromeParent()
        {
            if (_b == null) return null;
            var chrome = _b.transform.Find("Chrome");
            return chrome;
        }

        /// <summary>Bind effects HUD refs from the prefab; create only if missing.</summary>
        void ResolveEffectsHudRefs()
        {
            if (_b == null) return;
            var chrome = EffectsChromeParent();
            if (chrome == null) return;

            // Migrate leftover strip from older auction layout.
            if (_auction != null)
            {
                var stale = _auction.transform.Find("EffectsHud");
                if (stale != null) Destroy(stale.gameObject);
                var staleTip = _auction.transform.Find("EffectTooltip");
                if (staleTip != null && (_b.effectTooltip == null || _b.effectTooltip.transform != staleTip))
                    Destroy(staleTip.gameObject);
            }

            var bar = chrome.Find("EffectsHud");
            if (bar == null)
            {
                var go = new GameObject("EffectsHud", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(RectMask2D));
                go.transform.SetParent(chrome, false);
                go.transform.SetSiblingIndex(Mathf.Min(1, chrome.childCount));
                StretchUi(go.GetComponent<RectTransform>(), new Vector2(0.28f, 0.12f), new Vector2(0.76f, 0.88f), Vector2.zero, Vector2.zero);
                var bg = go.GetComponent<Image>();
                bg.sprite = null;
                bg.color = new Color(1f, 1f, 1f, 0f);
                bg.raycastTarget = false;
                var hlg = go.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(4, 4, 0, 0);
                hlg.spacing = 8;
                hlg.childAlignment = TextAnchor.MiddleRight;
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                bar = go.transform;
            }

            if (_b.effectsUpgrades == null)
            {
                var t = bar.Find("Upgrades");
                _b.effectsUpgrades = t != null ? t : CreateEffectsRowRuntime(bar, "Upgrades");
            }

            if (_b.effectsBoosters == null)
            {
                var t = bar.Find("Boosters");
                _b.effectsBoosters = t != null ? t : CreateEffectsRowRuntime(bar, "Boosters");
            }

            if (_b.effectTooltip == null)
            {
                var tipT = _b.transform.Find("EffectTooltip");
                if (tipT != null)
                {
                    _b.effectTooltip = tipT.gameObject;
                    if (_b.effectTooltipTitle == null)
                        _b.effectTooltipTitle = tipT.Find("Title")?.GetComponent<TextMeshProUGUI>();
                    if (_b.effectTooltipBody == null)
                        _b.effectTooltipBody = tipT.Find("Body")?.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        static Transform CreateEffectsRowRuntime(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 4;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var fit = go.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 28;
            le.flexibleHeight = 0;
            return go.transform;
        }

        void RebuildActiveEffects(GameState state)
        {
            ResolveEffectsHudRefs();
            _effectIcons.Clear();
            HideEffectTooltip();
            if (state == null || _b == null) return;

            ClearChildrenImmediate(_b.effectsUpgrades);
            ClearChildrenImmediate(_b.effectsBoosters);

            int upgradeCount = 0;
            int boosterCount = 0;

            if (_b.effectsUpgrades != null)
            {
                for (int i = 0; i < CampaignConfig.MetaUpgrades.Length; i++)
                {
                    var u = CampaignConfig.MetaUpgrades[i];
                    if (!state.Upgrades.Contains(u.Id)) continue;
                    SpawnEffectIcon(_b.effectsUpgrades, u.Id, u.NameRu, u.DescRu);
                    upgradeCount++;
                }
                _b.effectsUpgrades.gameObject.SetActive(upgradeCount > 0);
            }

            if (_b.effectsBoosters != null)
            {
                for (int i = 0; i < CampaignConfig.Boosters.Length; i++)
                {
                    var bo = CampaignConfig.Boosters[i];
                    if (!state.ActiveBoosters.Contains(bo.Id)) continue;
                    SpawnEffectIcon(_b.effectsBoosters, bo.Id, bo.NameRu, bo.DescRu);
                    boosterCount++;
                }
                _b.effectsBoosters.gameObject.SetActive(boosterCount > 0);
            }

            var chrome = EffectsChromeParent();
            var bar = chrome != null ? chrome.Find("EffectsHud") : null;
            if (bar != null)
                bar.gameObject.SetActive(upgradeCount + boosterCount > 0);
        }

        static void ClearChildrenImmediate(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        void SpawnEffectIcon(Transform parent, string id, string title, string body)
        {
            if (parent == null) return;
            var go = GameUiHierarchyFactory.BuildEffectIcon();
            go.transform.SetParent(parent, false);
            go.name = "E_" + id;
            var view = go.GetComponent<EffectIconView>();
            if (view != null)
            {
                view.Title = title ?? "";
                view.Body = body ?? "";
                if (view.icon != null)
                {
                    view.icon.sprite = GameUiMetaIcons.Get(id);
                    view.icon.enabled = view.icon.sprite != null;
                }
                view.OnHover = ShowEffectTooltip;
                view.OnLeave = HideEffectTooltip;
            }
            _effectIcons.Add(go);
        }

        void ShowEffectTooltip(EffectIconView view)
        {
            if (view == null || _b?.effectTooltip == null) return;
            if (_b.effectTooltipTitle != null) _b.effectTooltipTitle.text = view.Title;
            if (_b.effectTooltipBody != null) _b.effectTooltipBody.text = view.Body;

            var tipRt = _b.effectTooltip.GetComponent<RectTransform>();
            var iconRt = view.GetComponent<RectTransform>();
            var parentRt = _b.GetComponent<RectTransform>();
            if (tipRt != null && iconRt != null && parentRt != null)
            {
                Vector3[] corners = new Vector3[4];
                iconRt.GetWorldCorners(corners);
                Vector3 world = new Vector3((corners[0].x + corners[3].x) * 0.5f, corners[0].y - 6f, corners[0].z);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRt,
                        RectTransformUtility.WorldToScreenPoint(null, world),
                        null,
                        out var local))
                {
                    tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0.5f);
                    tipRt.pivot = new Vector2(0.5f, 1f);
                    tipRt.anchoredPosition = local;
                }
            }

            _b.effectTooltip.SetActive(true);
            _b.effectTooltip.transform.SetAsLastSibling();
        }

        void HideEffectTooltip()
        {
            if (_b?.effectTooltip != null)
                _b.effectTooltip.SetActive(false);
        }

        static void EnsureRowIcon(UpgradeRowView view, Transform row, float size)
        {
            if (view == null) return;
            if (view.icon == null)
                view.icon = FindOrCreateRowIcon(row, size);
        }

        static void EnsureRowIcon(BoosterRowView view, Transform row, float size)
        {
            if (view == null) return;
            if (view.icon == null)
                view.icon = FindOrCreateRowIcon(row, size);
        }

        static Image FindOrCreateRowIcon(Transform row, float size)
        {
            if (row == null) return null;
            var existing = row.Find("Icon");
            if (existing != null)
            {
                var img = existing.GetComponent<Image>();
                if (img != null) return img;
            }

            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(8f + size * 0.5f, 0f);
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static void ClearList(List<GameObject> list, Transform parent)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) Destroy(list[i]);
            }
            list.Clear();
        }
    }
}
