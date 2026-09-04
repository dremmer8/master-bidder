using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Builds the default Master Bidder uGUI hierarchy.
    /// Used by the Editor prefab generator and as a runtime fallback when prefabs are missing.
    /// </summary>
    public static class GameUiHierarchyFactory
    {
        public static GameObject BuildCollectorCard()
        {
            var card = CreatePanel("CollectorCard", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = card.AddComponent<LayoutElement>();
            le.minHeight = 78;
            le.preferredHeight = 78;
            card.GetComponent<Image>().color = GameUiStyle.PanelLight;
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();

            var portrait = CreatePanel("P", card.transform, new Vector2(0, 0.1f), new Vector2(0.18f, 0.9f), new Vector2(8, 0), Vector2.zero);
            var pImg = portrait.GetComponent<Image>();
            pImg.color = Color.white;
            pImg.preserveAspect = true;

            var t = CreateText("T", card.transform, "", 14, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(0.2f, 0), Vector2.one, new Vector2(8, 4), new Vector2(-12, -4));
            t.horizontalOverflow = HorizontalWrapMode.Wrap;

            var view = card.AddComponent<CollectorCardView>();
            view.background = card.GetComponent<Image>();
            view.portrait = pImg;
            view.portraitRoot = portrait;
            view.label = t;
            view.button = btn;
            return card;
        }

        public static GameObject BuildUpgradeRow()
        {
            var row = CreatePanel("UpgradeRow", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 64;
            le.preferredHeight = 64;
            row.GetComponent<Image>().color = GameUiStyle.PanelLight;

            var t = CreateText("T", row.transform, "", 12, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(0, 0), new Vector2(0.72f, 1), new Vector2(8, 2), new Vector2(-4, -2));
            t.horizontalOverflow = HorizontalWrapMode.Wrap;

            var buy = CreateButton("B", row.transform, out var bl);
            Place(buy, 0.74f, 0.2f, 0.96f, 0.8f);

            var view = row.AddComponent<UpgradeRowView>();
            view.background = row.GetComponent<Image>();
            view.label = t;
            view.buyButton = buy;
            view.buyLabel = bl;
            return row;
        }

        public static GameObject BuildBoosterRow()
        {
            var row = CreatePanel("BoosterRow", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 70;
            le.preferredHeight = 70;
            row.GetComponent<Image>().color = GameUiStyle.Panel;

            var t = CreateText("T", row.transform, "", 11, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(6, 2), new Vector2(-4, -2));
            t.horizontalOverflow = HorizontalWrapMode.Wrap;

            var buy = CreateButton("Buy", row.transform, out var bl);
            Place(buy, 0.72f, 0.2f, 0.96f, 0.8f);

            var view = row.AddComponent<BoosterRowView>();
            view.background = row.GetComponent<Image>();
            view.label = t;
            view.buyButton = buy;
            view.buyLabel = bl;
            return row;
        }

        // в”Ђв”Ђв”Ђ Main GameUI prefab в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        public static GameObject BuildGameUi()
        {
            var canvasGo = new GameObject("GameUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var b = canvasGo.AddComponent<GameUiBindings>();
            b.canvas = canvas;

            BuildChrome(canvasGo.transform, b);
            b.intro = BuildIntro(canvasGo.transform, b);
            b.brief = BuildBrief(canvasGo.transform, b);
            b.auction = BuildAuction(canvasGo.transform, b);
            b.report = BuildReport(canvasGo.transform, b);
            b.end = BuildEnd(canvasGo.transform, b);
            b.collectorPopup = BuildCollectorPopup(canvasGo.transform, b);
            b.purchaseCard = BuildPurchaseCard(canvasGo.transform, b);
            b.tutorial = BuildTutorial(canvasGo.transform, b);

            b.collectorPopup.SetActive(false);
            b.purchaseCard.SetActive(false);
            b.tutorial.SetActive(false);
            return canvasGo;
        }

        static void BuildChrome(Transform parent, GameUiBindings b)
        {
            var bar = CreatePanel("Chrome", parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), Vector2.zero);
            bar.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            b.chromeTitle = CreateText("Title", bar.transform, "", 14, TextAnchor.MiddleLeft);
            Stretch(b.chromeTitle.rectTransform, new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(14, 0), new Vector2(-8, 0));

            var langGo = new GameObject("Lang", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            langGo.transform.SetParent(bar.transform, false);
            Stretch(langGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.1f), new Vector2(0.99f, 0.9f), Vector2.zero, Vector2.zero);
            langGo.GetComponent<Image>().color = GameUiStyle.Panel;
            b.langDropdown = langGo.GetComponent<Dropdown>();
            b.langDropdown.targetGraphic = langGo.GetComponent<Image>();
            var caption = CreateText("Caption", langGo.transform, "RU", 14, TextAnchor.MiddleCenter);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            b.langDropdown.captionText = caption;

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
            b.langDropdown.template = template.GetComponent<RectTransform>();
            b.langDropdown.itemText = itemLabel;
            b.langDropdown.options.Clear();
            b.langDropdown.options.Add(new Dropdown.OptionData("Р СѓСЃСЃРєРёР№"));
            b.langDropdown.options.Add(new Dropdown.OptionData("English"));
        }

        static GameObject BuildIntro(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Intro", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = GameUiStyle.Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440, -300), new Vector2(440, 260));
            b.introTitle = CreateText("Title", card.transform, "", 40, TextAnchor.UpperCenter);
            Stretch(b.introTitle.rectTransform, new Vector2(0, 0.8f), Vector2.one, new Vector2(20, -12), new Vector2(-20, -8));
            b.introTitle.color = GameUiStyle.Accent;
            b.introTitle.fontStyle = FontStyle.Bold;
            b.introSubtitle = CreateText("Sub", card.transform, "", 20, TextAnchor.UpperCenter);
            Stretch(b.introSubtitle.rectTransform, new Vector2(0, 0.7f), new Vector2(1, 0.8f), new Vector2(20, 0), new Vector2(-20, 0));
            b.introSubtitle.color = GameUiStyle.Dim;
            b.introLede = CreateText("Lede", card.transform, "", 16, TextAnchor.UpperLeft);
            Stretch(b.introLede.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 0.7f), new Vector2(28, 0), new Vector2(-28, 0));
            b.introLede.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.introRules = CreateText("Rules", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.introRules.rectTransform, new Vector2(0, 0.16f), new Vector2(1, 0.42f), new Vector2(28, 0), new Vector2(-28, 0));
            b.introRules.color = GameUiStyle.Dim;
            b.introRules.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.btnContinue = CreateButton("Continue", card.transform, out b.continueLabel);
            Place(b.btnContinue, 0.05f, 0.03f, 0.48f, 0.13f);
            b.btnStart = CreateButton("Start", card.transform, out b.startLabel);
            Place(b.btnStart, 0.52f, 0.03f, 0.95f, 0.13f);
            b.btnStart.GetComponent<Image>().color = GameUiStyle.Good;
            return root;
        }

        static GameObject BuildBrief(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Brief", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = GameUiStyle.Bg;

            b.briefDay = CreateText("Day", root.transform, "", 22, TextAnchor.MiddleLeft);
            Stretch(b.briefDay.rectTransform, new Vector2(0, 0.92f), new Vector2(0.4f, 1), new Vector2(24, -48), new Vector2(0, -8));
            b.briefDay.color = GameUiStyle.Accent;
            b.briefCapital = CreateText("Cap", root.transform, "", 22, TextAnchor.MiddleRight);
            Stretch(b.briefCapital.rectTransform, new Vector2(0.4f, 0.92f), Vector2.one, new Vector2(0, -48), new Vector2(-24, -8));

            var left = CreatePanel("Clients", root.transform, new Vector2(0, 0.12f), new Vector2(0.58f, 0.9f), new Vector2(16, 0), new Vector2(-8, -56));
            b.briefClientHeading = CreateText("H", left.transform, "", 18, TextAnchor.UpperLeft);
            Stretch(b.briefClientHeading.rectTransform, new Vector2(0, 0.92f), Vector2.one, new Vector2(12, -8), new Vector2(-12, -4));
            b.briefClientHeading.color = GameUiStyle.Accent;
            b.collectorList = CreateScrollContent(left.transform, "CollectorScroll", new Vector2(0, 0.28f), new Vector2(1, 0.92f));
            b.briefOrderPreview = CreateText("OrderPrev", left.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.briefOrderPreview.rectTransform, new Vector2(0, 0), new Vector2(1, 0.28f), new Vector2(12, 8), new Vector2(-12, -4));
            b.briefOrderPreview.color = GameUiStyle.Dim;
            b.briefOrderPreview.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.briefOrderPreview.verticalOverflow = VerticalWrapMode.Overflow;

            var right = CreatePanel("Workshop", root.transform, new Vector2(0.58f, 0.12f), new Vector2(1, 0.9f), new Vector2(8, 0), new Vector2(-16, -56));
            b.briefWorkshopHeading = CreateText("WH", right.transform, "", 18, TextAnchor.UpperLeft);
            Stretch(b.briefWorkshopHeading.rectTransform, new Vector2(0, 0.92f), Vector2.one, new Vector2(12, -8), new Vector2(-12, -4));
            b.briefWorkshopHeading.color = GameUiStyle.Accent;
            b.upgradeList = CreateScrollContent(right.transform, "UpgradeScroll", new Vector2(0, 0), new Vector2(1, 0.92f));

            b.btnReset = CreateButton("Reset", root.transform, out b.resetLabel);
            Place(b.btnReset, 0.02f, 0.02f, 0.22f, 0.1f);
            b.btnEnterHall = CreateButton("Enter", root.transform, out b.enterLabel);
            Place(b.btnEnterHall, 0.7f, 0.02f, 0.98f, 0.1f);
            b.btnEnterHall.GetComponent<Image>().color = GameUiStyle.Good;
            return root;
        }

        static GameObject BuildAuction(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Auction", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0.12f);

            var audience = CreatePanel("Audience", root.transform, new Vector2(0.02f, 0.02f), new Vector2(0.5f, 0.14f), Vector2.zero, Vector2.zero);
            audience.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.85f);
            b.audienceRow = audience.transform;
            var hlg = audience.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.padding = new RectOffset(8, 8, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            b.rivalHeads = new Image[15];
            for (int i = 0; i < b.rivalHeads.Length; i++)
            {
                var head = CreatePanel("Head" + i, audience.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var le = head.AddComponent<LayoutElement>();
                le.flexibleWidth = 1;
                le.preferredHeight = 36;
                var img = head.GetComponent<Image>();
                img.color = GameUiStyle.RivalIdle;
                b.rivalHeads[i] = img;
            }

            var hud = CreatePanel("HudRight", root.transform, new Vector2(0.52f, 0), Vector2.one, new Vector2(12, 12), new Vector2(-12, -48));
            hud.GetComponent<Image>().color = GameUiStyle.Panel;

            b.aucHud = CreateText("AucHud", hud.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.aucHud.rectTransform, new Vector2(0, 0.9f), Vector2.one, new Vector2(14, -10), new Vector2(-14, -6));
            b.aucHud.color = GameUiStyle.Dim;

            b.orderCard = CreateText("Order", hud.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.orderCard.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.9f), new Vector2(14, 0), new Vector2(-14, 0));
            b.orderCard.color = GameUiStyle.Accent;
            b.orderCard.horizontalOverflow = HorizontalWrapMode.Wrap;

            var econ = CreatePanel("Econ", hud.transform, new Vector2(0, 0.58f), new Vector2(1, 0.72f), new Vector2(10, 0), new Vector2(-10, 0));
            econ.GetComponent<Image>().color = GameUiStyle.PanelLight;
            b.livePrice = CreateText("Price", econ.transform, "", 16, TextAnchor.MiddleLeft);
            Stretch(b.livePrice.rectTransform, new Vector2(0, 0.5f), Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
            b.liveBudget = CreateText("Budget", econ.transform, "", 16, TextAnchor.MiddleLeft);
            Stretch(b.liveBudget.rectTransform, new Vector2(0, 0), new Vector2(0.65f, 0.5f), new Vector2(10, 0), new Vector2(-4, 0));
            b.liveSpeed = CreateText("Speed", econ.transform, "", 16, TextAnchor.MiddleRight);
            Stretch(b.liveSpeed.rectTransform, new Vector2(0.55f, 0), new Vector2(1, 0.5f), new Vector2(4, 0), new Vector2(-10, 0));

            var fields = CreatePanel("Fields", hud.transform, new Vector2(0, 0.28f), new Vector2(1, 0.58f), new Vector2(10, 0), new Vector2(-10, 0));
            fields.GetComponent<Image>().color = GameUiStyle.PanelLight;
            b.fieldLabels = new Text[5];
            b.fieldValues = new Text[5];
            b.fieldRows = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                float yMax = 1f - i * 0.2f;
                float yMin = yMax - 0.2f;
                var row = CreatePanel("F" + i, fields.transform, new Vector2(0, yMin), new Vector2(1, yMax), new Vector2(4, 1), new Vector2(-4, -1));
                row.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);
                b.fieldRows[i] = row.GetComponent<Image>();
                b.fieldLabels[i] = CreateText("L", row.transform, "", 13, TextAnchor.MiddleLeft);
                Stretch(b.fieldLabels[i].rectTransform, new Vector2(0, 0), new Vector2(0.35f, 1), new Vector2(8, 0), Vector2.zero);
                b.fieldLabels[i].color = GameUiStyle.Dim;
                b.fieldValues[i] = CreateText("V", row.transform, "", 13, TextAnchor.MiddleLeft);
                Stretch(b.fieldValues[i].rectTransform, new Vector2(0.35f, 0), Vector2.one, new Vector2(4, 0), new Vector2(-8, 0));
                b.fieldValues[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            b.familiarBadge = CreateText("Familiar", hud.transform, "", 14, TextAnchor.MiddleCenter);
            Stretch(b.familiarBadge.rectTransform, new Vector2(0.55f, 0.24f), new Vector2(0.98f, 0.28f), Vector2.zero, Vector2.zero);
            b.familiarBadge.color = GameUiStyle.Accent;
            b.familiarBadge.gameObject.SetActive(false);

            b.resultBanner = CreateText("Banner", hud.transform, "", 20, TextAnchor.MiddleCenter);
            Stretch(b.resultBanner.rectTransform, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.28f), Vector2.zero, Vector2.zero);
            b.resultBanner.fontStyle = FontStyle.Bold;
            b.resultBanner.gameObject.SetActive(false);

            b.fundsHint = CreateText("Funds", hud.transform, "", 14, TextAnchor.MiddleCenter);
            Stretch(b.fundsHint.rectTransform, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.22f), Vector2.zero, Vector2.zero);
            b.fundsHint.color = GameUiStyle.Bad;
            b.fundsHint.gameObject.SetActive(false);

            b.btnStartLot = CreateButton("StartLot", hud.transform, out b.startLotLabel);
            Place(b.btnStartLot, 0.06f, 0.1f, 0.94f, 0.17f);
            b.btnBuy = CreateButton("Buy", hud.transform, out b.buyLabel);
            Place(b.btnBuy, 0.06f, 0.02f, 0.94f, 0.09f);
            b.btnBuy.GetComponent<Image>().color = GameUiStyle.Good;
            b.btnSkip = CreateButton("Skip", hud.transform, out b.skipLabel);
            Place(b.btnSkip, 0.06f, 0.1f, 0.48f, 0.17f);
            b.btnFinishDay = CreateButton("Finish", hud.transform, out b.finishLabel);
            Place(b.btnFinishDay, 0.52f, 0.1f, 0.94f, 0.17f);
            return root;
        }

        static GameObject BuildCollectorPopup(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("CollectorPopup", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0.65f);
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -240), new Vector2(380, 240));
            b.popupPortrait = CreatePanel("Portrait", card.transform, new Vector2(0, 0.55f), new Vector2(0.32f, 1), new Vector2(16, -16), new Vector2(-8, -16)).GetComponent<Image>();
            b.popupPortrait.color = GameUiStyle.PanelLight;
            b.popupPortrait.preserveAspect = true;
            b.popupName = CreateText("Name", card.transform, "", 26, TextAnchor.UpperLeft);
            Stretch(b.popupName.rectTransform, new Vector2(0.32f, 0.82f), Vector2.one, new Vector2(8, -12), new Vector2(-16, -8));
            b.popupName.color = GameUiStyle.Accent;
            b.popupTagline = CreateText("Tag", card.transform, "", 13, TextAnchor.UpperLeft);
            Stretch(b.popupTagline.rectTransform, new Vector2(0.32f, 0.68f), new Vector2(1, 0.82f), new Vector2(8, 0), new Vector2(-16, 0));
            b.popupTagline.color = GameUiStyle.Dim;
            b.popupTagline.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.popupSpeech = CreateText("Speech", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.popupSpeech.rectTransform, new Vector2(0, 0.48f), new Vector2(1, 0.55f), new Vector2(16, 0), new Vector2(-16, 0));
            b.popupSpeech.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.popupTags = CreateText("Tags", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.popupTags.rectTransform, new Vector2(0, 0.28f), new Vector2(1, 0.48f), new Vector2(16, 0), new Vector2(-16, 0));
            b.popupTags.color = GameUiStyle.Accent;
            b.popupTags.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.popupWarning = CreateText("Warn", card.transform, "", 13, TextAnchor.UpperLeft);
            Stretch(b.popupWarning.rectTransform, new Vector2(0, 0.16f), new Vector2(1, 0.28f), new Vector2(16, 0), new Vector2(-16, 0));
            b.popupWarning.color = GameUiStyle.Bad;
            b.popupWarning.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.btnPopupStart = CreateButton("Start", card.transform, out b.popupStartLabel);
            Place(b.btnPopupStart, 0.2f, 0.04f, 0.8f, 0.15f);
            b.btnPopupStart.GetComponent<Image>().color = GameUiStyle.Good;
            return root;
        }

        static GameObject BuildPurchaseCard(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("PurchaseCard", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340, -240), new Vector2(340, 240));
            b.pcTitle = CreateText("Title", card.transform, "", 26, TextAnchor.UpperCenter);
            Stretch(b.pcTitle.rectTransform, new Vector2(0, 0.82f), Vector2.one, new Vector2(16, -12), new Vector2(-16, -8));
            b.pcTitle.color = GameUiStyle.Accent;
            b.pcArtist = CreateText("Artist", card.transform, "", 18, TextAnchor.UpperCenter);
            Stretch(b.pcArtist.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.82f), new Vector2(16, 0), new Vector2(-16, 0));
            b.pcMeta = CreateText("Meta", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.pcMeta.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 0.72f), new Vector2(28, 0), new Vector2(-28, 0));
            b.pcMeta.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.pcFact = CreateText("Fact", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.pcFact.rectTransform, new Vector2(0, 0.18f), new Vector2(1, 0.4f), new Vector2(28, 0), new Vector2(-28, 0));
            b.pcFact.color = GameUiStyle.Dim;
            b.pcFact.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.btnPcContinue = CreateButton("Cont", card.transform, out b.pcContinueLabel);
            Place(b.btnPcContinue, 0.25f, 0.04f, 0.75f, 0.15f);
            b.btnPcContinue.GetComponent<Image>().color = GameUiStyle.Good;
            return root;
        }

        static GameObject BuildTutorial(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Tutorial", parent, new Vector2(0.15f, 0.02f), new Vector2(0.5f, 0.18f), Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            b.tutorialText = CreateText("T", root.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(b.tutorialText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));
            b.tutorialText.horizontalOverflow = HorizontalWrapMode.Wrap;
            return root;
        }

        static GameObject BuildReport(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Report", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = GameUiStyle.Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.92f), Vector2.zero, new Vector2(0, -48));
            b.reportTitle = CreateText("Title", card.transform, "", 28, TextAnchor.UpperLeft);
            Stretch(b.reportTitle.rectTransform, new Vector2(0, 0.9f), Vector2.one, new Vector2(20, -10), new Vector2(-20, -6));
            b.reportTitle.color = GameUiStyle.Accent;
            b.reportBody = CreateText("Body", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.reportBody.rectTransform, new Vector2(0, 0.42f), new Vector2(0.55f, 0.9f), new Vector2(20, 0), new Vector2(-10, 0));
            b.reportBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.reportBody.verticalOverflow = VerticalWrapMode.Overflow;

            var boostPanel = CreatePanel("Boosters", card.transform, new Vector2(0.55f, 0.18f), new Vector2(1, 0.9f), new Vector2(8, 0), new Vector2(-16, 0));
            boostPanel.GetComponent<Image>().color = GameUiStyle.PanelLight;
            b.boosterHeading = CreateText("BoosterHeading", boostPanel.transform, "", 16, TextAnchor.UpperLeft);
            Stretch(b.boosterHeading.rectTransform, new Vector2(0, 0.9f), Vector2.one, new Vector2(10, -6), new Vector2(-10, -4));
            b.boosterHeading.color = GameUiStyle.Accent;
            b.boosterList = CreateScrollContent(boostPanel.transform, "BoosterScroll", new Vector2(0, 0), new Vector2(1, 0.9f));

            b.btnReportContinue = CreateButton("Cont", card.transform, out b.reportContinueLabel);
            Place(b.btnReportContinue, 0.35f, 0.03f, 0.65f, 0.14f);
            b.btnReportContinue.GetComponent<Image>().color = GameUiStyle.Good;
            return root;
        }

        static GameObject BuildEnd(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_End", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = GameUiStyle.Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -130), new Vector2(300, 130));
            b.endTitle = CreateText("Title", card.transform, "", 34, TextAnchor.MiddleCenter);
            Stretch(b.endTitle.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 0.9f), new Vector2(12, 0), new Vector2(-12, 0));
            b.endTitle.color = GameUiStyle.Accent;
            b.btnRestart = CreateButton("Restart", card.transform, out b.restartLabel);
            Place(b.btnRestart, 0.2f, 0.12f, 0.8f, 0.35f);
            b.btnRestart.GetComponent<Image>().color = GameUiStyle.Good;
            return root;
        }

        // в”Ђв”Ђв”Ђ Helpers (mirror GameUiShell factories) в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        static Transform CreateScrollContent(Transform parent, string name, Vector2 aMin, Vector2 aMax)
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
            crt.sizeDelta = Vector2.zero;
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
            if (parent != null) go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), aMin, aMax, offMin, offMax);
            go.GetComponent<Image>().color = GameUiStyle.Panel;
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
            text.color = GameUiStyle.TextColor;
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
            img.color = GameUiStyle.Accent;
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
