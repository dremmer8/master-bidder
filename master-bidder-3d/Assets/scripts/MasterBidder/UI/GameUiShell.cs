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

            var rootImg = _brief.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.color = GameUiStyle.ScreenBg;
            }

            DressFixedBar(_brief.transform.Find("DayBar"), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(220f, 44f), GameUiSprites.BarDay);
            DressFixedBar(_brief.transform.Find("CapBar"), new Vector2(1f, 1f), new Vector2(-20f, -12f), new Vector2(250f, 44f), GameUiSprites.BarCurrency);

            if (_b.briefDay != null)
            {
                _b.briefDay.fontSize = 17;
                _b.briefDay.fontStyle = FontStyle.Bold;
                _b.briefDay.color = GameUiStyle.OnDark;
            }

            if (_b.briefCapital != null)
            {
                _b.briefCapital.fontSize = 17;
                _b.briefCapital.fontStyle = FontStyle.Bold;
                _b.briefCapital.color = GameUiStyle.OnDark;
            }

            var clients = _brief.transform.Find("Clients");
            if (clients != null)
            {
                StretchUi(clients.GetComponent<RectTransform>(), new Vector2(0, 0.12f), new Vector2(0.56f, 0.9f), new Vector2(16, 0), new Vector2(-6, -64));
                var clientsImg = clients.GetComponent<Image>();
                if (clientsImg != null) GameUiStyle.ApplyFramedPanel(clientsImg);

                if (_b.briefClientHeading != null)
                {
                    _b.briefClientHeading.fontSize = 20;
                    _b.briefClientHeading.fontStyle = FontStyle.Bold;
                    _b.briefClientHeading.color = GameUiStyle.Accent;
                    _b.briefClientHeading.alignment = TextAnchor.MiddleLeft;
                    StretchUi(_b.briefClientHeading.rectTransform, new Vector2(0, 0.9f), Vector2.one, new Vector2(22, -10), new Vector2(-22, -6));
                }

                var collectorScroll = clients.Find("CollectorScroll");
                if (collectorScroll != null)
                {
                    StretchUi(collectorScroll.GetComponent<RectTransform>(), new Vector2(0, 0.22f), new Vector2(1, 0.9f), new Vector2(14, 10), new Vector2(-14, -10));
                    TightenScrollSpacing(collectorScroll, 5);
                }

                EnsureOrderPreviewPlate(clients);
            }

            var workshop = _brief.transform.Find("Workshop");
            if (workshop != null)
            {
                StretchUi(workshop.GetComponent<RectTransform>(), new Vector2(0.56f, 0.12f), new Vector2(1, 0.9f), new Vector2(6, 0), new Vector2(-16, -64));
                var workshopImg = workshop.GetComponent<Image>();
                if (workshopImg != null) GameUiStyle.ApplyFramedPanel(workshopImg);

                if (_b.briefWorkshopHeading != null)
                {
                    _b.briefWorkshopHeading.fontSize = 20;
                    _b.briefWorkshopHeading.fontStyle = FontStyle.Bold;
                    _b.briefWorkshopHeading.color = GameUiStyle.Accent;
                    _b.briefWorkshopHeading.alignment = TextAnchor.MiddleLeft;
                    StretchUi(_b.briefWorkshopHeading.rectTransform, new Vector2(0, 0.9f), Vector2.one, new Vector2(22, -10), new Vector2(-22, -6));
                }

                var upgradeScroll = workshop.Find("UpgradeScroll");
                if (upgradeScroll != null)
                {
                    StretchUi(upgradeScroll.GetComponent<RectTransform>(), new Vector2(0, 0.02f), new Vector2(1, 0.9f), new Vector2(14, 10), new Vector2(-14, -10));
                    TightenScrollSpacing(upgradeScroll, 4);
                }
            }

            if (_b.btnReset != null)
                StretchUi(_b.btnReset.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.2f, 0.095f), Vector2.zero, Vector2.zero);
            if (_b.btnEnterHall != null)
                StretchUi(_b.btnEnterHall.GetComponent<RectTransform>(), new Vector2(0.72f, 0.02f), new Vector2(0.98f, 0.095f), Vector2.zero, Vector2.zero);
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

            var hud = _auction.transform.Find("HudRight");
            if (hud == null) return;

            EnsureEffectsHud();

            // Narrow side rail — leave the painting room breathing room.
            StretchUi(hud.GetComponent<RectTransform>(), new Vector2(0.72f, 0.03f), new Vector2(0.985f, 0.97f), new Vector2(6, 8), new Vector2(-12, -8));
            var hudImg = hud.GetComponent<Image>();
            if (hudImg != null) GameUiStyle.ApplyFramedPanel(hudImg);

            if (_b.aucHud != null)
            {
                _b.aucHud.fontSize = 13;
                _b.aucHud.fontStyle = FontStyle.Bold;
                _b.aucHud.color = GameUiStyle.TextColor;
                _b.aucHud.alignment = TextAnchor.MiddleLeft;
                _b.aucHud.horizontalOverflow = HorizontalWrapMode.Wrap;
                StretchUi(_b.aucHud.rectTransform, new Vector2(0, 0.91f), Vector2.one, new Vector2(16, -10), new Vector2(-16, -6));
            }

            EnsurePopupPlate(hud, "OrderPlate", _b.orderCard,
                new Vector2(0, 0.76f), new Vector2(1, 0.91f), new Vector2(14, 2), new Vector2(-14, -2));
            if (_b.orderCard != null)
            {
                _b.orderCard.fontSize = 14;
                _b.orderCard.fontStyle = FontStyle.Bold;
                _b.orderCard.color = GameUiStyle.Accent;
                _b.orderCard.alignment = TextAnchor.MiddleLeft;
                _b.orderCard.lineSpacing = 1.12f;
                _b.orderCard.horizontalOverflow = HorizontalWrapMode.Wrap;
                StretchUi(_b.orderCard.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6));
            }

            var econ = hud.Find("Econ");
            if (econ != null)
            {
                StretchUi(econ.GetComponent<RectTransform>(), new Vector2(0, 0.62f), new Vector2(1, 0.75f), new Vector2(14, 0), new Vector2(-14, 0));
                var econImg = econ.GetComponent<Image>();
                if (econImg != null) GameUiStyle.ApplyCard(econImg);
            }

            if (_b.livePrice != null)
            {
                _b.livePrice.fontSize = 17;
                _b.livePrice.fontStyle = FontStyle.Bold;
                _b.livePrice.color = GameUiStyle.TextColor;
                StretchUi(_b.livePrice.rectTransform, new Vector2(0, 0.48f), Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
            }
            if (_b.liveBudget != null)
            {
                _b.liveBudget.fontSize = 13;
                _b.liveBudget.color = GameUiStyle.TextColor;
                StretchUi(_b.liveBudget.rectTransform, new Vector2(0, 0), new Vector2(0.58f, 0.48f), new Vector2(12, 0), new Vector2(-4, 0));
            }
            if (_b.liveSpeed != null)
            {
                _b.liveSpeed.fontSize = 13;
                _b.liveSpeed.color = GameUiStyle.TextColor;
                StretchUi(_b.liveSpeed.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.48f), new Vector2(4, 0), new Vector2(-12, 0));
            }

            var fields = hud.Find("Fields");
            if (fields != null)
            {
                StretchUi(fields.GetComponent<RectTransform>(), new Vector2(0, 0.28f), new Vector2(1, 0.6f), new Vector2(14, 0), new Vector2(-14, 0));
                var fieldsImg = fields.GetComponent<Image>();
                if (fieldsImg != null) GameUiStyle.ApplyCard(fieldsImg);
            }

            if (_b.fieldLabels != null)
            {
                // Match factory: taller band for fact (index 3).
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

            if (_b.btnStartLot != null)
                StretchUi(_b.btnStartLot.GetComponent<RectTransform>(), new Vector2(0.07f, 0.1f), new Vector2(0.93f, 0.16f), Vector2.zero, Vector2.zero);
            if (_b.btnBuy != null)
                StretchUi(_b.btnBuy.GetComponent<RectTransform>(), new Vector2(0.07f, 0.02f), new Vector2(0.93f, 0.09f), Vector2.zero, Vector2.zero);
            if (_b.btnSkip != null)
                StretchUi(_b.btnSkip.GetComponent<RectTransform>(), new Vector2(0.07f, 0.1f), new Vector2(0.48f, 0.16f), Vector2.zero, Vector2.zero);
            if (_b.btnFinishDay != null)
                StretchUi(_b.btnFinishDay.GetComponent<RectTransform>(), new Vector2(0.52f, 0.1f), new Vector2(0.93f, 0.16f), Vector2.zero, Vector2.zero);
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

        void EnsureOrderPreviewPlate(Transform clients)
        {
            if (_b.briefOrderPreview == null || clients == null) return;

            Transform plate = clients.Find("OrderPlate");
            if (plate == null)
            {
                var plateGo = new GameObject("OrderPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                plateGo.transform.SetParent(clients, false);
                plate = plateGo.transform;
                _b.briefOrderPreview.transform.SetParent(plate, false);
            }

            StretchUi(plate.GetComponent<RectTransform>(), new Vector2(0, 0.03f), new Vector2(1, 0.2f), new Vector2(16, 8), new Vector2(-16, -4));
            var plateImg = plate.GetComponent<Image>();
            if (plateImg != null) GameUiStyle.ApplyCard(plateImg);

            _b.briefOrderPreview.fontSize = 15;
            _b.briefOrderPreview.fontStyle = FontStyle.Bold;
            _b.briefOrderPreview.color = GameUiStyle.TextColor;
            _b.briefOrderPreview.alignment = TextAnchor.MiddleLeft;
            _b.briefOrderPreview.horizontalOverflow = HorizontalWrapMode.Wrap;
            _b.briefOrderPreview.lineSpacing = 1.15f;
            StretchUi(_b.briefOrderPreview.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 6), new Vector2(-14, -6));
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
            RefreshIntro(session);
            RefreshBrief(session);
            RefreshAuction(session);
            RefreshReport(session);
            RefreshEnd(session);
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
            var state = session?.State;
            int day = state?.Day ?? 1;
            int capital = state?.Capital ?? CampaignConfig.StartingCapital;
            _b.briefDay.text = $"{LocaleService.T("brief.day")} {day} / {CampaignConfig.CampaignLength}";
            _b.briefCapital.text = $"{LocaleService.T("brief.capital")} {capital:N0} ₽";
            _b.briefClientHeading.text = LocaleService.T("brief.clientHeading");
            _b.briefWorkshopHeading.text = LocaleService.T("brief.workshop");
            _b.enterLabel.text = LocaleService.T("brief.enterHall");
            _b.resetLabel.text = LocaleService.T("brief.resetProgress");

            if (state?.PendingOrder != null)
            {
                var o = state.PendingOrder;
                var venue = CampaignConfig.GetVenue(state.PendingVenue);
                _b.briefOrderPreview.text =
                    $"{LocaleService.T("brief.orderPreview")}: {o.CriteriaLabel}\n" +
                    $"{venue.LabelRu} · {state.Lots.Count} {LocaleService.T("brief.lots")} · {o.Budget:N0} ₽";
            }
            else _b.briefOrderPreview.text = "";

            RebuildCollectorCards(session);
            RebuildUpgradeRows(session);
            RebuildActiveEffects(state);
        }

        void RebuildCollectorCards(GameSession session)
        {
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
                    le.minHeight = 56;
                    le.preferredHeight = 56;
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
                    view.portraitRoot.SetActive(c.portrait != null);
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
            var state = session?.State;
            if (state == null) return;

            var venue = CampaignConfig.GetVenue(state.CurrentVenue);
            _b.aucHud.text =
                $"{LocaleService.T("auction.day")} {state.Day} · {LocaleService.T("auction.venue")} {venue.LabelRu} · " +
                $"{LocaleService.T("auction.lot")} {state.CurrentLotIndex + 1}/{state.Lots.Count}";

            var order = state.DayOrders.Count > 0 ? state.DayOrders[0] : state.PendingOrder;
            _b.orderCard.text = order != null
                ? $"{order.NameRu}\n{order.CriteriaLabel}\n{LocaleService.T("auction.clientBudget")} {order.Budget:N0} ₽"
                : "";

            var lot = state.CurrentLot;
            int price = lot != null
                ? AuctionRules.ComputeLivePrice(lot, state.RevealStep, AuctionRules.GetPriceStepPct(state))
                : 0;
            float speed = AuctionRules.ComputeSpeedMultiplier(state.RevealStep, AuctionRules.GetSpeedFloor(state));
            _b.livePrice.text = $"{LocaleService.T("auction.currentPrice")}: {price:N0} ₽";
            _b.liveBudget.text = $"{LocaleService.T("auction.budgetLeft")}: {state.ClientBudgetRemaining:N0} ₽";
            _b.liveSpeed.text = $"{LocaleService.T("auction.speed")}{speed:0.00}";

            RefreshRevealFields(state, lot, order);
            _b.familiarBadge.gameObject.SetActive(lot != null && lot.Familiar);
            _b.familiarBadge.text = LocaleService.T("auction.familiar");

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
            bool busy = state.LotResolved || state.FastForwarding || _purchaseCardVisible;
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
            if (_b?.reportTitle == null) return;
            var state = session?.State;
            var r = state?.PendingResult;
            _b.reportTitle.text = $"{LocaleService.T("report.title")} {state?.Day ?? 0}";
            if (r == null)
            {
                _b.reportBody.text = "";
                return;
            }

            // Play once when report content first appears for this day result.
            if (!_reportSoundPlayed)
            {
                _reportSoundPlayed = true;
                if (r.Pass) AudioService.PlayDayPass();
                else AudioService.PlayDayFail();
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
            _b.reportBody.text = sb.ToString();

            bool showBoosters = r.Pass && state.Day < CampaignConfig.CampaignLength;
            RebuildBoosterRows(session, showBoosters);
            _b.reportContinueLabel.text = r.Pass
                ? LocaleService.T("report.continue")
                : LocaleService.T("report.finish");
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
