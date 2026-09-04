using System.Collections.Generic;
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
    /// Runtime UI for Intro / Brief / Auction / Report / End + overlays.
    /// </summary>
    public class GameUiShell : MonoBehaviour
    {
        AppFlow _flow;
        Canvas _canvas;

        GameObject _intro, _brief, _auction, _report, _end;
        GameObject _collectorPopup, _purchaseCard, _tutorial;

        // Intro
        Text _introTitle, _introSubtitle, _introLede, _introRules;
        Button _btnContinue, _btnStart;
        Text _continueLabel, _startLabel;

        // Brief
        Text _briefDay, _briefCapital, _briefClientHeading, _briefWorkshopHeading;
        Text _briefOrderPreview;
        Transform _collectorList;
        Transform _upgradeList;
        Button _btnEnterHall, _btnReset;
        Text _enterLabel, _resetLabel;
        readonly List<GameObject> _collectorCards = new List<GameObject>();
        readonly List<GameObject> _upgradeRows = new List<GameObject>();

        // Auction
        Text _aucHud;
        Text _orderCard;
        Text _livePrice, _liveBudget, _liveSpeed;
        Text[] _fieldLabels = new Text[5];
        Text[] _fieldValues = new Text[5];
        Image[] _fieldRows = new Image[5];
        Text _resultBanner;
        Text _fundsHint;
        Text _familiarBadge;
        Button _btnStartLot, _btnBuy, _btnSkip, _btnFinishDay;
        Text _startLotLabel, _buyLabel, _skipLabel, _finishLabel;
        float _fundsFlashUntil;

        // Collector popup
        Text _popupName, _popupTagline, _popupSpeech, _popupTags, _popupWarning;
        Image _popupPortrait;
        Button _btnPopupStart;
        Text _popupStartLabel;

        // Audience (rival heads)
        Transform _audienceRow;
        Image[] _rivalHeads = new Image[15];
        int _lastRaisedRival = -1;
        float _rivalRaiseUntil;

        // Purchase card
        Text _pcTitle, _pcArtist, _pcMeta, _pcFact;
        Button _btnPcContinue;
        Text _pcContinueLabel;
        bool _purchaseCardVisible;

        // Tutorial
        Text _tutorialText;

        // Report
        Text _reportTitle, _reportBody;
        Text _boosterHeading;
        Transform _boosterList;
        readonly List<GameObject> _boosterRows = new List<GameObject>();
        Button _btnReportContinue;
        Text _reportContinueLabel;

        // End
        Text _endTitle;
        Button _btnRestart;
        Text _restartLabel;

        Text _chromeTitle;
        Dropdown _langDropdown;

        static readonly Color Bg = new Color(0.086f, 0.094f, 0.114f, 0.96f);
        static readonly Color Panel = new Color(0.129f, 0.141f, 0.169f, 0.97f);
        static readonly Color PanelLight = new Color(0.169f, 0.184f, 0.220f, 1f);
        static readonly Color Accent = new Color(0.831f, 0.631f, 0.227f, 1f);
        static readonly Color TextColor = new Color(0.925f, 0.933f, 0.945f, 1f);
        static readonly Color Dim = new Color(0.604f, 0.631f, 0.671f, 1f);
        static readonly Color Good = new Color(0.298f, 0.686f, 0.490f, 1f);
        static readonly Color Bad = new Color(0.831f, 0.341f, 0.227f, 1f);

        static readonly string[] FieldIds = { "genre", "period", "artist", "fact", "title" };

        public bool IsPurchaseCardVisible => _purchaseCardVisible;
        public bool IsCollectorPopupVisible => _collectorPopup != null && _collectorPopup.activeSelf;

        public void Bind(AppFlow flow) => _flow = flow;

        public void BuildIfNeeded()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("GameUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            EnsureEventSystem();
            BuildChrome(canvasGo.transform);
            _intro = BuildIntro(canvasGo.transform);
            _brief = BuildBrief(canvasGo.transform);
            _auction = BuildAuction(canvasGo.transform);
            _report = BuildReport(canvasGo.transform);
            _end = BuildEnd(canvasGo.transform);
            _collectorPopup = BuildCollectorPopup(canvasGo.transform);
            _purchaseCard = BuildPurchaseCard(canvasGo.transform);
            _tutorial = BuildTutorial(canvasGo.transform);
            _collectorPopup.SetActive(false);
            _purchaseCard.SetActive(false);
            _tutorial.SetActive(false);
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        void BuildChrome(Transform parent)
        {
            var bar = CreatePanel("Chrome", parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), Vector2.zero);
            bar.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            _chromeTitle = CreateText("Title", bar.transform, "", 14, TextAnchor.MiddleLeft);
            Stretch(_chromeTitle.rectTransform, new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(14, 0), new Vector2(-8, 0));

            var langGo = new GameObject("Lang", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            langGo.transform.SetParent(bar.transform, false);
            Stretch(langGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.1f), new Vector2(0.99f, 0.9f), Vector2.zero, Vector2.zero);
            langGo.GetComponent<Image>().color = Panel;
            _langDropdown = langGo.GetComponent<Dropdown>();
            _langDropdown.targetGraphic = langGo.GetComponent<Image>();
            var caption = CreateText("Caption", langGo.transform, "RU", 14, TextAnchor.MiddleCenter);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _langDropdown.captionText = caption;

            var template = CreatePanel("Template", langGo.transform, Vector2.zero, Vector2.one, new Vector2(0, -90), Vector2.zero);
            template.SetActive(false);
            var viewport = CreatePanel("Viewport", template.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            Stretch(content.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var item = CreatePanel("Item", content.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var itemLabel = CreateText("ItemLabel", item.transform, "Option", 14, TextAnchor.MiddleCenter);
            Stretch(itemLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var toggle = item.AddComponent<Toggle>();
            toggle.targetGraphic = item.GetComponent<Image>();
            _langDropdown.template = template.GetComponent<RectTransform>();
            _langDropdown.itemText = itemLabel;
            _langDropdown.options.Clear();
            _langDropdown.options.Add(new Dropdown.OptionData("Русский"));
            _langDropdown.options.Add(new Dropdown.OptionData("English"));
            _langDropdown.value = LocaleService.Language == "en" ? 1 : 0;
            _langDropdown.onValueChanged.AddListener(i => _flow?.OnSetLanguage(i == 1 ? "en" : "ru"));
        }

        GameObject BuildIntro(Transform parent)
        {
            var root = CreatePanel("Screen_Intro", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440, -300), new Vector2(440, 260));
            _introTitle = CreateText("Title", card.transform, "", 40, TextAnchor.UpperCenter);
            Stretch(_introTitle.rectTransform, new Vector2(0, 0.8f), new Vector2(1, 1), new Vector2(20, -12), new Vector2(-20, -8));
            _introTitle.color = Accent;
            _introTitle.fontStyle = FontStyle.Bold;
            _introSubtitle = CreateText("Sub", card.transform, "", 20, TextAnchor.UpperCenter);
            Stretch(_introSubtitle.rectTransform, new Vector2(0, 0.7f), new Vector2(1, 0.8f), new Vector2(20, 0), new Vector2(-20, 0));
            _introSubtitle.color = Dim;
            _introLede = CreateText("Lede", card.transform, "", 16, TextAnchor.UpperLeft);
            Stretch(_introLede.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 0.7f), new Vector2(28, 0), new Vector2(-28, 0));
            _introLede.horizontalOverflow = HorizontalWrapMode.Wrap;
            _introRules = CreateText("Rules", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(_introRules.rectTransform, new Vector2(0, 0.16f), new Vector2(1, 0.42f), new Vector2(28, 0), new Vector2(-28, 0));
            _introRules.color = Dim;
            _introRules.horizontalOverflow = HorizontalWrapMode.Wrap;
            _btnContinue = CreateButton("Continue", card.transform, out _continueLabel);
            Place(_btnContinue, 0.05f, 0.03f, 0.48f, 0.13f);
            _btnContinue.onClick.AddListener(() => _flow?.OnContinueCareer());
            _btnStart = CreateButton("Start", card.transform, out _startLabel);
            Place(_btnStart, 0.52f, 0.03f, 0.95f, 0.13f);
            _btnStart.GetComponent<Image>().color = Good;
            _btnStart.onClick.AddListener(() => _flow?.OnStartCareer());
            return root;
        }

        GameObject BuildBrief(Transform parent)
        {
            var root = CreatePanel("Screen_Brief", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = Bg;

            _briefDay = CreateText("Day", root.transform, "", 22, TextAnchor.MiddleLeft);
            Stretch(_briefDay.rectTransform, new Vector2(0, 0.92f), new Vector2(0.4f, 1), new Vector2(24, -48), new Vector2(0, -8));
            _briefDay.color = Accent;
            _briefCapital = CreateText("Cap", root.transform, "", 22, TextAnchor.MiddleRight);
            Stretch(_briefCapital.rectTransform, new Vector2(0.4f, 0.92f), new Vector2(1, 1), new Vector2(0, -48), new Vector2(-24, -8));

            var left = CreatePanel("Clients", root.transform, new Vector2(0, 0.12f), new Vector2(0.58f, 0.9f), new Vector2(16, 0), new Vector2(-8, -56));
            _briefClientHeading = CreateText("H", left.transform, "", 18, TextAnchor.UpperLeft);
            Stretch(_briefClientHeading.rectTransform, new Vector2(0, 0.92f), new Vector2(1, 1), new Vector2(12, -8), new Vector2(-12, -4));
            _briefClientHeading.color = Accent;
            _collectorList = CreateScrollContent(left.transform, "CollectorScroll", new Vector2(0, 0.28f), new Vector2(1, 0.92f));
            _briefOrderPreview = CreateText("OrderPrev", left.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(_briefOrderPreview.rectTransform, new Vector2(0, 0), new Vector2(1, 0.28f), new Vector2(12, 8), new Vector2(-12, -4));
            _briefOrderPreview.color = Dim;
            _briefOrderPreview.horizontalOverflow = HorizontalWrapMode.Wrap;
            _briefOrderPreview.verticalOverflow = VerticalWrapMode.Overflow;

            var right = CreatePanel("Workshop", root.transform, new Vector2(0.58f, 0.12f), new Vector2(1, 0.9f), new Vector2(8, 0), new Vector2(-16, -56));
            _briefWorkshopHeading = CreateText("WH", right.transform, "", 18, TextAnchor.UpperLeft);
            Stretch(_briefWorkshopHeading.rectTransform, new Vector2(0, 0.92f), new Vector2(1, 1), new Vector2(12, -8), new Vector2(-12, -4));
            _briefWorkshopHeading.color = Accent;
            _upgradeList = CreateScrollContent(right.transform, "UpgradeScroll", new Vector2(0, 0), new Vector2(1, 0.92f));

            _btnReset = CreateButton("Reset", root.transform, out _resetLabel);
            Place(_btnReset, 0.02f, 0.02f, 0.22f, 0.1f);
            _btnReset.onClick.AddListener(() => _flow?.OnResetProgress());
            _btnEnterHall = CreateButton("Enter", root.transform, out _enterLabel);
            Place(_btnEnterHall, 0.7f, 0.02f, 0.98f, 0.1f);
            _btnEnterHall.GetComponent<Image>().color = Good;
            _btnEnterHall.onClick.AddListener(() => _flow?.OnEnterHall());
            return root;
        }

        GameObject BuildAuction(Transform parent)
        {
            var root = CreatePanel("Screen_Auction", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);

            // Left: rival audience strip under the 3D stage
            var audience = CreatePanel("Audience", root.transform, new Vector2(0.02f, 0.02f), new Vector2(0.5f, 0.14f), Vector2.zero, Vector2.zero);
            audience.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.85f);
            _audienceRow = audience.transform;
            var hlg = audience.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(8, 8, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            for (int i = 0; i < _rivalHeads.Length; i++)
            {
                var head = CreatePanel("Head" + i, audience.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var le = head.AddComponent<LayoutElement>();
                le.flexibleWidth = 1;
                le.preferredHeight = 36;
                var img = head.GetComponent<Image>();
                img.color = new Color(0.25f, 0.27f, 0.32f, 1f);
                _rivalHeads[i] = img;
            }

            var hud = CreatePanel("HudRight", root.transform, new Vector2(0.52f, 0), new Vector2(1, 1), new Vector2(12, 12), new Vector2(-12, -48));
            hud.GetComponent<Image>().color = Panel;

            _aucHud = CreateText("AucHud", hud.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(_aucHud.rectTransform, new Vector2(0, 0.9f), new Vector2(1, 1), new Vector2(14, -10), new Vector2(-14, -6));
            _aucHud.color = Dim;

            _orderCard = CreateText("Order", hud.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(_orderCard.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.9f), new Vector2(14, 0), new Vector2(-14, 0));
            _orderCard.color = Accent;
            _orderCard.horizontalOverflow = HorizontalWrapMode.Wrap;

            var econ = CreatePanel("Econ", hud.transform, new Vector2(0, 0.58f), new Vector2(1, 0.72f), new Vector2(10, 0), new Vector2(-10, 0));
            econ.GetComponent<Image>().color = PanelLight;
            _livePrice = CreateText("Price", econ.transform, "", 16, TextAnchor.MiddleLeft);
            Stretch(_livePrice.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(10, 0), new Vector2(-10, 0));
            _liveBudget = CreateText("Budget", econ.transform, "", 16, TextAnchor.MiddleLeft);
            Stretch(_liveBudget.rectTransform, new Vector2(0, 0), new Vector2(0.65f, 0.5f), new Vector2(10, 0), new Vector2(-4, 0));
            _liveSpeed = CreateText("Speed", econ.transform, "", 16, TextAnchor.MiddleRight);
            Stretch(_liveSpeed.rectTransform, new Vector2(0.55f, 0), new Vector2(1, 0.5f), new Vector2(4, 0), new Vector2(-10, 0));

            var fields = CreatePanel("Fields", hud.transform, new Vector2(0, 0.28f), new Vector2(1, 0.58f), new Vector2(10, 0), new Vector2(-10, 0));
            fields.GetComponent<Image>().color = PanelLight;
            for (int i = 0; i < 5; i++)
            {
                float yMax = 1f - i * 0.2f;
                float yMin = yMax - 0.2f;
                var row = CreatePanel("F" + i, fields.transform, new Vector2(0, yMin), new Vector2(1, yMax), new Vector2(4, 1), new Vector2(-4, -1));
                row.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);
                _fieldRows[i] = row.GetComponent<Image>();
                _fieldLabels[i] = CreateText("L", row.transform, "", 13, TextAnchor.MiddleLeft);
                Stretch(_fieldLabels[i].rectTransform, new Vector2(0, 0), new Vector2(0.35f, 1), new Vector2(8, 0), new Vector2(0, 0));
                _fieldLabels[i].color = Dim;
                _fieldValues[i] = CreateText("V", row.transform, "", 13, TextAnchor.MiddleLeft);
                Stretch(_fieldValues[i].rectTransform, new Vector2(0.35f, 0), new Vector2(1, 1), new Vector2(4, 0), new Vector2(-8, 0));
                _fieldValues[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            _familiarBadge = CreateText("Familiar", hud.transform, "", 14, TextAnchor.MiddleCenter);
            Stretch(_familiarBadge.rectTransform, new Vector2(0.55f, 0.24f), new Vector2(0.98f, 0.28f), Vector2.zero, Vector2.zero);
            _familiarBadge.color = Accent;
            _familiarBadge.gameObject.SetActive(false);

            _resultBanner = CreateText("Banner", hud.transform, "", 20, TextAnchor.MiddleCenter);
            Stretch(_resultBanner.rectTransform, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.28f), Vector2.zero, Vector2.zero);
            _resultBanner.fontStyle = FontStyle.Bold;
            _resultBanner.gameObject.SetActive(false);

            _fundsHint = CreateText("Funds", hud.transform, "", 14, TextAnchor.MiddleCenter);
            Stretch(_fundsHint.rectTransform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.22f), Vector2.zero, Vector2.zero);
            _fundsHint.color = Bad;
            _fundsHint.gameObject.SetActive(false);

            _btnStartLot = CreateButton("StartLot", hud.transform, out _startLotLabel);
            Place(_btnStartLot, 0.06f, 0.1f, 0.94f, 0.17f);
            _btnStartLot.onClick.AddListener(() => _flow?.OnStartLot());

            _btnBuy = CreateButton("Buy", hud.transform, out _buyLabel);
            Place(_btnBuy, 0.06f, 0.02f, 0.94f, 0.09f);
            _btnBuy.GetComponent<Image>().color = Good;
            _btnBuy.onClick.AddListener(() => _flow?.OnBuy());

            _btnSkip = CreateButton("Skip", hud.transform, out _skipLabel);
            Place(_btnSkip, 0.06f, 0.1f, 0.48f, 0.17f);
            _btnSkip.onClick.AddListener(() => _flow?.OnSkip());

            _btnFinishDay = CreateButton("Finish", hud.transform, out _finishLabel);
            Place(_btnFinishDay, 0.52f, 0.1f, 0.94f, 0.17f);
            _btnFinishDay.onClick.AddListener(() => _flow?.OnFinishDay());

            return root;
        }

        GameObject BuildCollectorPopup(Transform parent)
        {
            var root = CreatePanel("CollectorPopup", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -240), new Vector2(380, 240));
            _popupPortrait = CreatePanel("Portrait", card.transform, new Vector2(0, 0.55f), new Vector2(0.32f, 1), new Vector2(16, -16), new Vector2(-8, -16)).GetComponent<Image>();
            _popupPortrait.color = PanelLight;
            _popupPortrait.preserveAspect = true;
            _popupName = CreateText("Name", card.transform, "", 26, TextAnchor.UpperLeft);
            Stretch(_popupName.rectTransform, new Vector2(0.32f, 0.82f), new Vector2(1, 1), new Vector2(8, -12), new Vector2(-16, -8));
            _popupName.color = Accent;
            _popupTagline = CreateText("Tag", card.transform, "", 13, TextAnchor.UpperLeft);
            Stretch(_popupTagline.rectTransform, new Vector2(0.32f, 0.68f), new Vector2(1, 0.82f), new Vector2(8, 0), new Vector2(-16, 0));
            _popupTagline.color = Dim;
            _popupTagline.horizontalOverflow = HorizontalWrapMode.Wrap;
            _popupSpeech = CreateText("Speech", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(_popupSpeech.rectTransform, new Vector2(0, 0.48f), new Vector2(1, 0.55f), new Vector2(16, 0), new Vector2(-16, 0));
            _popupSpeech.horizontalOverflow = HorizontalWrapMode.Wrap;
            _popupTags = CreateText("Tags", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(_popupTags.rectTransform, new Vector2(0, 0.28f), new Vector2(1, 0.48f), new Vector2(16, 0), new Vector2(-16, 0));
            _popupTags.color = Accent;
            _popupTags.horizontalOverflow = HorizontalWrapMode.Wrap;
            _popupWarning = CreateText("Warn", card.transform, "", 13, TextAnchor.UpperLeft);
            Stretch(_popupWarning.rectTransform, new Vector2(0, 0.16f), new Vector2(1, 0.28f), new Vector2(16, 0), new Vector2(-16, 0));
            _popupWarning.color = Bad;
            _popupWarning.horizontalOverflow = HorizontalWrapMode.Wrap;
            _btnPopupStart = CreateButton("Start", card.transform, out _popupStartLabel);
            Place(_btnPopupStart, 0.2f, 0.04f, 0.8f, 0.15f);
            _btnPopupStart.GetComponent<Image>().color = Good;
            _btnPopupStart.onClick.AddListener(() => _flow?.OnCollectorPopupStart());
            return root;
        }

        GameObject BuildPurchaseCard(Transform parent)
        {
            var root = CreatePanel("PurchaseCard", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340, -240), new Vector2(340, 240));
            _pcTitle = CreateText("Title", card.transform, "", 26, TextAnchor.UpperCenter);
            Stretch(_pcTitle.rectTransform, new Vector2(0, 0.82f), new Vector2(1, 1), new Vector2(16, -12), new Vector2(-16, -8));
            _pcTitle.color = Accent;
            _pcArtist = CreateText("Artist", card.transform, "", 18, TextAnchor.UpperCenter);
            Stretch(_pcArtist.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.82f), new Vector2(16, 0), new Vector2(-16, 0));
            _pcMeta = CreateText("Meta", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(_pcMeta.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 0.72f), new Vector2(28, 0), new Vector2(-28, 0));
            _pcMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            _pcFact = CreateText("Fact", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(_pcFact.rectTransform, new Vector2(0, 0.18f), new Vector2(1, 0.4f), new Vector2(28, 0), new Vector2(-28, 0));
            _pcFact.color = Dim;
            _pcFact.horizontalOverflow = HorizontalWrapMode.Wrap;
            _btnPcContinue = CreateButton("Cont", card.transform, out _pcContinueLabel);
            Place(_btnPcContinue, 0.25f, 0.04f, 0.75f, 0.15f);
            _btnPcContinue.GetComponent<Image>().color = Good;
            _btnPcContinue.onClick.AddListener(() => _flow?.OnPurchaseCardDismiss());
            return root;
        }

        GameObject BuildTutorial(Transform parent)
        {
            var root = CreatePanel("Tutorial", parent, new Vector2(0.15f, 0.02f), new Vector2(0.5f, 0.18f), Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            _tutorialText = CreateText("T", root.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(_tutorialText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));
            _tutorialText.horizontalOverflow = HorizontalWrapMode.Wrap;
            return root;
        }

        GameObject BuildReport(Transform parent)
        {
            var root = CreatePanel("Screen_Report", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.92f), Vector2.zero, new Vector2(0, -48));
            _reportTitle = CreateText("Title", card.transform, "", 28, TextAnchor.UpperLeft);
            Stretch(_reportTitle.rectTransform, new Vector2(0, 0.9f), new Vector2(1, 1), new Vector2(20, -10), new Vector2(-20, -6));
            _reportTitle.color = Accent;
            _reportBody = CreateText("Body", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(_reportBody.rectTransform, new Vector2(0, 0.42f), new Vector2(0.55f, 0.9f), new Vector2(20, 0), new Vector2(-10, 0));
            _reportBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _reportBody.verticalOverflow = VerticalWrapMode.Overflow;

            var boostPanel = CreatePanel("Boosters", card.transform, new Vector2(0.55f, 0.18f), new Vector2(1, 0.9f), new Vector2(8, 0), new Vector2(-16, 0));
            boostPanel.GetComponent<Image>().color = PanelLight;
            _boosterHeading = CreateText("BoosterHeading", boostPanel.transform, "", 16, TextAnchor.UpperLeft);
            Stretch(_boosterHeading.rectTransform, new Vector2(0, 0.9f), new Vector2(1, 1), new Vector2(10, -6), new Vector2(-10, -4));
            _boosterHeading.color = Accent;
            _boosterList = CreateScrollContent(boostPanel.transform, "BoosterScroll", new Vector2(0, 0), new Vector2(1, 0.9f));

            _btnReportContinue = CreateButton("Cont", card.transform, out _reportContinueLabel);
            Place(_btnReportContinue, 0.35f, 0.03f, 0.65f, 0.14f);
            _btnReportContinue.GetComponent<Image>().color = Good;
            _btnReportContinue.onClick.AddListener(() => _flow?.OnReportContinue());
            return root;
        }

        GameObject BuildEnd(Transform parent)
        {
            var root = CreatePanel("Screen_End", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -130), new Vector2(300, 130));
            _endTitle = CreateText("Title", card.transform, "", 34, TextAnchor.MiddleCenter);
            Stretch(_endTitle.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 0.9f), new Vector2(12, 0), new Vector2(-12, 0));
            _endTitle.color = Accent;
            _btnRestart = CreateButton("Restart", card.transform, out _restartLabel);
            Place(_btnRestart, 0.2f, 0.12f, 0.8f, 0.35f);
            _btnRestart.GetComponent<Image>().color = Good;
            _btnRestart.onClick.AddListener(() => _flow?.OnRestart());
            return root;
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
            }
        }

        public void ShowCollectorPopup(DayOrder order, CollectorData collector = null)
        {
            if (_collectorPopup == null || order == null) return;
            _popupName.text = order.NameRu;
            _popupTagline.text = order.TaglineRu ?? "";
            _popupSpeech.text = LocaleService.T("collectorPopup.speech");
            _popupTags.text = order.CriteriaLabel;
            _popupWarning.text = LocaleService.T("collectorPopup.warning");
            _popupStartLabel.text = LocaleService.T("collectorPopup.start");
            if (_popupPortrait != null)
            {
                if (collector?.portrait != null)
                {
                    _popupPortrait.sprite = collector.portrait;
                    _popupPortrait.color = Color.white;
                }
                else
                {
                    _popupPortrait.sprite = null;
                    _popupPortrait.color = PanelLight;
                }
            }
            _collectorPopup.SetActive(true);
        }

        public void RaiseRandomRival()
        {
            if (_rivalHeads == null || _rivalHeads.Length == 0) return;
            ResetRivalHeads();
            int idx = Random.Range(0, _rivalHeads.Length);
            _lastRaisedRival = idx;
            _rivalRaiseUntil = Time.unscaledTime + 1.6f;
            if (_rivalHeads[idx] != null)
                _rivalHeads[idx].color = Accent;
        }

        void ResetRivalHeads()
        {
            if (_rivalHeads == null) return;
            for (int i = 0; i < _rivalHeads.Length; i++)
            {
                if (_rivalHeads[i] != null)
                    _rivalHeads[i].color = new Color(0.25f, 0.27f, 0.32f, 1f);
            }
            _lastRaisedRival = -1;
        }

        public void HideCollectorPopup()
        {
            if (_collectorPopup != null) _collectorPopup.SetActive(false);
        }

        public void ShowPurchaseCard(PresentedLot lot, int price)
        {
            if (_purchaseCard == null || lot == null) return;
            _pcTitle.text = lot.TitleRu;
            _pcArtist.text = lot.ArtistRu;
            _pcMeta.text =
                $"{LocaleService.T("auction.field.period")}: {lot.PeriodRu}\n" +
                $"{LocaleService.T("auction.field.genre")}: {lot.GenreRu}\n" +
                $"{LocaleService.T("rarity." + GameCatalog.RarityToString(lot.Rarity))}\n" +
                $"{price:N0} ₽";
            _pcFact.text = lot.FactRu;
            _pcContinueLabel.text = LocaleService.T("purchase.continue");
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
            if (_fundsHint != null)
            {
                _fundsHint.text = LocaleService.T("auction.insufficient");
                _fundsHint.gameObject.SetActive(true);
            }
        }

        public void Refresh(GameSession session)
        {
            if (_chromeTitle) _chromeTitle.text = LocaleService.T("chrome.title");
            RefreshIntro(session);
            RefreshBrief(session);
            RefreshAuction(session);
            RefreshReport(session);
            RefreshEnd(session);
        }

        void RefreshIntro(GameSession session)
        {
            if (_introTitle == null) return;
            _introTitle.text = LocaleService.T("intro.title");
            _introSubtitle.text = LocaleService.T("intro.subtitle");
            _introLede.text = LocaleService.T("intro.lede");
            _introRules.text = "• " + LocaleService.T("intro.rule1") + "\n• " + LocaleService.T("intro.rule2")
                               + "\n• " + LocaleService.T("intro.rule3") + "\n• " + LocaleService.T("intro.rule4");
            bool hasSave = SaveService.HasSave();
            _btnContinue.gameObject.SetActive(hasSave);
            _continueLabel.text = LocaleService.T("intro.continue");
            _startLabel.text = hasSave ? LocaleService.T("intro.newCareer") : LocaleService.T("intro.start");
        }

        void RefreshBrief(GameSession session)
        {
            if (_briefDay == null) return;
            var state = session?.State;
            int day = state?.Day ?? 1;
            int capital = state?.Capital ?? CampaignConfig.StartingCapital;
            _briefDay.text = $"{LocaleService.T("brief.day")} {day} / {CampaignConfig.CampaignLength}";
            _briefCapital.text = $"{LocaleService.T("brief.capital")} {capital:N0} ₽";
            _briefClientHeading.text = LocaleService.T("brief.clientHeading");
            _briefWorkshopHeading.text = LocaleService.T("brief.workshop");
            _enterLabel.text = LocaleService.T("brief.enterHall");
            _resetLabel.text = LocaleService.T("brief.resetProgress");

            if (state?.PendingOrder != null)
            {
                var o = state.PendingOrder;
                var venue = CampaignConfig.GetVenue(state.PendingVenue);
                _briefOrderPreview.text =
                    $"{LocaleService.T("brief.orderPreview")}: {o.CriteriaLabel}\n" +
                    $"{venue.LabelRu} · {state.Lots.Count} {LocaleService.T("brief.lots")} · {o.Budget:N0} ₽";
            }
            else _briefOrderPreview.text = "";

            RebuildCollectorCards(session);
            RebuildUpgradeRows(session);
        }

        void RebuildCollectorCards(GameSession session)
        {
            ClearList(_collectorCards, _collectorList);
            var catalog = _flow?.Catalog;
            if (catalog?.collectors == null || session?.State == null) return;
            string selected = session.State.SelectedBranchId;
            foreach (var c in catalog.collectors)
            {
                if (c == null) continue;
                int progress = session.State.BranchProgress.TryGetValue(c.collectorId, out int p) ? p : 0;
                bool isSelected = c.collectorId == selected;
                var card = CreatePanel("C_" + c.collectorId, _collectorList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var le = card.AddComponent<LayoutElement>();
                le.minHeight = 78;
                le.preferredHeight = 78;
                card.GetComponent<Image>().color = isSelected ? new Color(Accent.r, Accent.g, Accent.b, 0.35f) : PanelLight;
                var btn = card.AddComponent<Button>();
                btn.targetGraphic = card.GetComponent<Image>();
                string id = c.collectorId;
                btn.onClick.AddListener(() => _flow?.SelectBranch(id));

                if (c.portrait != null)
                {
                    var portrait = CreatePanel("P", card.transform, new Vector2(0, 0.1f), new Vector2(0.18f, 0.9f), new Vector2(8, 0), new Vector2(0, 0));
                    var pImg = portrait.GetComponent<Image>();
                    pImg.sprite = c.portrait;
                    pImg.color = Color.white;
                    pImg.preserveAspect = true;
                }

                var t = CreateText("T", card.transform, $"{c.nameRu}\n{LocaleService.T("brief.mission")} {progress + 1}/{c.LadderLength}", 14, TextAnchor.MiddleLeft);
                Stretch(t.rectTransform, new Vector2(0.2f, 0), new Vector2(1, 1), new Vector2(8, 4), new Vector2(-12, -4));
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                _collectorCards.Add(card);
            }
        }

        void RebuildUpgradeRows(GameSession session)
        {
            ClearList(_upgradeRows, _upgradeList);
            if (session?.State == null) return;
            foreach (var u in CampaignConfig.MetaUpgrades)
            {
                bool owned = session.State.Upgrades.Contains(u.Id);
                bool canBuy = !owned && session.State.Capital >= u.Cost;
                var row = CreatePanel("U_" + u.Id, _upgradeList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var le = row.AddComponent<LayoutElement>();
                le.minHeight = 64;
                le.preferredHeight = 64;
                row.GetComponent<Image>().color = PanelLight;
                var t = CreateText("T", row.transform, $"{u.NameRu} — {u.Cost:N0} ₽\n{u.DescRu}", 12, TextAnchor.MiddleLeft);
                Stretch(t.rectTransform, new Vector2(0, 0), new Vector2(0.72f, 1), new Vector2(8, 2), new Vector2(-4, -2));
                t.color = owned ? Dim : TextColor;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                var buy = CreateButton("B", row.transform, out var bl);
                Place(buy, 0.74f, 0.2f, 0.96f, 0.8f);
                bl.text = owned ? LocaleService.T("brief.owned") : LocaleService.T("brief.buy");
                buy.interactable = canBuy;
                buy.GetComponent<Image>().color = canBuy ? Good : Dim;
                string uid = u.Id;
                buy.onClick.AddListener(() => _flow?.BuyUpgrade(uid));
                _upgradeRows.Add(row);
            }
        }

        void RefreshAuction(GameSession session)
        {
            if (_aucHud == null) return;
            var state = session?.State;
            if (state == null) return;

            var venue = CampaignConfig.GetVenue(state.CurrentVenue);
            _aucHud.text =
                $"{LocaleService.T("auction.day")} {state.Day} · {LocaleService.T("auction.venue")} {venue.LabelRu} · " +
                $"{LocaleService.T("auction.lot")} {state.CurrentLotIndex + 1}/{state.Lots.Count}";

            var order = state.DayOrders.Count > 0 ? state.DayOrders[0] : state.PendingOrder;
            _orderCard.text = order != null
                ? $"{order.NameRu}\n{order.CriteriaLabel}\n{LocaleService.T("auction.clientBudget")} {order.Budget:N0} ₽"
                : "";

            var lot = state.CurrentLot;
            int price = lot != null
                ? AuctionRules.ComputeLivePrice(lot, state.RevealStep, AuctionRules.GetPriceStepPct(state))
                : 0;
            float speed = AuctionRules.ComputeSpeedMultiplier(state.RevealStep, AuctionRules.GetSpeedFloor(state));
            _livePrice.text = $"{LocaleService.T("auction.currentPrice")}: {price:N0} ₽";
            _liveBudget.text = $"{LocaleService.T("auction.budgetLeft")}: {state.ClientBudgetRemaining:N0} ₽";
            _liveSpeed.text = $"{LocaleService.T("auction.speed")}{speed:0.00}";

            RefreshRevealFields(state, lot, order);
            _familiarBadge.gameObject.SetActive(lot != null && lot.Familiar);
            _familiarBadge.text = LocaleService.T("auction.familiar");

            bool showBanner = !string.IsNullOrEmpty(state.LastLotResult);
            _resultBanner.gameObject.SetActive(showBanner);
            if (showBanner)
            {
                if (state.LastLotResult == "won")
                {
                    _resultBanner.text = LocaleService.T("auction.won");
                    _resultBanner.color = Good;
                }
                else if (state.LastLotResult == "lost")
                {
                    _resultBanner.text = LocaleService.T("auction.lost");
                    _resultBanner.color = Bad;
                }
                else
                {
                    _resultBanner.text = LocaleService.T("auction.skip");
                    _resultBanner.color = Dim;
                }
            }

            if (_fundsHint != null && Time.unscaledTime > _fundsFlashUntil)
                _fundsHint.gameObject.SetActive(false);

            bool standby = state.AwaitingLotStart || IsCollectorPopupVisible;
            bool busy = state.LotResolved || state.FastForwarding || _purchaseCardVisible;
            _btnStartLot.gameObject.SetActive(state.AwaitingLotStart && !IsCollectorPopupVisible);
            _btnSkip.gameObject.SetActive(!state.AwaitingLotStart);
            _btnFinishDay.gameObject.SetActive(!state.AwaitingLotStart);
            _btnBuy.gameObject.SetActive(!state.AwaitingLotStart);
            _btnBuy.interactable = !standby && !busy && (!state.TutorialPaused || state.TutorialStep == TutorialStep.BuyMatch);
            _btnSkip.interactable = !standby && !busy && (!state.TutorialPaused || state.TutorialStep == TutorialStep.SkipMiss);
            _btnFinishDay.interactable = !state.TutorialPaused && !busy;

            _startLotLabel.text = LocaleService.T("auction.startLot");
            _buyLabel.text = LocaleService.T("auction.buy");
            _skipLabel.text = LocaleService.T("auction.skip");
            _finishLabel.text = LocaleService.T("auction.finishDay");

            bool showTut = state.TutorialPaused && state.TutorialStep != TutorialStep.None;
            if (_tutorial != null)
            {
                _tutorial.SetActive(showTut && _auction.activeSelf);
                if (showTut)
                {
                    _tutorialText.text = state.TutorialStep == TutorialStep.BuyMatch
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
                _fieldLabels[i].text = LocaleService.T("auction.field." + id);
                string raw = lot == null ? "—" : FieldValue(lot, id);
                bool revealed = state.RevealStep > i
                                || (!string.IsNullOrEmpty(state.FreeRevealedField) && state.FreeRevealedField == id);
                _fieldValues[i].text = revealed ? raw : AuctionRules.MaskValue(raw);
                _fieldValues[i].color = revealed ? TextColor : Dim;

                bool isTarget = order != null && IsOrderTarget(order, id);
                _fieldRows[i].color = isTarget
                    ? new Color(Accent.r, Accent.g, Accent.b, 0.22f)
                    : new Color(0, 0, 0, 0.15f);
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
            if (_reportTitle == null) return;
            var state = session?.State;
            var r = state?.PendingResult;
            _reportTitle.text = $"{LocaleService.T("report.title")} {state?.Day ?? 0}";
            if (r == null)
            {
                _reportBody.text = "";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(r.OrdersFulfilled
                ? LocaleService.T("report.fulfilled")
                : LocaleService.T("report.unfulfilled"));
            if (r.SavedByCreditLine) sb.AppendLine(LocaleService.T("report.creditLine"));
            sb.AppendLine();
            sb.AppendLine($"{LocaleService.T("report.commission")} {r.TotalCommission:N0} ₽");
            sb.AppendLine($"{LocaleService.T("report.capitalEnd")} {r.ProjectedCapital:N0} ₽");
            sb.AppendLine();
            sb.AppendLine(LocaleService.T("report.ledger"));
            sb.AppendLine($"{LocaleService.T("report.start")}: {r.StartingCapital:N0}");
            sb.AppendLine($"{LocaleService.T("report.net")}: {r.Net:N0}");
            sb.AppendLine($"{LocaleService.T("report.end")}: {r.ProjectedCapital:N0}");
            sb.AppendLine();
            if (r.PurchaseDetails != null)
            {
                for (int i = 0; i < r.PurchaseDetails.Length; i++)
                {
                    var d = r.PurchaseDetails[i];
                    string mark = d.Matched ? LocaleService.T("report.correct") : LocaleService.T("report.incorrect");
                    sb.AppendLine($"• {d.TitleRu} — {mark} ({d.Amount:N0} ₽)");
                }
            }
            _reportBody.text = sb.ToString();

            bool showBoosters = r.Pass && state.Day < CampaignConfig.CampaignLength;
            RebuildBoosterRows(session, showBoosters);
            _reportContinueLabel.text = r.Pass
                ? LocaleService.T("report.continue")
                : LocaleService.T("report.finish");
        }

        void RebuildBoosterRows(GameSession session, bool show)
        {
            ClearList(_boosterRows, _boosterList);
            if (_boosterHeading != null)
                _boosterHeading.text = show ? LocaleService.T("report.boosters") : "";
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

                var row = CreatePanel("B_" + id, _boosterList, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var le = row.AddComponent<LayoutElement>();
                le.minHeight = 70;
                le.preferredHeight = 70;
                row.GetComponent<Image>().color = Panel;
                var t = CreateText("T", row.transform, $"{def.NameRu} — {cost:N0} ₽\n{def.DescRu}", 11, TextAnchor.MiddleLeft);
                Stretch(t.rectTransform, new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(6, 2), new Vector2(-4, -2));
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                var buy = CreateButton("Buy", row.transform, out var bl);
                Place(buy, 0.72f, 0.2f, 0.96f, 0.8f);
                bl.text = owned ? LocaleService.T("report.ownedBooster") : LocaleService.T("report.buyBooster");
                buy.interactable = canBuy;
                buy.GetComponent<Image>().color = canBuy ? Good : Dim;
                string bid = id;
                buy.onClick.AddListener(() => _flow?.BuyBooster(bid));
                _boosterRows.Add(row);
            }
        }

        void RefreshEnd(GameSession session)
        {
            if (_endTitle == null) return;
            var state = session?.State;
            bool bankrupt = state?.PendingResult != null && !state.PendingResult.Pass;
            _endTitle.text = bankrupt ? LocaleService.T("end.bankruptTitle") : LocaleService.T("end.careerTitle");
            _restartLabel.text = LocaleService.T("end.restart");
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

        static void ClearList(List<GameObject> list, Transform parent)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) Destroy(list[i]);
            }
            list.Clear();
        }

        Transform CreateScrollContent(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            Stretch(scrollGo.GetComponent<RectTransform>(), aMin, aMax, new Vector2(8, 8), new Vector2(-8, -8));
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = CreatePanel("Viewport", scrollGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 0);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = crt;
            return content.transform;
        }

        static GameObject CreatePanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), aMin, aMax, offMin, offMax);
            go.GetComponent<Image>().color = Panel;
            return go;
        }

        static Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(string name, Transform parent, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = Accent;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            label = CreateText("Label", go.transform, name, 16, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 2), new Vector2(-6, -2));
            label.color = Color.black;
            return btn;
        }

        static void Place(Button btn, float xMin, float yMin, float xMax, float yMax)
        {
            Stretch(btn.GetComponent<RectTransform>(), new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero);
        }

        static void Stretch(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }
    }
}
