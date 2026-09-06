using System.Collections.Generic;
using MasterBidder.Audio;
using MasterBidder.Campaign;
using MasterBidder.Content;
using MasterBidder.Core;
using MasterBidder.Flow;
using MasterBidder.Services;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Runtime UI controller. Instantiates editable prefabs from Assets/content/ui/
    /// (generated via Master Bidder → Generate UI Prefabs) and binds game actions.
    /// </summary>
    public class GameUiShell : MonoBehaviour
    {
        [SerializeField] GameObject gameUiPrefab;
        [SerializeField] GameObject collectorCardPrefab;
        [SerializeField] GameObject upgradeRowPrefab;
        [SerializeField] GameObject boosterRowPrefab;

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
        bool _briefLayoutReady;
        bool _reportShowBoosters;
        bool _reportLayoutReady;
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
            if (_b == null)
            {
                Debug.LogError("[GameUiShell] GameUI root is missing GameUiBindings.");
                Destroy(instance);
                return;
            }

            GameUiSprites.Warmup();
            _canvas = _b.canvas != null ? _b.canvas : instance.GetComponent<Canvas>();
            ApplyBindings(_b);
            DressIntroScreen();
            DressBriefScreen();
            DressAuctionScreen();
            DressReportScreen();
            DressCollectorPopup();
            DressTutorialHint();
            WireListeners();
        }

        void DressIntroScreen()
        {
            if (_intro == null) return;
            var rootImg = _intro.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.color = GameUiStyle.ScreenBg;
            }

            var card = _intro.transform.Find("Card");
            if (card == null) return;
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null)
                GameUiStyle.ApplyFramedPanel(cardImg);

            var cardRt = card.GetComponent<RectTransform>();
            if (cardRt != null)
            {
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = new Vector2(0f, -8f);
                cardRt.sizeDelta = new Vector2(620f, 400f);
            }

            if (_b.introTitle != null)
            {
                _b.introTitle.fontSize = 34;
                _b.introTitle.alignment = TextAnchor.MiddleCenter;
                StretchUi(_b.introTitle.rectTransform, new Vector2(0, 0.84f), Vector2.one, new Vector2(24, -18), new Vector2(-24, -8));
            }

            if (_b.introSubtitle != null)
            {
                _b.introSubtitle.fontSize = 20;
                _b.introSubtitle.fontStyle = FontStyle.Bold;
                _b.introSubtitle.color = GameUiStyle.TextColor;
                _b.introSubtitle.alignment = TextAnchor.MiddleCenter;
                _b.introSubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
                StretchUi(_b.introSubtitle.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.84f), new Vector2(24, 0), new Vector2(-24, 0));
            }

            if (_b.introLede != null)
            {
                _b.introLede.fontSize = 15;
                _b.introLede.lineSpacing = 1.1f;
                StretchUi(_b.introLede.rectTransform, new Vector2(0, 0.54f), new Vector2(1, 0.70f), new Vector2(28, 0), new Vector2(-28, 0));
            }

            if (_b.introRules != null)
            {
                _b.introRules.fontSize = 14;
                _b.introRules.lineSpacing = 1.15f;
                StretchUi(_b.introRules.rectTransform, new Vector2(0, 0.18f), new Vector2(1, 0.52f), new Vector2(28, 0), new Vector2(-28, 0));
            }

            if (_b.btnContinue != null)
                StretchUi(_b.btnContinue.GetComponent<RectTransform>(), new Vector2(0.06f, 0.04f), new Vector2(0.48f, 0.15f), Vector2.zero, Vector2.zero);
            if (_b.btnStart != null)
                StretchUi(_b.btnStart.GetComponent<RectTransform>(), new Vector2(0.52f, 0.04f), new Vector2(0.94f, 0.15f), Vector2.zero, Vector2.zero);
        }

        void DressBriefScreen()
        {
            if (_brief == null) return;

            // Leave the shared Chrome banner visible (same top bar as auction).
            StretchUi(_brief.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -52));

            var rootImg = _brief.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.color = GameUiStyle.ScreenBg;
            }

            EnsureBriefChromeBars();
            EnsureBriefLayout();
            ApplyBriefPanelMode();
        }

        void EnsureBriefChromeBars()
        {
            if (_b == null) return;
            var chrome = EffectsChromeParent();
            if (chrome == null) return;

            // Hide legacy split day/cap pills — replaced by one status plate on the right.
            HideChromeChild(chrome, "DayBar");
            HideChromeChild(chrome, "CapBar");
            if (_brief != null)
            {
                HideChromeChild(_brief.transform, "DayBar");
                HideChromeChild(_brief.transform, "CapBar");
            }

            // Shared status plate (day + capital) — constant across gameplay screens.
            Transform status = chrome.Find("ChromeStatus") ?? chrome.Find("BriefStatus");
            if (status == null)
            {
                var go = new GameObject("ChromeStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(chrome, false);
                status = go.transform;
            }
            else if (status.name == "BriefStatus")
            {
                status.name = "ChromeStatus";
            }

            DressFixedBar(status, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(360f, 38f), GameUiSprites.BarCurrency);
            var lang = chrome.Find("Lang");
            if (lang != null)
            {
                status.SetSiblingIndex(lang.GetSiblingIndex());
                lang.gameObject.SetActive(false);
            }
            else status.SetAsLastSibling();

            Text statusText = status.Find("Status")?.GetComponent<Text>();
            if (statusText == null)
            {
                if (_b.briefDay != null)
                {
                    _b.briefDay.transform.SetParent(status, false);
                    _b.briefDay.gameObject.name = "Status";
                    statusText = _b.briefDay;
                }
                else
                {
                    var tGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
                    tGo.transform.SetParent(status, false);
                    statusText = tGo.GetComponent<Text>();
                    statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (statusText.font == null) statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    _b.briefDay = statusText;
                }
            }
            else if (_b.briefDay == null)
            {
                _b.briefDay = statusText;
            }

            statusText.fontSize = 15;
            statusText.fontStyle = FontStyle.Bold;
            statusText.color = GameUiStyle.OnDark;
            statusText.alignment = TextAnchor.MiddleCenter;
            StretchUi(statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 0), new Vector2(-12, 0));

            if (_b.briefCapital != null && _b.briefCapital != statusText)
                _b.briefCapital.gameObject.SetActive(false);

            if (_b.langDropdown != null)
                _b.langDropdown.gameObject.SetActive(false);

            status.gameObject.SetActive(true);
            EnsureEffectsHud();
        }

        static void HideChromeChild(Transform parent, string name)
        {
            if (parent == null) return;
            var t = parent.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        void EnsureBriefLayout()
        {
            if (_brief == null || _b == null || _briefLayoutReady) return;

            // Hide legacy dual-column Clients panel; keep its scroll for reparenting.
            var legacyClients = _brief.transform.Find("Clients");
            Transform collectorScroll = null;
            if (legacyClients != null)
            {
                collectorScroll = legacyClients.Find("CollectorScroll");
                var orderPlate = legacyClients.Find("OrderPlate");
                if (orderPlate != null) orderPlate.gameObject.SetActive(false);
                var legacyImg = legacyClients.GetComponent<Image>();
                if (legacyImg != null)
                {
                    legacyImg.sprite = null;
                    legacyImg.color = new Color(0, 0, 0, 0);
                    legacyImg.raycastTarget = false;
                }
                StretchUi(legacyClients.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }

            // Sidebar: prefer new name, else migrate Workshop.
            Transform sidebar = _brief.transform.Find("Sidebar");
            var workshop = _brief.transform.Find("Workshop");
            if (sidebar == null && workshop != null)
            {
                workshop.name = "Sidebar";
                sidebar = workshop;
            }
            if (sidebar == null)
            {
                var go = new GameObject("Sidebar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_brief.transform, false);
                sidebar = go.transform;
            }

            StretchUi(sidebar.GetComponent<RectTransform>(), new Vector2(0.66f, 0.02f), new Vector2(0.985f, 0.98f), new Vector2(6, 8), new Vector2(-12, -8));
            var sidebarImg = sidebar.GetComponent<Image>();
            if (sidebarImg != null) GameUiStyle.ApplyFramedPanel(sidebarImg);

            // List headings at the top of the sidebar.
            EnsureSidebarHeading(ref _b.briefClientHeading, sidebar, "OrdersH");
            EnsureSidebarHeading(ref _b.briefWorkshopHeading, sidebar, "UpgradesH");
            if (_b.briefClientHeading != null)
            {
                StretchUi(_b.briefClientHeading.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
                _b.briefClientHeading.fontSize = 26;
                _b.briefClientHeading.fontStyle = FontStyle.Bold;
                _b.briefClientHeading.color = GameUiStyle.Accent;
                _b.briefClientHeading.alignment = TextAnchor.MiddleLeft;
            }
            if (_b.briefWorkshopHeading != null)
            {
                StretchUi(_b.briefWorkshopHeading.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
                _b.briefWorkshopHeading.fontSize = 26;
                _b.briefWorkshopHeading.fontStyle = FontStyle.Bold;
                _b.briefWorkshopHeading.color = GameUiStyle.Accent;
                _b.briefWorkshopHeading.alignment = TextAnchor.MiddleLeft;
            }

            // Upgrade icons live in Chrome (auction-style); remove sidebar strip if present.
            var briefEffects = sidebar.Find("BriefEffects");
            if (briefEffects != null) briefEffects.gameObject.SetActive(false);
            _b.briefEffectsHost = null;

            // Move scrolls into sidebar under the heading.
            if (collectorScroll == null && _b.collectorList != null)
                collectorScroll = _b.collectorList.parent != null ? _b.collectorList.parent.parent : null;
            collectorScroll = sidebar.Find("CollectorScroll")
                ?? (legacyClients != null ? legacyClients.Find("CollectorScroll") : null)
                ?? collectorScroll;
            var upgradeScroll = sidebar.Find("UpgradeScroll")
                ?? (workshop != null ? workshop.Find("UpgradeScroll") : null)
                ?? _brief.transform.Find("Workshop/UpgradeScroll");

            if (collectorScroll != null)
            {
                collectorScroll.SetParent(sidebar, false);
                StretchUi(collectorScroll.GetComponent<RectTransform>(), new Vector2(0, 0.14f), new Vector2(1, 0.88f), new Vector2(10, 6), new Vector2(-10, -4));
                TightenScrollSpacing(collectorScroll, 6);
                var content = collectorScroll.Find("Viewport/Content");
                if (content != null) _b.collectorList = content;
            }

            if (upgradeScroll != null)
            {
                upgradeScroll.SetParent(sidebar, false);
                StretchUi(upgradeScroll.GetComponent<RectTransform>(), new Vector2(0, 0.14f), new Vector2(1, 0.88f), new Vector2(10, 6), new Vector2(-10, -4));
                TightenScrollSpacing(upgradeScroll, 4);
                var content = upgradeScroll.Find("Viewport/Content");
                if (content != null) _b.upgradeList = content;
            }

            // Footer actions inside sidebar.
            if (_b.btnBriefPanelToggle == null)
            {
                var existing = sidebar.Find("TogglePanel")?.GetComponent<Button>();
                if (existing != null)
                {
                    _b.btnBriefPanelToggle = existing;
                    _b.briefPanelToggleLabel = existing.GetComponentInChildren<Text>();
                }
                else
                {
                    var toggleGo = new GameObject("TogglePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    toggleGo.transform.SetParent(sidebar, false);
                    var img = toggleGo.GetComponent<Image>();
                    GameUiStyle.ApplySecondaryButton(img);
                    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    labelGo.transform.SetParent(toggleGo.transform, false);
                    StretchUi(labelGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
                    var label = labelGo.GetComponent<Text>();
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.fontSize = 15;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = GameUiStyle.TextColor;
                    _b.btnBriefPanelToggle = toggleGo.GetComponent<Button>();
                    _b.btnBriefPanelToggle.targetGraphic = img;
                    _b.briefPanelToggleLabel = label;
                }
            }
            StretchUi(_b.btnBriefPanelToggle.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.48f, 0.12f), Vector2.zero, Vector2.zero);

            if (_b.btnEnterHall != null)
            {
                _b.btnEnterHall.transform.SetParent(sidebar, false);
                StretchUi(_b.btnEnterHall.GetComponent<RectTransform>(), new Vector2(0.52f, 0.02f), new Vector2(0.96f, 0.12f), Vector2.zero, Vector2.zero);
            }

            EnsureActiveClientPanel();

            if (_b.btnReset != null)
            {
                _b.btnReset.transform.SetParent(_brief.transform, false);
                StretchUi(_b.btnReset.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.14f, 0.07f), Vector2.zero, Vector2.zero);
                var cg = _b.btnReset.GetComponent<CanvasGroup>();
                if (cg == null) cg = _b.btnReset.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.5f;
                if (_b.resetLabel != null) _b.resetLabel.fontSize = 12;
            }

            if (legacyClients != null)
                legacyClients.gameObject.SetActive(false);

            _briefLayoutReady = true;
        }

        static void EnsureSidebarHeading(ref Text heading, Transform sidebar, string name)
        {
            if (sidebar == null) return;
            if (heading != null)
            {
                // If heading was aliased to the active-client name, create a real list title.
                if (heading.transform.parent != null && heading.transform.parent.name == "ActiveClient")
                    heading = null;
                else
                {
                    heading.transform.SetParent(sidebar, false);
                    heading.gameObject.name = name;
                    heading.gameObject.SetActive(true);
                    return;
                }
            }

            var existing = sidebar.Find(name)?.GetComponent<Text>();
            if (existing != null)
            {
                heading = existing;
                return;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(sidebar, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.color = GameUiStyle.Accent;
            t.alignment = TextAnchor.MiddleLeft;
            heading = t;
        }

        void EnsureActiveClientPanel()
        {
            if (_brief == null || _b == null) return;

            Transform active = _brief.transform.Find("ActiveClient");
            if (active == null)
            {
                var go = new GameObject("ActiveClient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_brief.transform, false);
                active = go.transform;
            }

            // Short compact strip under Chrome (sketch proportions).
            StretchUi(active.GetComponent<RectTransform>(), new Vector2(0.015f, 0.86f), new Vector2(0.42f, 0.98f), new Vector2(6, -4), new Vector2(-6, -4));
            var activeImg = active.GetComponent<Image>();
            if (activeImg != null) GameUiStyle.ApplyCard(activeImg);
            _b.briefActiveClient = active.gameObject;

            if (_b.briefActivePortrait == null)
            {
                var p = active.Find("Portrait");
                if (p == null)
                {
                    var pGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    pGo.transform.SetParent(active, false);
                    p = pGo.transform;
                }
                _b.briefActivePortrait = p.GetComponent<Image>();
            }
            StretchUi(_b.briefActivePortrait.rectTransform, new Vector2(0, 0.08f), new Vector2(0.16f, 0.92f), new Vector2(8, 0), new Vector2(-2, 0));
            _b.briefActivePortrait.preserveAspect = true;
            _b.briefActivePortrait.color = _b.briefActivePortrait.sprite != null ? Color.white : GameUiStyle.PanelLight;

            if (_b.briefActiveName == null || _b.briefActiveName.transform.parent != active)
            {
                var n = active.Find("Name")?.GetComponent<Text>();
                if (n == null)
                {
                    var nGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
                    nGo.transform.SetParent(active, false);
                    n = nGo.GetComponent<Text>();
                    n.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (n.font == null) n.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                _b.briefActiveName = n;
            }
            _b.briefActiveName.fontSize = 14;
            _b.briefActiveName.fontStyle = FontStyle.Bold;
            _b.briefActiveName.color = GameUiStyle.Accent;
            _b.briefActiveName.alignment = TextAnchor.MiddleLeft;
            StretchUi(_b.briefActiveName.rectTransform, new Vector2(0.17f, 0.52f), new Vector2(0.48f, 0.92f), new Vector2(4, 0), new Vector2(-4, -2));

            if (_b.briefActiveTags == null || _b.briefActiveTags.transform.parent != active)
            {
                if (_b.briefOrderPreview != null && _b.briefOrderPreview.transform.parent != active)
                {
                    _b.briefOrderPreview.transform.SetParent(active, false);
                    _b.briefActiveTags = _b.briefOrderPreview;
                    _b.briefOrderPreview.gameObject.name = "Tags";
                }
                else
                {
                    var t = active.Find("Tags")?.GetComponent<Text>();
                    if (t == null)
                    {
                        var tGo = new GameObject("Tags", typeof(RectTransform), typeof(Text));
                        tGo.transform.SetParent(active, false);
                        t = tGo.GetComponent<Text>();
                        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                    _b.briefActiveTags = t;
                    _b.briefOrderPreview = t;
                }
            }
            _b.briefActiveTags.fontSize = 20;
            _b.briefActiveTags.fontStyle = FontStyle.Bold;
            _b.briefActiveTags.color = GameUiStyle.TextColor;
            _b.briefActiveTags.alignment = TextAnchor.MiddleLeft;
            _b.briefActiveTags.horizontalOverflow = HorizontalWrapMode.Wrap;
            _b.briefActiveTags.verticalOverflow = VerticalWrapMode.Overflow;
            _b.briefActiveTags.lineSpacing = 1.0f;
            StretchUi(_b.briefActiveTags.rectTransform, new Vector2(0.48f, 0.08f), new Vector2(1, 0.92f), new Vector2(4, 2), new Vector2(-10, -2));
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
                _b.briefClientHeading.fontSize = 26;
                _b.briefClientHeading.fontStyle = FontStyle.Bold;
                _b.briefClientHeading.color = GameUiStyle.Accent;
                _b.briefClientHeading.gameObject.SetActive(!_briefShowUpgrades);
            }
            if (_b.briefWorkshopHeading != null)
            {
                _b.briefWorkshopHeading.text = LocaleService.T("brief.workshop");
                _b.briefWorkshopHeading.fontSize = 26;
                _b.briefWorkshopHeading.fontStyle = FontStyle.Bold;
                _b.briefWorkshopHeading.color = GameUiStyle.Accent;
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

        void DressAuctionScreen()
        {
            if (_auction == null) return;

            var rootImg = _auction.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.color = new Color(0, 0, 0, 0);
                rootImg.raycastTarget = false;
            }

            var audience = _auction.transform.Find("Audience");
            if (audience != null)
            {
                audience.gameObject.SetActive(false);
                var audImg = audience.GetComponent<Image>();
                if (audImg != null)
                {
                    audImg.sprite = null;
                    audImg.color = new Color(0, 0, 0, 0);
                    audImg.raycastTarget = false;
                }
            }

            if (_b != null)
                _b.rivalHeads = System.Array.Empty<Image>();

            EnsureAuctionActiveClientPanel();

            var hud = _auction.transform.Find("HudRight");
            if (hud == null) return;

            EnsureEffectsHud();

            // Side rail under the shared Chrome banner.
            StretchUi(hud.GetComponent<RectTransform>(), new Vector2(0.72f, 0.02f), new Vector2(0.985f, 1f), new Vector2(6, 8), new Vector2(-12, -56));
            var hudImg = hud.GetComponent<Image>();
            if (hudImg != null) GameUiStyle.ApplyFramedPanel(hudImg);

            // Day / venue / lot — keep at top of sidebar.
            if (_b.aucHud != null)
            {
                _b.aucHud.fontSize = 13;
                _b.aucHud.fontStyle = FontStyle.Bold;
                _b.aucHud.color = GameUiStyle.TextColor;
                _b.aucHud.alignment = TextAnchor.MiddleLeft;
                _b.aucHud.horizontalOverflow = HorizontalWrapMode.Wrap;
                StretchUi(_b.aucHud.rectTransform, new Vector2(0, 0.92f), Vector2.one, new Vector2(16, -8), new Vector2(-16, -4));
            }

            // Order details moved to top-left client strip — hide sidebar order plate.
            var orderPlate = hud.Find("OrderPlate");
            if (orderPlate != null) orderPlate.gameObject.SetActive(false);
            if (_b.orderCard != null) _b.orderCard.gameObject.SetActive(false);

            // Big live price.
            var econ = hud.Find("Econ");
            if (econ != null)
            {
                StretchUi(econ.GetComponent<RectTransform>(), new Vector2(0, 0.72f), new Vector2(1, 0.92f), new Vector2(14, 2), new Vector2(-14, -4));
                var econImg = econ.GetComponent<Image>();
                if (econImg != null) GameUiStyle.ApplyCard(econImg);
            }

            if (_b.livePrice != null)
            {
                _b.livePrice.fontSize = 26;
                _b.livePrice.fontStyle = FontStyle.Bold;
                _b.livePrice.color = GameUiStyle.TextColor;
                _b.livePrice.alignment = TextAnchor.MiddleCenter;
                StretchUi(_b.livePrice.rectTransform, new Vector2(0, 0.42f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -4));
            }

            // Only remaining client purchase budget (no commission / speed).
            if (_b.liveBudget != null)
            {
                _b.liveBudget.fontSize = 15;
                _b.liveBudget.fontStyle = FontStyle.Bold;
                _b.liveBudget.color = GameUiStyle.TextColor;
                _b.liveBudget.alignment = TextAnchor.MiddleCenter;
                StretchUi(_b.liveBudget.rectTransform, new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(10, 4), new Vector2(-10, -4));
            }
            if (_b.liveSpeed != null)
                _b.liveSpeed.gameObject.SetActive(false);

            var fields = hud.Find("Fields");
            if (fields != null)
            {
                StretchUi(fields.GetComponent<RectTransform>(), new Vector2(0, 0.2f), new Vector2(1, 0.72f), new Vector2(14, 4), new Vector2(-14, -4));
                var fieldsImg = fields.GetComponent<Image>();
                if (fieldsImg != null) GameUiStyle.ApplyCard(fieldsImg);
            }

            if (_b.fieldLabels != null)
            {
                float[] rowTops = { 1f, 0.86f, 0.72f, 0.58f, 0.18f, 0f };
                for (int i = 0; i < _b.fieldLabels.Length; i++)
                {
                    if (_b.fieldRows != null && i < _b.fieldRows.Length && _b.fieldRows[i] != null)
                    {
                        var rowRt = _b.fieldRows[i].rectTransform;
                        StretchUi(rowRt, new Vector2(0, rowTops[i + 1]), new Vector2(1, rowTops[i]), new Vector2(3, 1), new Vector2(-3, -1));
                        if (_b.fieldRows[i].GetComponent<RectMask2D>() == null)
                            _b.fieldRows[i].gameObject.AddComponent<RectMask2D>();
                    }

                    if (_b.fieldLabels[i] != null)
                    {
                        _b.fieldLabels[i].fontSize = 12;
                        _b.fieldLabels[i].color = GameUiStyle.Dim;
                        _b.fieldLabels[i].alignment = TextAnchor.UpperLeft;
                        StretchUi(_b.fieldLabels[i].rectTransform, new Vector2(0, 0), new Vector2(0.38f, 1), new Vector2(10, 4), new Vector2(0, -4));
                    }
                    if (_b.fieldValues != null && i < _b.fieldValues.Length && _b.fieldValues[i] != null)
                    {
                        _b.fieldValues[i].fontSize = 13;
                        _b.fieldValues[i].alignment = TextAnchor.UpperLeft;
                        _b.fieldValues[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                        _b.fieldValues[i].verticalOverflow = VerticalWrapMode.Truncate;
                        StretchUi(_b.fieldValues[i].rectTransform, new Vector2(0.38f, 0), Vector2.one, new Vector2(4, 4), new Vector2(-10, -4));
                    }
                }
            }

            // 3 controls: large BUY + two smaller (Skip / Finish). StartLot spans when shown.
            if (_b.btnStartLot != null)
                StretchUi(_b.btnStartLot.GetComponent<RectTransform>(), new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.18f), Vector2.zero, Vector2.zero);
            if (_b.btnBuy != null)
                StretchUi(_b.btnBuy.GetComponent<RectTransform>(), new Vector2(0.05f, 0.02f), new Vector2(0.58f, 0.18f), Vector2.zero, Vector2.zero);
            if (_b.btnSkip != null)
                StretchUi(_b.btnSkip.GetComponent<RectTransform>(), new Vector2(0.6f, 0.02f), new Vector2(0.77f, 0.18f), Vector2.zero, Vector2.zero);
            if (_b.btnFinishDay != null)
                StretchUi(_b.btnFinishDay.GetComponent<RectTransform>(), new Vector2(0.79f, 0.02f), new Vector2(0.95f, 0.18f), Vector2.zero, Vector2.zero);

            // Result / funds toasts sit above the button row.
            var bannerBg = hud.Find("BannerBg");
            if (bannerBg != null)
                StretchUi(bannerBg.GetComponent<RectTransform>(), new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.24f), Vector2.zero, Vector2.zero);
            var fundsBg = hud.Find("FundsBg");
            if (fundsBg != null)
                StretchUi(fundsBg.GetComponent<RectTransform>(), new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.24f), Vector2.zero, Vector2.zero);
            if (_b.familiarBadge != null)
                StretchUi(_b.familiarBadge.rectTransform, new Vector2(0.55f, 0.24f), new Vector2(0.96f, 0.28f), Vector2.zero, Vector2.zero);
        }

        void EnsureAuctionActiveClientPanel()
        {
            if (_auction == null || _b == null) return;

            Transform active = _auction.transform.Find("ActiveClient");
            if (active == null)
            {
                var go = new GameObject("ActiveClient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_auction.transform, false);
                active = go.transform;
            }

            // Compact strip under Chrome (same idea as brief).
            var rt = active.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.015f, 1f);
            rt.anchorMax = new Vector2(0.42f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -56f);
            rt.sizeDelta = new Vector2(0f, 72f);

            var activeImg = active.GetComponent<Image>();
            if (activeImg != null) GameUiStyle.ApplyCard(activeImg);
            _b.auctionActiveClient = active.gameObject;

            if (_b.auctionActivePortrait == null)
            {
                var p = active.Find("Portrait");
                if (p == null)
                {
                    var pGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    pGo.transform.SetParent(active, false);
                    p = pGo.transform;
                }
                _b.auctionActivePortrait = p.GetComponent<Image>();
            }
            StretchUi(_b.auctionActivePortrait.rectTransform, new Vector2(0, 0.08f), new Vector2(0.16f, 0.92f), new Vector2(8, 0), new Vector2(-2, 0));
            _b.auctionActivePortrait.preserveAspect = true;

            if (_b.auctionActiveName == null)
            {
                var n = active.Find("Name")?.GetComponent<Text>();
                if (n == null)
                {
                    var nGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
                    nGo.transform.SetParent(active, false);
                    n = nGo.GetComponent<Text>();
                    n.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (n.font == null) n.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                _b.auctionActiveName = n;
            }
            _b.auctionActiveName.fontSize = 14;
            _b.auctionActiveName.fontStyle = FontStyle.Bold;
            _b.auctionActiveName.color = GameUiStyle.Accent;
            _b.auctionActiveName.alignment = TextAnchor.MiddleLeft;
            StretchUi(_b.auctionActiveName.rectTransform, new Vector2(0.17f, 0.52f), new Vector2(0.48f, 0.92f), new Vector2(4, 0), new Vector2(-4, -2));

            if (_b.auctionActiveTags == null)
            {
                var t = active.Find("Tags")?.GetComponent<Text>();
                if (t == null)
                {
                    var tGo = new GameObject("Tags", typeof(RectTransform), typeof(Text));
                    tGo.transform.SetParent(active, false);
                    t = tGo.GetComponent<Text>();
                    t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                _b.auctionActiveTags = t;
            }
            _b.auctionActiveTags.fontSize = 20;
            _b.auctionActiveTags.fontStyle = FontStyle.Bold;
            _b.auctionActiveTags.color = GameUiStyle.TextColor;
            _b.auctionActiveTags.alignment = TextAnchor.MiddleLeft;
            _b.auctionActiveTags.horizontalOverflow = HorizontalWrapMode.Wrap;
            _b.auctionActiveTags.verticalOverflow = VerticalWrapMode.Overflow;
            StretchUi(_b.auctionActiveTags.rectTransform, new Vector2(0.48f, 0.08f), new Vector2(1, 0.92f), new Vector2(4, 2), new Vector2(-10, -2));
        }

        void DressReportScreen()
        {
            if (_report == null) return;

            StretchUi(_report.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -52));
            var rootImg = _report.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.color = new Color(0, 0, 0, 0);
                rootImg.raycastTarget = false;
            }

            EnsureReportLayout();
            ApplyReportPanelMode();
        }

        void EnsureReportLayout()
        {
            if (_report == null || _b == null || _reportLayoutReady) return;

            // Hide legacy full-screen card layout.
            var legacyCard = _report.transform.Find("Card");
            if (legacyCard != null)
            {
                // Steal scrolls/buttons before disabling.
                var legacyBoost = legacyCard.Find("Boosters");
                var legacyBoosterScroll = legacyBoost != null ? legacyBoost.Find("BoosterScroll") : null;
                if (legacyBoosterScroll == null && _b.boosterList != null)
                    legacyBoosterScroll = _b.boosterList.parent != null ? _b.boosterList.parent.parent : null;
            }

            Transform sidebar = _report.transform.Find("Sidebar");
            if (sidebar == null)
            {
                var go = new GameObject("Sidebar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_report.transform, false);
                sidebar = go.transform;
            }
            StretchUi(sidebar.GetComponent<RectTransform>(), new Vector2(0.66f, 0.02f), new Vector2(0.985f, 0.98f), new Vector2(6, 8), new Vector2(-12, -8));
            var sideImg = sidebar.GetComponent<Image>();
            if (sideImg != null) GameUiStyle.ApplyFramedPanel(sideImg);

            // Headings
            if (_b.reportTitle != null)
            {
                _b.reportTitle.transform.SetParent(sidebar, false);
                StretchUi(_b.reportTitle.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
                _b.reportTitle.fontSize = 26;
                _b.reportTitle.fontStyle = FontStyle.Bold;
                _b.reportTitle.color = GameUiStyle.Accent;
            }
            if (_b.boosterHeading != null)
            {
                _b.boosterHeading.transform.SetParent(sidebar, false);
                StretchUi(_b.boosterHeading.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
                _b.boosterHeading.fontSize = 26;
                _b.boosterHeading.fontStyle = FontStyle.Bold;
                _b.boosterHeading.color = GameUiStyle.Accent;
            }
            if (_b.reportBody != null)
                _b.reportBody.gameObject.SetActive(false);

            // Purchase tags scroll
            Transform purchaseScroll = sidebar.Find("PurchaseScroll");
            if (purchaseScroll == null)
            {
                CreateReportScrollRuntime(sidebar, "PurchaseScroll");
                purchaseScroll = sidebar.Find("PurchaseScroll");
            }
            if (purchaseScroll != null)
            {
                StretchUi(purchaseScroll.GetComponent<RectTransform>(), new Vector2(0, 0.14f), new Vector2(1, 0.88f), new Vector2(10, 6), new Vector2(-10, -4));
                TightenScrollSpacing(purchaseScroll, 8);
                var content = purchaseScroll.Find("Viewport/Content");
                if (content != null) _b.purchaseTagList = content;
            }

            // Booster scroll — migrate from legacy card if needed
            Transform boosterScroll = sidebar.Find("BoosterScroll");
            if (boosterScroll == null && legacyCard != null)
            {
                var fromCard = legacyCard.Find("Boosters/BoosterScroll");
                if (fromCard != null)
                {
                    fromCard.SetParent(sidebar, false);
                    boosterScroll = fromCard;
                }
            }
            if (boosterScroll == null && _b.boosterList != null)
            {
                var maybe = _b.boosterList.parent != null ? _b.boosterList.parent.parent : null;
                if (maybe != null && maybe.name.Contains("Booster"))
                {
                    maybe.SetParent(sidebar, false);
                    boosterScroll = maybe;
                }
            }
            if (boosterScroll == null)
            {
                CreateReportScrollRuntime(sidebar, "BoosterScroll");
                boosterScroll = sidebar.Find("BoosterScroll");
            }
            if (boosterScroll != null)
            {
                StretchUi(boosterScroll.GetComponent<RectTransform>(), new Vector2(0, 0.14f), new Vector2(1, 0.88f), new Vector2(10, 6), new Vector2(-10, -4));
                TightenScrollSpacing(boosterScroll, 6);
                var content = boosterScroll.Find("Viewport/Content");
                if (content != null) _b.boosterList = content;
                boosterScroll.gameObject.SetActive(false);
            }

            // Footer buttons
            if (_b.btnReportPanelToggle == null)
            {
                var existing = sidebar.Find("TogglePanel")?.GetComponent<Button>();
                if (existing != null)
                {
                    _b.btnReportPanelToggle = existing;
                    _b.reportPanelToggleLabel = existing.GetComponentInChildren<Text>();
                }
                else
                {
                    var toggleGo = new GameObject("TogglePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    toggleGo.transform.SetParent(sidebar, false);
                    var img = toggleGo.GetComponent<Image>();
                    GameUiStyle.ApplySecondaryButton(img);
                    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    labelGo.transform.SetParent(toggleGo.transform, false);
                    StretchUi(labelGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
                    var label = labelGo.GetComponent<Text>();
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.fontSize = 15;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = GameUiStyle.TextColor;
                    _b.btnReportPanelToggle = toggleGo.GetComponent<Button>();
                    _b.btnReportPanelToggle.targetGraphic = img;
                    _b.reportPanelToggleLabel = label;
                }
            }
            StretchUi(_b.btnReportPanelToggle.GetComponent<RectTransform>(), new Vector2(0.04f, 0.02f), new Vector2(0.48f, 0.12f), Vector2.zero, Vector2.zero);

            if (_b.btnReportContinue != null)
            {
                _b.btnReportContinue.transform.SetParent(sidebar, false);
                StretchUi(_b.btnReportContinue.GetComponent<RectTransform>(), new Vector2(0.52f, 0.02f), new Vector2(0.96f, 0.12f), Vector2.zero, Vector2.zero);
            }

            EnsureReportStamp();

            if (legacyCard != null)
                legacyCard.gameObject.SetActive(false);

            _reportLayoutReady = true;
        }

        Transform CreateReportScrollRuntime(Transform sidebar, string name)
        {
            // Mirror factory scroll; returns Content transform via side-effect on hierarchy.
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(sidebar, false);
            StretchUi(scrollGo.GetComponent<RectTransform>(), new Vector2(0, 0.14f), new Vector2(1, 0.88f), new Vector2(10, 6), new Vector2(-10, -4));
            scrollGo.GetComponent<Image>().sprite = null;
            scrollGo.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.1f, 0.06f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            StretchUi(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().sprite = null;
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = crt;
            return crt;
        }

        void EnsureReportStamp()
        {
            if (_report == null || _b == null) return;
            Transform stamp = _report.transform.Find("ClientStamp");
            if (stamp == null)
            {
                var go = new GameObject("ClientStamp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_report.transform, false);
                stamp = go.transform;
            }
            StretchUi(stamp.GetComponent<RectTransform>(), new Vector2(0.03f, 0.04f), new Vector2(0.28f, 0.28f), Vector2.zero, Vector2.zero);
            var img = stamp.GetComponent<Image>();
            if (img != null) GameUiStyle.ApplyFramedPanel(img);
            _b.reportStamp = stamp.gameObject;

            if (_b.reportStampLabel == null)
            {
                var t = stamp.Find("StampLabel")?.GetComponent<Text>();
                if (t == null)
                {
                    var tGo = new GameObject("StampLabel", typeof(RectTransform), typeof(Text));
                    tGo.transform.SetParent(stamp, false);
                    t = tGo.GetComponent<Text>();
                    t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                _b.reportStampLabel = t;
            }
            _b.reportStampLabel.fontSize = 22;
            _b.reportStampLabel.fontStyle = FontStyle.Bold;
            _b.reportStampLabel.alignment = TextAnchor.MiddleCenter;
            _b.reportStampLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            StretchUi(_b.reportStampLabel.rectTransform, new Vector2(0, 0.38f), Vector2.one, new Vector2(12, 0), new Vector2(-12, -10));

            if (_b.reportStampDetail == null)
            {
                var t = stamp.Find("StampDetail")?.GetComponent<Text>();
                if (t == null)
                {
                    var tGo = new GameObject("StampDetail", typeof(RectTransform), typeof(Text));
                    tGo.transform.SetParent(stamp, false);
                    t = tGo.GetComponent<Text>();
                    t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                _b.reportStampDetail = t;
            }
            _b.reportStampDetail.fontSize = 14;
            _b.reportStampDetail.alignment = TextAnchor.UpperCenter;
            _b.reportStampDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            StretchUi(_b.reportStampDetail.rectTransform, new Vector2(0, 0.08f), new Vector2(1, 0.4f), new Vector2(12, 4), new Vector2(-12, 0));
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
                _b.reportTitle.fontSize = 26;
                _b.reportTitle.gameObject.SetActive(!_reportShowBoosters);
            }
            if (_b.boosterHeading != null)
            {
                _b.boosterHeading.text = LocaleService.T("report.boosters");
                _b.boosterHeading.fontSize = 26;
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

        void DressTutorialHint()
        {
            if (_tutorial == null) return;

            var rt = _tutorial.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Sit above Skip/Buy so the coach never covers the taught action.
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.2f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(520f, 88f);
            }

            var img = _tutorial.GetComponent<Image>();
            if (img != null)
            {
                GameUiStyle.ApplySliced(img, GameUiSprites.ToastInfo, GameUiStyle.SpriteReady);
                // Match MVP pointer-events:none — toast must not steal clicks from action buttons.
                img.raycastTarget = false;
            }

            if (_b.tutorialText != null)
            {
                _b.tutorialText.fontSize = 15;
                _b.tutorialText.fontStyle = FontStyle.Bold;
                _b.tutorialText.color = GameUiStyle.TextColor;
                _b.tutorialText.alignment = TextAnchor.MiddleCenter;
                _b.tutorialText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _b.tutorialText.raycastTarget = false;
                StretchUi(_b.tutorialText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 12), new Vector2(-28, -12));
            }
        }

        void DressCollectorPopup()
        {
            if (_collectorPopup == null) return;

            var rootImg = _collectorPopup.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.color = GameUiStyle.Overlay;
            }

            var card = _collectorPopup.transform.Find("Card");
            if (card == null) return;

            var cardRt = card.GetComponent<RectTransform>();
            if (cardRt != null)
            {
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.sizeDelta = new Vector2(720f, 420f);
            }

            var cardImg = card.GetComponent<Image>();
            if (cardImg != null) GameUiStyle.ApplyFramedPanel(cardImg);

            // Header: compact portrait + identity in one band.
            if (_b.popupPortrait != null)
            {
                StretchUi(_b.popupPortrait.rectTransform, new Vector2(0, 0.7f), new Vector2(0.22f, 0.96f), new Vector2(24, -16), new Vector2(-8, -14));
                _b.popupPortrait.preserveAspect = true;
                _b.popupPortrait.transform.SetSiblingIndex(1);
            }

            if (_b.popupName != null)
            {
                _b.popupName.fontSize = 26;
                _b.popupName.fontStyle = FontStyle.Bold;
                _b.popupName.color = GameUiStyle.Accent;
                _b.popupName.alignment = TextAnchor.LowerLeft;
                StretchUi(_b.popupName.rectTransform, new Vector2(0.24f, 0.84f), new Vector2(1, 0.96f), new Vector2(8, 0), new Vector2(-24, -14));
            }

            if (_b.popupTagline != null)
            {
                _b.popupTagline.fontSize = 14;
                _b.popupTagline.color = GameUiStyle.TextColor;
                _b.popupTagline.alignment = TextAnchor.UpperLeft;
                _b.popupTagline.lineSpacing = 1.1f;
                _b.popupTagline.horizontalOverflow = HorizontalWrapMode.Wrap;
                StretchUi(_b.popupTagline.rectTransform, new Vector2(0.24f, 0.7f), new Vector2(1, 0.84f), new Vector2(8, 0), new Vector2(-24, 0));
            }

            // Speech fully below the portrait row — never clipped by the face.
            EnsurePopupPlate(card, "SpeechPlate", _b.popupSpeech,
                new Vector2(0, 0.42f), new Vector2(1, 0.68f), new Vector2(22, 4), new Vector2(-22, -4));
            if (_b.popupSpeech != null)
            {
                _b.popupSpeech.fontSize = 15;
                _b.popupSpeech.fontStyle = FontStyle.Italic;
                _b.popupSpeech.color = GameUiStyle.TextColor;
                _b.popupSpeech.alignment = TextAnchor.UpperLeft;
                _b.popupSpeech.horizontalOverflow = HorizontalWrapMode.Wrap;
                _b.popupSpeech.lineSpacing = 1.15f;
                StretchUi(_b.popupSpeech.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10));
            }

            EnsurePopupPlate(card, "TagsPlate", _b.popupTags,
                new Vector2(0, 0.28f), new Vector2(1, 0.42f), new Vector2(22, 2), new Vector2(-22, -2));
            if (_b.popupTags != null)
            {
                _b.popupTags.fontSize = 17;
                _b.popupTags.fontStyle = FontStyle.Bold;
                _b.popupTags.color = GameUiStyle.Accent;
                _b.popupTags.alignment = TextAnchor.MiddleLeft;
                _b.popupTags.horizontalOverflow = HorizontalWrapMode.Wrap;
                StretchUi(_b.popupTags.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));
            }

            if (_b.popupWarning != null)
            {
                _b.popupWarning.fontSize = 13;
                _b.popupWarning.color = GameUiStyle.Bad;
                _b.popupWarning.alignment = TextAnchor.MiddleLeft;
                StretchUi(_b.popupWarning.rectTransform, new Vector2(0, 0.16f), new Vector2(1, 0.28f), new Vector2(28, 0), new Vector2(-28, 0));
            }

            if (_b.btnPopupStart != null)
                StretchUi(_b.btnPopupStart.GetComponent<RectTransform>(), new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.14f), Vector2.zero, Vector2.zero);
        }

        static void EnsurePopupPlate(Transform card, string plateName, Text content,
            Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            if (card == null || content == null) return;

            Transform plate = card.Find(plateName);
            if (plate == null)
            {
                var go = new GameObject(plateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                plate = go.transform;
            }

            if (content.transform.parent != plate)
                content.transform.SetParent(plate, false);

            StretchUi(plate.GetComponent<RectTransform>(), aMin, aMax, offMin, offMax);
            var img = plate.GetComponent<Image>();
            if (img != null) GameUiStyle.ApplyCard(img);
        }

        static void DressFixedBar(Transform bar, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Sprite sprite)
        {
            if (bar == null) return;
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = bar.GetComponent<Image>();
            if (img != null)
                GameUiStyle.ApplySliced(img, sprite, GameUiStyle.SpriteReady);
        }

        static void TightenScrollSpacing(Transform scrollRoot, int spacing)
        {
            if (scrollRoot == null) return;
            var content = scrollRoot.Find("Viewport/Content");
            if (content == null) return;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) return;
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(4, 4, 4, 4);
        }

        static void StretchUi(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            if (rt == null) return;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
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
            EnsureBriefChromeBars();

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

        static void SetToastActive(Text label, bool active)
        {
            if (label == null) return;
            var root = label.transform.parent != null &&
                       (label.transform.parent.name == "BannerBg" || label.transform.parent.name == "FundsBg")
                ? label.transform.parent.gameObject
                : label.gameObject;
            root.SetActive(active);
        }

        static void StyleResultToast(Text label, string result)
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
            EnsureBriefLayout();

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

        void RefreshActiveClientPanel(GameSession session)
        {
            if (_b?.briefActiveClient == null) return;
            var state = session?.State;
            var catalog = _flow?.Catalog;
            var collector = state != null && catalog != null
                ? catalog.FindCollector(state.SelectedBranchId)
                : null;
            var order = state?.PendingOrder;
            bool show = collector != null && order != null;
            _b.briefActiveClient.SetActive(show);
            if (!show) return;

            if (_b.briefActiveName != null)
                _b.briefActiveName.text = collector.nameRu ?? order.NameRu ?? "";

            if (_b.briefActiveTags != null)
                _b.briefActiveTags.text = order.CriteriaLabel ?? "";

            if (_b.briefActivePortrait != null)
            {
                if (collector.portrait != null)
                {
                    _b.briefActivePortrait.sprite = collector.portrait;
                    _b.briefActivePortrait.color = Color.white;
                    _b.briefActivePortrait.enabled = true;
                }
                else
                {
                    _b.briefActivePortrait.sprite = null;
                    _b.briefActivePortrait.color = GameUiStyle.PanelLight;
                }
            }
        }

        void RefreshAuctionActiveClient(DayOrder order, GameSession session)
        {
            if (_b?.auctionActiveClient == null) return;
            var catalog = _flow?.Catalog;
            CollectorData collector = null;
            if (order != null && catalog != null)
                collector = catalog.FindCollector(order.CollectorId);
            if (collector == null && session?.State != null && catalog != null)
                collector = catalog.FindCollector(session.State.SelectedBranchId);

            bool show = order != null;
            _b.auctionActiveClient.SetActive(show);
            if (!show) return;

            if (_b.auctionActiveName != null)
                _b.auctionActiveName.text = collector?.nameRu ?? order.NameRu ?? "";

            if (_b.auctionActiveTags != null)
                _b.auctionActiveTags.text = order.CriteriaLabel ?? "";

            if (_b.auctionActivePortrait != null)
            {
                if (collector?.portrait != null)
                {
                    _b.auctionActivePortrait.sprite = collector.portrait;
                    _b.auctionActivePortrait.color = Color.white;
                    _b.auctionActivePortrait.enabled = true;
                }
                else
                {
                    _b.auctionActivePortrait.sprite = null;
                    _b.auctionActivePortrait.color = GameUiStyle.PanelLight;
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
                var go = collectorCardPrefab != null
                    ? Instantiate(collectorCardPrefab, _b.collectorList)
                    : GameUiHierarchyFactory.BuildCollectorCard();
                if (collectorCardPrefab == null)
                    go.transform.SetParent(_b.collectorList, false);
                go.name = "C_" + c.collectorId;
                var view = go.GetComponent<CollectorCardView>();
                if (view == null) continue;

                var le = go.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = 118;
                    le.preferredHeight = 118;
                }

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
                {
                    var prt = view.portraitRoot.GetComponent<RectTransform>();
                    if (prt != null)
                        StretchUi(prt, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(6, 6), new Vector2(-2, -6));
                    view.portraitRoot.SetActive(c.portrait != null);
                }
                if (c.portrait != null && view.portrait != null)
                {
                    view.portrait.sprite = c.portrait;
                    view.portrait.color = Color.white;
                    view.portrait.preserveAspect = true;
                }

                if (view.label != null)
                {
                    view.label.fontSize = 15;
                    view.label.lineSpacing = 1.05f;
                    view.label.text = $"{c.nameRu}\n{LocaleService.T("brief.mission")} {progress + 1}/{c.LadderLength}";
                    view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    StretchUi(view.label.rectTransform, new Vector2(0.3f, 0), Vector2.one, new Vector2(8, 8), new Vector2(-10, -8));
                }

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
                var go = upgradeRowPrefab != null
                    ? Instantiate(upgradeRowPrefab, _b.upgradeList)
                    : GameUiHierarchyFactory.BuildUpgradeRow();
                if (upgradeRowPrefab == null)
                    go.transform.SetParent(_b.upgradeList, false);
                go.name = "U_" + u.Id;
                var view = go.GetComponent<UpgradeRowView>();
                if (view == null) continue;

                var le = go.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = 64;
                    le.preferredHeight = 64;
                }

                EnsureRowIcon(view, go.transform, 48f);
                if (view.icon != null)
                {
                    view.icon.sprite = GameUiMetaIcons.Get(u.Id);
                    view.icon.enabled = view.icon.sprite != null;
                    view.icon.color = owned ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                }

                if (view.label != null)
                {
                    view.label.fontSize = 13;
                    view.label.lineSpacing = 1.05f;
                    view.label.text = $"{u.NameRu} — {u.Cost:N0} ₽\n{u.DescRu}";
                    view.label.color = owned ? GameUiStyle.Dim : GameUiStyle.TextColor;
                    view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    if (view.icon != null)
                    {
                        var lrt = view.label.rectTransform;
                        lrt.offsetMin = new Vector2(64f, lrt.offsetMin.y);
                    }
                }

                if (view.buyLabel != null)
                {
                    view.buyLabel.fontSize = 14;
                    view.buyLabel.text = owned ? LocaleService.T("brief.owned") : LocaleService.T("brief.buy");
                }
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
            EnsureAuctionActiveClientPanel();
            var state = session?.State;
            if (state == null) return;

            var venue = CampaignConfig.GetVenue(state.CurrentVenue);
            _b.aucHud.text =
                $"{LocaleService.T("auction.day")} {state.Day} · {LocaleService.T("auction.venue")} {venue.LabelRu} · " +
                $"{LocaleService.T("auction.lot")} {state.CurrentLotIndex + 1}/{state.Lots.Count}";

            var order = state.DayOrders.Count > 0 ? state.DayOrders[0] : state.PendingOrder;
            RefreshAuctionActiveClient(order, session);

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
            bool busy = state.LotResolved || state.FastForwarding || _purchaseCardVisible || presenting;
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
                _b.fieldValues[i].fontStyle = revealed ? FontStyle.Bold : FontStyle.Normal;
                _b.fieldValues[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _b.fieldValues[i].verticalOverflow = VerticalWrapMode.Truncate;

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
            EnsureReportLayout();

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
                _b.reportStampDetail.color = GameUiStyle.TextColor;
            }

            RebuildPurchaseTags(r);
            bool showBoosters = r.Pass && state.Day < CampaignConfig.CampaignLength;
            if (!showBoosters) _reportShowBoosters = false;
            RebuildBoosterRows(session, showBoosters);
            if (_b.btnReportPanelToggle != null)
                _b.btnReportPanelToggle.gameObject.SetActive(showBoosters);
            if (_b.btnReportContinue != null)
            {
                StretchUi(_b.btnReportContinue.GetComponent<RectTransform>(),
                    showBoosters ? new Vector2(0.52f, 0.02f) : new Vector2(0.04f, 0.02f),
                    new Vector2(0.96f, 0.12f),
                    Vector2.zero, Vector2.zero);
            }
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
                var empty = GameUiHierarchyFactory.BuildPurchaseTag();
                empty.transform.SetParent(_b.purchaseTagList, false);
                var view = empty.GetComponent<PurchaseTagView>();
                if (view != null)
                {
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
                var go = GameUiHierarchyFactory.BuildPurchaseTag();
                go.transform.SetParent(_b.purchaseTagList, false);
                go.name = "Tag_" + i;
                var view = go.GetComponent<PurchaseTagView>();
                if (view == null) continue;

                if (view.title != null)
                {
                    view.title.fontSize = 16;
                    view.title.text = d.TitleRu ?? "";
                    view.title.color = GameUiStyle.TextColor;
                }
                if (view.meta != null)
                {
                    view.meta.fontSize = 13;
                    view.meta.text = $"{d.Price:N0} ₽  ·  {LocaleService.T("report.commission")} {d.Amount:N0} ₽";
                    if (!string.IsNullOrEmpty(d.Reason))
                        view.meta.text += "\n" + d.Reason;
                }
                if (view.stamp != null)
                {
                    view.stamp.fontSize = 14;
                    view.stamp.text = d.Matched
                        ? LocaleService.T("report.correct")
                        : LocaleService.T("report.incorrect");
                    view.stamp.color = d.Matched ? GameUiStyle.Good : GameUiStyle.Bad;
                }
                if (view.background != null)
                {
                    view.background.color = d.Matched
                        ? new Color(GameUiStyle.Good.r, GameUiStyle.Good.g, GameUiStyle.Good.b, 0.12f)
                        : new Color(GameUiStyle.Bad.r, GameUiStyle.Bad.g, GameUiStyle.Bad.b, 0.1f);
                    // Keep card sprite readable
                    if (view.background.sprite != null)
                        view.background.color = d.Matched ? GameUiStyle.SelectedTint : GameUiStyle.SpriteReady;
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

                var go = boosterRowPrefab != null
                    ? Instantiate(boosterRowPrefab, _b.boosterList)
                    : GameUiHierarchyFactory.BuildBoosterRow();
                if (boosterRowPrefab == null)
                    go.transform.SetParent(_b.boosterList, false);
                go.name = "B_" + id;
                var view = go.GetComponent<BoosterRowView>();
                if (view == null) continue;

                var le = go.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = 76;
                    le.preferredHeight = 76;
                }

                EnsureRowIcon(view, go.transform, 56f);
                if (view.icon != null)
                {
                    view.icon.sprite = GameUiMetaIcons.Get(id);
                    view.icon.enabled = view.icon.sprite != null;
                    view.icon.color = owned ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
                }

                if (view.label != null)
                {
                    view.label.text = $"{def.NameRu} — {cost:N0} ₽\n{def.DescRu}";
                    view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    if (view.icon != null)
                    {
                        var lrt = view.label.rectTransform;
                        lrt.offsetMin = new Vector2(72f, lrt.offsetMin.y);
                    }
                }

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
                if (_purchaseCardVisible)
                {
                    _flow.OnPurchaseCardDismiss();
                    return;
                }
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_purchaseCardVisible) _flow.OnPurchaseCardDismiss();
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

        void EnsureEffectsHud()
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

            if (_b.chromeTitle != null)
                StretchUi(_b.chromeTitle.rectTransform, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(28, 0), new Vector2(-8, 0));

            var bar = chrome.Find("EffectsHud");
            if (bar == null)
            {
                var go = new GameObject("EffectsHud", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(chrome, false);
                go.transform.SetSiblingIndex(Mathf.Min(1, chrome.childCount));
                StretchUi(go.GetComponent<RectTransform>(), new Vector2(0.28f, 0.12f), new Vector2(0.76f, 0.88f), Vector2.zero, Vector2.zero);
                var bg = go.GetComponent<Image>();
                bg.sprite = null;
                bg.color = new Color(1f, 1f, 1f, 0f);
                bg.raycastTarget = false;
                if (go.GetComponent<RectMask2D>() == null)
                    go.AddComponent<RectMask2D>();
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
            else
            {
                StretchUi(bar.GetComponent<RectTransform>(), new Vector2(0.28f, 0.12f), new Vector2(0.76f, 0.88f), Vector2.zero, Vector2.zero);
                var bg = bar.GetComponent<Image>();
                if (bg != null)
                {
                    bg.sprite = null;
                    bg.color = new Color(1f, 1f, 1f, 0f);
                    bg.raycastTarget = false;
                }
                if (bar.GetComponent<RectMask2D>() == null)
                    bar.gameObject.AddComponent<RectMask2D>();
                var hlg = bar.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.childForceExpandHeight = false;
                    hlg.childControlHeight = true;
                    hlg.childAlignment = TextAnchor.MiddleRight;
                }
            }

            if (_b.effectsUpgrades == null || _b.effectsUpgrades.parent != bar)
            {
                var t = bar.Find("Upgrades");
                _b.effectsUpgrades = t != null ? t : CreateEffectsRowRuntime(bar, "Upgrades");
            }
            TuneEffectsRow(_b.effectsUpgrades);

            if (_b.effectsBoosters == null || _b.effectsBoosters.parent != bar)
            {
                var t = bar.Find("Boosters");
                _b.effectsBoosters = t != null ? t : CreateEffectsRowRuntime(bar, "Boosters");
            }
            TuneEffectsRow(_b.effectsBoosters);

            if (_b.effectTooltip == null)
            {
                var tipT = _b.transform.Find("EffectTooltip");
                if (tipT != null)
                {
                    _b.effectTooltip = tipT.gameObject;
                    _b.effectTooltipTitle = tipT.Find("Title")?.GetComponent<Text>();
                    _b.effectTooltipBody = tipT.Find("Body")?.GetComponent<Text>();
                }
                else
                {
                    var tip = new GameObject("EffectTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                    tip.transform.SetParent(_b.transform, false);
                    var tipRt = tip.GetComponent<RectTransform>();
                    tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0.5f);
                    tipRt.pivot = new Vector2(0.5f, 1f);
                    tipRt.sizeDelta = new Vector2(240f, 110f);
                    GameUiStyle.ApplyCard(tip.GetComponent<Image>());
                    tip.GetComponent<Image>().raycastTarget = false;
                    var cg = tip.GetComponent<CanvasGroup>();
                    cg.blocksRaycasts = false;
                    cg.interactable = false;

                    var title = new GameObject("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                    title.transform.SetParent(tip.transform, false);
                    StretchUi(title.rectTransform, new Vector2(0, 0.62f), Vector2.one, new Vector2(12, -8), new Vector2(-12, -6));
                    title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (title.font == null) title.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    title.fontSize = 14;
                    title.fontStyle = FontStyle.Bold;
                    title.color = GameUiStyle.Accent;
                    title.alignment = TextAnchor.UpperLeft;
                    title.raycastTarget = false;

                    var body = new GameObject("Body", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                    body.transform.SetParent(tip.transform, false);
                    StretchUi(body.rectTransform, new Vector2(0, 0), new Vector2(1, 0.62f), new Vector2(12, 8), new Vector2(-12, 0));
                    body.font = title.font;
                    body.fontSize = 12;
                    body.color = GameUiStyle.TextColor;
                    body.alignment = TextAnchor.UpperLeft;
                    body.horizontalOverflow = HorizontalWrapMode.Wrap;
                    body.verticalOverflow = VerticalWrapMode.Overflow;
                    body.raycastTarget = false;

                    tip.SetActive(false);
                    _b.effectTooltip = tip;
                    _b.effectTooltipTitle = title;
                    _b.effectTooltipBody = body;
                }
            }

            if (_b.effectTooltip != null)
                _b.effectTooltip.transform.SetAsLastSibling();
        }

        static void TuneEffectsRow(Transform row)
        {
            if (row == null) return;
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null)
            {
                h.spacing = 4;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
            }
            var le = row.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = le.preferredHeight = 28;
                le.flexibleHeight = 0;
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
            EnsureEffectsHud();
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
