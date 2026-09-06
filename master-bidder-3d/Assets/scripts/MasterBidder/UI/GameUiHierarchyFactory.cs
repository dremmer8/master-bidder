using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Builds the default Master Bidder uGUI hierarchy for prefab generation
    /// (and as a runtime fallback when prefabs are missing).
    /// Defaults for fonts, sizes, spacing, and anchors are baked here once;
    /// after Generate UI Prefabs, edit those values on the assets — GameUiShell
    /// will not re-apply layout or typography at runtime.
    /// </summary>
    public static class GameUiHierarchyFactory
    {
        public static GameObject BuildCollectorCard()
        {
            var card = CreatePanel("CollectorCard", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = card.AddComponent<LayoutElement>();
            // ~3 cards visible in the brief sidebar scroll.
            le.minHeight = 118;
            le.preferredHeight = 118;
            GameUiStyle.ApplyCard(card.GetComponent<Image>());
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();

            // Portrait spans full card height on the left.
            var portrait = CreatePanel("P", card.transform, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(6, 6), new Vector2(-2, -6));
            var pImg = portrait.GetComponent<Image>();
            pImg.color = Color.white;
            pImg.preserveAspect = true;

            var t = CreateText("T", card.transform, "", 15, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(0.3f, 0), Vector2.one, new Vector2(8, 8), new Vector2(-10, -8));
            t.enableWordWrapping = true;
            t.lineSpacing = GameUiStyle.ToTmpLineSpacing(1.05f);

            var view = card.AddComponent<CollectorCardView>();
            view.background = card.GetComponent<Image>();
            view.portrait = pImg;
            view.portraitRoot = portrait;
            view.label = t;
            view.button = btn;
            GameUiSampleContent.ApplyCollectorCard(view);
            return card;
        }

        public static GameObject BuildUpgradeRow()
        {
            var row = CreatePanel("UpgradeRow", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 64;
            le.preferredHeight = 64;
            GameUiStyle.ApplyCard(row.GetComponent<Image>());

            var icon = CreateIcon("Icon", row.transform);
            Stretch(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -24), new Vector2(56, 24));

            var t = CreateText("T", row.transform, "", 13, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(0, 0), new Vector2(0.72f, 1), new Vector2(64, 4), new Vector2(-4, -4));
            t.enableWordWrapping = true;
            t.lineSpacing = GameUiStyle.ToTmpLineSpacing(1.05f);

            var buy = CreatePrimaryButton("B", row.transform, out var bl);
            Place(buy, 0.74f, 0.18f, 0.97f, 0.82f);
            bl.fontSize = 14;

            var view = row.AddComponent<UpgradeRowView>();
            view.background = row.GetComponent<Image>();
            view.icon = icon;
            view.label = t;
            view.buyButton = buy;
            view.buyLabel = bl;
            GameUiSampleContent.ApplyUpgradeRow(view);
            return row;
        }

        public static GameObject BuildBoosterRow()
        {
            var row = CreatePanel("BoosterRow", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 76;
            le.preferredHeight = 76;
            GameUiStyle.ApplyCard(row.GetComponent<Image>());

            var icon = CreateIcon("Icon", row.transform);
            Stretch(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -28), new Vector2(64, 28));

            var t = CreateText("T", row.transform, "", 11, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, new Vector2(0, 0), new Vector2(0.7f, 1), new Vector2(72, 2), new Vector2(-4, -2));
            t.enableWordWrapping = true;

            var buy = CreatePrimaryButton("Buy", row.transform, out var bl);
            Place(buy, 0.72f, 0.2f, 0.96f, 0.8f);

            var view = row.AddComponent<BoosterRowView>();
            view.background = row.GetComponent<Image>();
            view.icon = icon;
            view.label = t;
            view.buyButton = buy;
            view.buyLabel = bl;
            GameUiSampleContent.ApplyBoosterRow(view);
            return row;
        }

        static Image CreateIcon(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // в”Ђв”Ђв”Ђ Main GameUI prefab в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        public static GameObject BuildGameUi()
        {
            GameUiSprites.Warmup();

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
            GameUiSampleContent.Apply(b);
            return canvasGo;
        }

        static void BuildChrome(Transform parent, GameUiBindings b)
        {
            var bar = CreatePanel("Chrome", parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), Vector2.zero);
            GameUiStyle.ApplySliced(bar.GetComponent<Image>(), GameUiSprites.Banner, GameUiStyle.SpriteReady);
            b.chromeTitle = CreateDisplayText("Title", bar.transform, "", 16, TextAnchor.MiddleLeft);
            Stretch(b.chromeTitle.rectTransform, new Vector2(0, 0), new Vector2(0.28f, 1), new Vector2(28, 0), new Vector2(-8, 0));

            BuildEffectsHud(bar.transform, parent, b);

            var langGo = new GameObject("Lang", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            langGo.transform.SetParent(bar.transform, false);
            Stretch(langGo.GetComponent<RectTransform>(), new Vector2(0.78f, 0.12f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);
            GameUiStyle.ApplySecondaryButton(langGo.GetComponent<Image>());
            b.langDropdown = langGo.GetComponent<TMP_Dropdown>();
            b.langDropdown.targetGraphic = langGo.GetComponent<Image>();
            var caption = CreateText("Caption", langGo.transform, "RU", 14, TextAnchor.MiddleCenter);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            caption.color = GameUiStyle.TextColor;
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
            b.langDropdown.options.Add(new TMP_Dropdown.OptionData("Русский"));
            b.langDropdown.options.Add(new TMP_Dropdown.OptionData("English"));
        }

        static GameObject BuildIntro(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Intro", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = GameUiStyle.ScreenBg;
            // Compact plate: crest via ApplyFramedPanel; bands sized to content (no spare vertical air).
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-310, -200), new Vector2(310, 200));
            GameUiStyle.ApplyFramedPanel(card.GetComponent<Image>());
            b.introTitle = CreateDisplayText("Title", card.transform, "", 34, TextAnchor.MiddleCenter);
            Stretch(b.introTitle.rectTransform, new Vector2(0, 0.84f), Vector2.one, new Vector2(24, -18), new Vector2(-24, -8));
            b.introTitle.color = GameUiStyle.Accent;
            b.introSubtitle = CreateDisplayText("Sub", card.transform, "", 20, TextAnchor.MiddleCenter);
            Stretch(b.introSubtitle.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.84f), new Vector2(24, 0), new Vector2(-24, 0));
            b.introSubtitle.color = GameUiStyle.TextColor;
            b.introSubtitle.enableWordWrapping = true;
            b.introLede = CreateText("Lede", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.introLede.rectTransform, new Vector2(0, 0.54f), new Vector2(1, 0.70f), new Vector2(28, 0), new Vector2(-28, 0));
            b.introLede.enableWordWrapping = true;
            b.introLede.lineSpacing = GameUiStyle.ToTmpLineSpacing(1.1f);
            b.introRules = CreateText("Rules", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.introRules.rectTransform, new Vector2(0, 0.18f), new Vector2(1, 0.52f), new Vector2(28, 0), new Vector2(-28, 0));
            b.introRules.color = GameUiStyle.Dim;
            b.introRules.enableWordWrapping = true;
            b.introRules.lineSpacing = GameUiStyle.ToTmpLineSpacing(1.15f);
            b.btnContinue = CreateSecondaryButton("Continue", card.transform, out b.continueLabel);
            Place(b.btnContinue, 0.06f, 0.04f, 0.48f, 0.15f);
            b.btnStart = CreatePrimaryButton("Start", card.transform, out b.startLabel);
            Place(b.btnStart, 0.52f, 0.04f, 0.94f, 0.15f);
            return root;
        }

        static GameObject BuildBrief(Transform parent, GameUiBindings b)
        {
            // Sit below the shared Chrome banner (same top bar as auction).
            var root = CreatePanel("Screen_Brief", parent, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -52));
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = GameUiStyle.ScreenBg;

            var chrome = parent.Find("Chrome") ?? parent;
            var status = CreateFixedBar("ChromeStatus", chrome, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(360f, 38f));
            GameUiStyle.ApplySliced(status.GetComponent<Image>(), GameUiSprites.BarCurrency, GameUiStyle.SpriteReady);
            b.briefDay = CreateText("Status", status.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(b.briefDay.rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 0), new Vector2(-12, 0));
            b.briefDay.color = GameUiStyle.OnDark;
            MakeUiBold(b.briefDay);
            b.briefCapital = b.briefDay;
            status.SetActive(true);
            var lang = chrome.Find("Lang");
            if (lang != null) lang.gameObject.SetActive(false);

            // Compact strip: portrait + name + large tags (sketch-sized).
            var active = CreatePanel("ActiveClient", root.transform, new Vector2(0.015f, 0.86f), new Vector2(0.42f, 0.98f), new Vector2(6, -4), new Vector2(-6, -4));
            GameUiStyle.ApplyCard(active.GetComponent<Image>());
            b.briefActiveClient = active;
            b.briefActivePortrait = CreatePanel("Portrait", active.transform, new Vector2(0, 0.08f), new Vector2(0.16f, 0.92f), new Vector2(8, 0), new Vector2(-2, 0)).GetComponent<Image>();
            b.briefActivePortrait.sprite = null;
            b.briefActivePortrait.color = GameUiStyle.PanelLight;
            b.briefActivePortrait.preserveAspect = true;
            b.briefActiveName = CreateDisplayText("Name", active.transform, "", 14, TextAnchor.MiddleLeft);
            Stretch(b.briefActiveName.rectTransform, new Vector2(0.17f, 0.52f), new Vector2(0.48f, 0.92f), new Vector2(4, 0), new Vector2(-4, -2));
            b.briefActiveName.color = GameUiStyle.Accent;
            b.briefActiveTags = CreateText("Tags", active.transform, "", 20, TextAnchor.MiddleLeft);
            Stretch(b.briefActiveTags.rectTransform, new Vector2(0.48f, 0.08f), new Vector2(1, 0.92f), new Vector2(4, 2), new Vector2(-10, -2));
            b.briefActiveTags.color = GameUiStyle.TextColor;
            MakeUiBold(b.briefActiveTags);
            b.briefActiveTags.enableWordWrapping = true;
            b.briefActiveTags.overflowMode = TextOverflowModes.Overflow;
            b.briefActiveTags.lineSpacing = 0f;
            b.briefOrderPreview = b.briefActiveTags;

            // Right sidebar: heading → list → actions (icons live in Chrome, like auction).
            var sidebar = CreatePanel("Sidebar", root.transform, new Vector2(0.66f, 0.02f), new Vector2(0.985f, 0.98f), new Vector2(6, 8), new Vector2(-12, -8));
            GameUiStyle.ApplyFramedPanel(sidebar.GetComponent<Image>());

            b.briefClientHeading = CreateDisplayText("OrdersH", sidebar.transform, "", 26, TextAnchor.MiddleLeft);
            Stretch(b.briefClientHeading.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
            b.briefClientHeading.color = GameUiStyle.Accent;

            b.briefWorkshopHeading = CreateDisplayText("UpgradesH", sidebar.transform, "", 26, TextAnchor.MiddleLeft);
            Stretch(b.briefWorkshopHeading.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
            b.briefWorkshopHeading.color = GameUiStyle.Accent;
            b.briefWorkshopHeading.gameObject.SetActive(false);

            b.collectorList = CreateScrollContent(sidebar.transform, "CollectorScroll", new Vector2(0, 0.14f), new Vector2(1, 0.88f), 6);
            b.upgradeList = CreateScrollContent(sidebar.transform, "UpgradeScroll", new Vector2(0, 0.14f), new Vector2(1, 0.88f), 4);
            var upgradeScroll = sidebar.transform.Find("UpgradeScroll");
            if (upgradeScroll != null) upgradeScroll.gameObject.SetActive(false);

            b.btnBriefPanelToggle = CreateSecondaryButton("TogglePanel", sidebar.transform, out b.briefPanelToggleLabel);
            Place(b.btnBriefPanelToggle, 0.04f, 0.02f, 0.48f, 0.12f);
            b.btnEnterHall = CreatePrimaryButton("Enter", sidebar.transform, out b.enterLabel);
            Place(b.btnEnterHall, 0.52f, 0.02f, 0.96f, 0.12f);

            b.btnReset = CreateSecondaryButton("Reset", root.transform, out b.resetLabel);
            Place(b.btnReset, 0.02f, 0.02f, 0.14f, 0.07f);
            b.resetLabel.fontSize = 12;
            var resetCg = b.btnReset.gameObject.GetComponent<CanvasGroup>();
            if (resetCg == null) resetCg = b.btnReset.gameObject.AddComponent<CanvasGroup>();
            resetCg.alpha = 0.5f;
            return root;
        }

        static GameObject BuildAuction(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Auction", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0f);

            // Placeholder for future 3D rival characters — keep transform hook, hide placeholder UI.
            var audience = CreatePanel("Audience", root.transform, new Vector2(0.02f, 0.02f), new Vector2(0.5f, 0.14f), Vector2.zero, Vector2.zero);
            audience.GetComponent<Image>().sprite = null;
            audience.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            audience.GetComponent<Image>().raycastTarget = false;
            b.audienceRow = audience.transform;
            b.rivalHeads = System.Array.Empty<Image>();
            audience.SetActive(false);

            // Compact client strip under Chrome (portrait + name + large tags).
            var active = CreatePanel("ActiveClient", root.transform, new Vector2(0.015f, 1f), new Vector2(0.42f, 1f), new Vector2(6, -128), new Vector2(-6, -56));
            GameUiStyle.ApplyCard(active.GetComponent<Image>());
            b.auctionActiveClient = active;
            b.auctionActivePortrait = CreatePanel("Portrait", active.transform, new Vector2(0, 0.08f), new Vector2(0.16f, 0.92f), new Vector2(8, 0), new Vector2(-2, 0)).GetComponent<Image>();
            b.auctionActivePortrait.sprite = null;
            b.auctionActivePortrait.color = GameUiStyle.PanelLight;
            b.auctionActivePortrait.preserveAspect = true;
            b.auctionActiveName = CreateDisplayText("Name", active.transform, "", 14, TextAnchor.MiddleLeft);
            Stretch(b.auctionActiveName.rectTransform, new Vector2(0.17f, 0.52f), new Vector2(0.48f, 0.92f), new Vector2(4, 0), new Vector2(-4, -2));
            b.auctionActiveName.color = GameUiStyle.Accent;
            b.auctionActiveTags = CreateText("Tags", active.transform, "", 20, TextAnchor.MiddleLeft);
            Stretch(b.auctionActiveTags.rectTransform, new Vector2(0.48f, 0.08f), new Vector2(1, 0.92f), new Vector2(4, 2), new Vector2(-10, -2));
            b.auctionActiveTags.color = GameUiStyle.TextColor;
            MakeUiBold(b.auctionActiveTags);
            b.auctionActiveTags.enableWordWrapping = true;

            var hud = CreatePanel("HudRight", root.transform, new Vector2(0.72f, 0.02f), new Vector2(0.985f, 1f), new Vector2(6, 8), new Vector2(-12, -56));
            GameUiStyle.ApplyFramedPanel(hud.GetComponent<Image>());

            b.aucHud = CreateText("AucHud", hud.transform, "", 13, TextAnchor.MiddleLeft);
            Stretch(b.aucHud.rectTransform, new Vector2(0, 0.92f), Vector2.one, new Vector2(16, -8), new Vector2(-16, -4));
            b.aucHud.color = GameUiStyle.TextColor;
            MakeUiBold(b.aucHud);
            b.aucHud.enableWordWrapping = true;

            // Legacy binding kept hidden — client lives in ActiveClient strip.
            b.orderCard = CreateText("Order", hud.transform, "", 14, TextAnchor.MiddleLeft);
            b.orderCard.gameObject.SetActive(false);

            var econ = CreatePanel("Econ", hud.transform, new Vector2(0, 0.72f), new Vector2(1, 0.92f), new Vector2(14, 2), new Vector2(-14, -4));
            GameUiStyle.ApplyCard(econ.GetComponent<Image>());
            b.livePrice = CreateText("Price", econ.transform, "", 26, TextAnchor.MiddleCenter);
            Stretch(b.livePrice.rectTransform, new Vector2(0, 0.42f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -4));
            MakeUiBold(b.livePrice);
            b.livePrice.color = GameUiStyle.TextColor;
            b.liveBudget = CreateText("Budget", econ.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(b.liveBudget.rectTransform, new Vector2(0, 0), new Vector2(1, 0.42f), new Vector2(10, 4), new Vector2(-10, -4));
            MakeUiBold(b.liveBudget);
            b.liveBudget.color = GameUiStyle.TextColor;
            b.liveSpeed = CreateText("Speed", econ.transform, "", 13, TextAnchor.MiddleRight);
            b.liveSpeed.gameObject.SetActive(false);

            var fields = CreatePanel("Fields", hud.transform, new Vector2(0, 0.2f), new Vector2(1, 0.72f), new Vector2(14, 4), new Vector2(-14, -4));
            GameUiStyle.ApplyCard(fields.GetComponent<Image>());
            b.fieldLabels = new TextMeshProUGUI[5];
            b.fieldValues = new TextMeshProUGUI[5];
            b.fieldRows = new Image[5];
            // Fact (index 3) gets a taller band so long copy stays inside the row.
            float[] rowTops = { 1f, 0.86f, 0.72f, 0.58f, 0.18f, 0f };
            for (int i = 0; i < 5; i++)
            {
                float yMax = rowTops[i];
                float yMin = rowTops[i + 1];
                var row = CreatePanel("F" + i, fields.transform, new Vector2(0, yMin), new Vector2(1, yMax), new Vector2(3, 1), new Vector2(-3, -1));
                row.GetComponent<Image>().sprite = null;
                row.GetComponent<Image>().color = i % 2 == 0
                    ? new Color(0.2f, 0.16f, 0.12f, 0.1f)
                    : new Color(0.2f, 0.16f, 0.12f, 0.04f);
                row.AddComponent<RectMask2D>();
                b.fieldRows[i] = row.GetComponent<Image>();
                b.fieldLabels[i] = CreateText("L", row.transform, "", 12, TextAnchor.UpperLeft);
                Stretch(b.fieldLabels[i].rectTransform, new Vector2(0, 0), new Vector2(0.38f, 1), new Vector2(10, 4), new Vector2(0, -4));
                b.fieldLabels[i].color = GameUiStyle.Dim;
                b.fieldValues[i] = CreateText("V", row.transform, "", 13, TextAnchor.UpperLeft);
                Stretch(b.fieldValues[i].rectTransform, new Vector2(0.38f, 0), Vector2.one, new Vector2(4, 4), new Vector2(-10, -4));
                b.fieldValues[i].enableWordWrapping = true;
                b.fieldValues[i].overflowMode = TextOverflowModes.Truncate;
                b.fieldValues[i].color = GameUiStyle.TextColor;
            }

            b.familiarBadge = CreateText("Familiar", hud.transform, "", 12, TextAnchor.MiddleCenter);
            Stretch(b.familiarBadge.rectTransform, new Vector2(0.55f, 0.24f), new Vector2(0.96f, 0.28f), Vector2.zero, Vector2.zero);
            b.familiarBadge.color = GameUiStyle.Accent;
            b.familiarBadge.gameObject.SetActive(false);

            var bannerBg = CreatePanel("BannerBg", hud.transform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.24f), Vector2.zero, Vector2.zero);
            GameUiStyle.ApplySliced(bannerBg.GetComponent<Image>(), GameUiSprites.ToastInfo, GameUiStyle.SpriteReady);
            bannerBg.SetActive(false);
            b.resultBanner = CreateText("Banner", bannerBg.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(b.resultBanner.rectTransform, Vector2.zero, Vector2.one, new Vector2(36, 4), new Vector2(-32, -4));
            MakeUiBold(b.resultBanner);
            b.resultBanner.color = GameUiStyle.TextColor;

            var fundsBg = CreatePanel("FundsBg", hud.transform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.24f), Vector2.zero, Vector2.zero);
            GameUiStyle.ApplySliced(fundsBg.GetComponent<Image>(), GameUiSprites.ToastError, GameUiStyle.SpriteReady);
            fundsBg.SetActive(false);
            b.fundsHint = CreateText("Funds", fundsBg.transform, "", 13, TextAnchor.MiddleCenter);
            Stretch(b.fundsHint.rectTransform, Vector2.zero, Vector2.one, new Vector2(36, 2), new Vector2(-32, -2));
            b.fundsHint.color = GameUiStyle.Bad;

            b.btnStartLot = CreateSecondaryButton("StartLot", hud.transform, out b.startLotLabel);
            Place(b.btnStartLot, 0.05f, 0.02f, 0.95f, 0.18f);
            b.btnBuy = CreatePrimaryButton("Buy", hud.transform, out b.buyLabel);
            Place(b.btnBuy, 0.05f, 0.02f, 0.58f, 0.18f);
            b.btnSkip = CreateSecondaryButton("Skip", hud.transform, out b.skipLabel);
            Place(b.btnSkip, 0.6f, 0.02f, 0.77f, 0.18f);
            b.btnFinishDay = CreateDangerButton("Finish", hud.transform, out b.finishLabel);
            Place(b.btnFinishDay, 0.79f, 0.02f, 0.95f, 0.18f);
            return root;
        }

        static GameObject BuildCollectorPopup(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("CollectorPopup", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = GameUiStyle.Overlay;
            // Dialogue card: portrait+identity header, then full-width speech below (never under the face).
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360, -210), new Vector2(360, 210));
            GameUiStyle.ApplyFramedPanel(card.GetComponent<Image>());

            b.popupPortrait = CreatePanel("Portrait", card.transform, new Vector2(0, 0.7f), new Vector2(0.22f, 0.96f), new Vector2(24, -16), new Vector2(-8, -14)).GetComponent<Image>();
            b.popupPortrait.sprite = null;
            b.popupPortrait.color = GameUiStyle.PanelLight;
            b.popupPortrait.preserveAspect = true;

            b.popupName = CreateDisplayText("Name", card.transform, "", 26, TextAnchor.LowerLeft);
            Stretch(b.popupName.rectTransform, new Vector2(0.24f, 0.84f), new Vector2(1, 0.96f), new Vector2(8, 0), new Vector2(-24, -14));
            b.popupName.color = GameUiStyle.Accent;

            b.popupTagline = CreateText("Tag", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.popupTagline.rectTransform, new Vector2(0.24f, 0.7f), new Vector2(1, 0.84f), new Vector2(8, 0), new Vector2(-24, 0));
            b.popupTagline.color = GameUiStyle.TextColor;
            b.popupTagline.enableWordWrapping = true;
            b.popupTagline.lineSpacing = GameUiStyle.ToTmpLineSpacing(1.1f);

            var speechPlate = CreatePanel("SpeechPlate", card.transform, new Vector2(0, 0.42f), new Vector2(1, 0.68f), new Vector2(22, 4), new Vector2(-22, -4));
            GameUiStyle.ApplyCard(speechPlate.GetComponent<Image>());
            b.popupSpeech = CreateText("Speech", speechPlate.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.popupSpeech.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10));
            b.popupSpeech.color = GameUiStyle.TextColor;
            GameUiStyle.ApplyDisplayItalic(b.popupSpeech);
            b.popupSpeech.enableWordWrapping = true;
            b.popupSpeech.lineSpacing = GameUiStyle.ToTmpLineSpacing(1.15f);

            var tagsPlate = CreatePanel("TagsPlate", card.transform, new Vector2(0, 0.28f), new Vector2(1, 0.42f), new Vector2(22, 2), new Vector2(-22, -2));
            GameUiStyle.ApplyCard(tagsPlate.GetComponent<Image>());
            b.popupTags = CreateDisplayText("Tags", tagsPlate.transform, "", 17, TextAnchor.MiddleLeft);
            Stretch(b.popupTags.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));
            b.popupTags.color = GameUiStyle.Accent;
            b.popupTags.enableWordWrapping = true;

            b.popupWarning = CreateText("Warn", card.transform, "", 13, TextAnchor.MiddleLeft);
            Stretch(b.popupWarning.rectTransform, new Vector2(0, 0.16f), new Vector2(1, 0.28f), new Vector2(28, 0), new Vector2(-28, 0));
            b.popupWarning.color = GameUiStyle.Bad;
            b.popupWarning.enableWordWrapping = true;

            b.btnPopupStart = CreatePrimaryButton("Start", card.transform, out b.popupStartLabel);
            Place(b.btnPopupStart, 0.2f, 0.04f, 0.8f, 0.14f);
            return root;
        }

        static GameObject BuildPurchaseCard(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("PurchaseCard", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = GameUiStyle.Overlay;
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340, -240), new Vector2(340, 240));
            GameUiStyle.ApplyPanel(card.GetComponent<Image>());
            b.pcTitle = CreateDisplayText("Title", card.transform, "", 26, TextAnchor.UpperCenter);
            Stretch(b.pcTitle.rectTransform, new Vector2(0, 0.82f), Vector2.one, new Vector2(22, -16), new Vector2(-22, -12));
            b.pcTitle.color = GameUiStyle.Accent;
            b.pcArtist = CreateDisplayText("Artist", card.transform, "", 18, TextAnchor.UpperCenter, bold: false);
            Stretch(b.pcArtist.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.82f), new Vector2(22, 0), new Vector2(-22, 0));
            b.pcMeta = CreateText("Meta", card.transform, "", 15, TextAnchor.UpperLeft);
            Stretch(b.pcMeta.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 0.72f), new Vector2(32, 0), new Vector2(-32, 0));
            b.pcMeta.enableWordWrapping = true;
            b.pcFact = CreateText("Fact", card.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.pcFact.rectTransform, new Vector2(0, 0.18f), new Vector2(1, 0.4f), new Vector2(32, 0), new Vector2(-32, 0));
            b.pcFact.color = GameUiStyle.Dim;
            b.pcFact.enableWordWrapping = true;
            b.btnPcContinue = CreatePrimaryButton("Cont", card.transform, out b.pcContinueLabel);
            Place(b.btnPcContinue, 0.25f, 0.04f, 0.75f, 0.15f);
            return root;
        }

        static GameObject BuildTutorial(Transform parent, GameUiBindings b)
        {
            // Compact toast above the action row so it never covers Skip/Buy.
            var root = new GameObject("Tutorial", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.2f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 88f);
            var img = root.GetComponent<Image>();
            GameUiStyle.ApplySliced(img, GameUiSprites.ToastInfo, GameUiStyle.SpriteReady);
            img.raycastTarget = false;
            b.tutorialText = CreateText("T", root.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(b.tutorialText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 12), new Vector2(-28, -12));
            b.tutorialText.enableWordWrapping = true;
            b.tutorialText.overflowMode = TextOverflowModes.Overflow;
            b.tutorialText.color = GameUiStyle.TextColor;
            MakeUiBold(b.tutorialText);
            b.tutorialText.raycastTarget = false;
            return root;
        }

        public static GameObject BuildPurchaseTag()
        {
            var card = CreatePanel("PurchaseTag", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = card.AddComponent<LayoutElement>();
            le.minHeight = 110;
            le.preferredHeight = 110;
            GameUiStyle.ApplyCard(card.GetComponent<Image>());

            var title = CreateDisplayText("Title", card.transform, "", 16, TextAnchor.UpperLeft);
            Stretch(title.rectTransform, new Vector2(0, 0.55f), new Vector2(0.68f, 1), new Vector2(14, -10), new Vector2(-8, -6));
            title.enableWordWrapping = true;

            var meta = CreateText("Meta", card.transform, "", 13, TextAnchor.UpperLeft);
            Stretch(meta.rectTransform, new Vector2(0, 0.08f), new Vector2(0.68f, 0.55f), new Vector2(14, 6), new Vector2(-8, 0));
            meta.color = GameUiStyle.Dim;
            meta.enableWordWrapping = true;

            var stamp = CreateDisplayText("Stamp", card.transform, "", 15, TextAnchor.MiddleCenter);
            Stretch(stamp.rectTransform, new Vector2(0.68f, 0.12f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);
            stamp.enableWordWrapping = true;

            var view = card.AddComponent<PurchaseTagView>();
            view.background = card.GetComponent<Image>();
            view.title = title;
            view.meta = meta;
            view.stamp = stamp;
            GameUiSampleContent.ApplyPurchaseTag(view);
            return card;
        }

        static GameObject BuildReport(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_Report", parent, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -52));
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            root.GetComponent<Image>().raycastTarget = false;

            // Client stamp — bottom-left of the room scene.
            var stamp = CreatePanel("ClientStamp", root.transform, new Vector2(0.03f, 0.04f), new Vector2(0.28f, 0.28f), Vector2.zero, Vector2.zero);
            GameUiStyle.ApplyFramedPanel(stamp.GetComponent<Image>());
            b.reportStamp = stamp;
            b.reportStampLabel = CreateDisplayText("StampLabel", stamp.transform, "", 22, TextAnchor.MiddleCenter);
            Stretch(b.reportStampLabel.rectTransform, new Vector2(0, 0.38f), Vector2.one, new Vector2(12, 0), new Vector2(-12, -10));
            b.reportStampLabel.color = GameUiStyle.Accent;
            b.reportStampLabel.enableWordWrapping = true;
            b.reportStampDetail = CreateText("StampDetail", stamp.transform, "", 14, TextAnchor.UpperCenter);
            Stretch(b.reportStampDetail.rectTransform, new Vector2(0, 0.08f), new Vector2(1, 0.4f), new Vector2(12, 4), new Vector2(-12, 0));
            b.reportStampDetail.color = GameUiStyle.TextColor;
            b.reportStampDetail.enableWordWrapping = true;

            var sidebar = CreatePanel("Sidebar", root.transform, new Vector2(0.66f, 0.02f), new Vector2(0.985f, 0.98f), new Vector2(6, 8), new Vector2(-12, -8));
            GameUiStyle.ApplyFramedPanel(sidebar.GetComponent<Image>());

            b.reportTitle = CreateDisplayText("TagsH", sidebar.transform, "", 26, TextAnchor.MiddleLeft);
            Stretch(b.reportTitle.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
            b.reportTitle.color = GameUiStyle.Accent;

            b.boosterHeading = CreateDisplayText("BoostH", sidebar.transform, "", 26, TextAnchor.MiddleLeft);
            Stretch(b.boosterHeading.rectTransform, new Vector2(0, 0.88f), Vector2.one, new Vector2(18, -10), new Vector2(-18, -4));
            b.boosterHeading.color = GameUiStyle.Accent;
            b.boosterHeading.gameObject.SetActive(false);

            // Keep reportBody as a hidden legacy binding (summary moved into stamp + tags).
            b.reportBody = CreateText("Body", sidebar.transform, "", 14, TextAnchor.UpperLeft);
            b.reportBody.gameObject.SetActive(false);

            b.purchaseTagList = CreateScrollContent(sidebar.transform, "PurchaseScroll", new Vector2(0, 0.14f), new Vector2(1, 0.88f), 8);
            b.boosterList = CreateScrollContent(sidebar.transform, "BoosterScroll", new Vector2(0, 0.14f), new Vector2(1, 0.88f), 6);
            var boosterScroll = sidebar.transform.Find("BoosterScroll");
            if (boosterScroll != null) boosterScroll.gameObject.SetActive(false);

            b.btnReportPanelToggle = CreateSecondaryButton("TogglePanel", sidebar.transform, out b.reportPanelToggleLabel);
            Place(b.btnReportPanelToggle, 0.04f, 0.02f, 0.48f, 0.12f);
            b.btnReportContinue = CreatePrimaryButton("Cont", sidebar.transform, out b.reportContinueLabel);
            Place(b.btnReportContinue, 0.52f, 0.02f, 0.96f, 0.12f);
            return root;
        }

        static GameObject BuildEnd(Transform parent, GameUiBindings b)
        {
            var root = CreatePanel("Screen_End", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().sprite = null;
            root.GetComponent<Image>().color = GameUiStyle.Bg;
            var card = CreatePanel("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -130), new Vector2(300, 130));
            GameUiStyle.ApplyPanel(card.GetComponent<Image>());
            b.endTitle = CreateDisplayText("Title", card.transform, "", 34, TextAnchor.MiddleCenter);
            Stretch(b.endTitle.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 0.9f), new Vector2(20, 0), new Vector2(-20, 0));
            b.endTitle.color = GameUiStyle.Accent;
            b.btnRestart = CreatePrimaryButton("Restart", card.transform, out b.restartLabel);
            Place(b.btnRestart, 0.2f, 0.12f, 0.8f, 0.35f);
            return root;
        }

        static void BuildEffectsHud(Transform chromeBar, Transform canvasRoot, GameUiBindings b)
        {
            var bar = CreatePanel("EffectsHud", chromeBar, new Vector2(0.28f, 0.12f), new Vector2(0.76f, 0.88f), Vector2.zero, Vector2.zero);
            bar.GetComponent<Image>().sprite = null;
            bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            bar.GetComponent<Image>().raycastTarget = false;
            bar.AddComponent<RectMask2D>();

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            b.effectsUpgrades = CreateEffectsRow(bar.transform, "Upgrades");
            b.effectsBoosters = CreateEffectsRow(bar.transform, "Boosters");

            var tip = CreatePanel("EffectTooltip", canvasRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120, -100), new Vector2(120, 10));
            GameUiStyle.ApplyCard(tip.GetComponent<Image>());
            tip.GetComponent<Image>().raycastTarget = false;
            var tipCg = tip.AddComponent<CanvasGroup>();
            tipCg.blocksRaycasts = false;
            tipCg.interactable = false;
            b.effectTooltipTitle = CreateDisplayText("Title", tip.transform, "", 14, TextAnchor.UpperLeft);
            Stretch(b.effectTooltipTitle.rectTransform, new Vector2(0, 0.62f), Vector2.one, new Vector2(12, -8), new Vector2(-12, -6));
            b.effectTooltipTitle.color = GameUiStyle.Accent;
            b.effectTooltipTitle.raycastTarget = false;
            b.effectTooltipBody = CreateText("Body", tip.transform, "", 12, TextAnchor.UpperLeft);
            Stretch(b.effectTooltipBody.rectTransform, new Vector2(0, 0), new Vector2(1, 0.62f), new Vector2(12, 8), new Vector2(-12, 0));
            b.effectTooltipBody.enableWordWrapping = true;
            b.effectTooltipBody.overflowMode = TextOverflowModes.Overflow;
            b.effectTooltipBody.raycastTarget = false;
            tip.SetActive(false);
            b.effectTooltip = tip;
        }

        static Transform CreateEffectsRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
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
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 28;
            le.preferredHeight = 28;
            le.flexibleHeight = 0;
            return go.transform;
        }

        public static GameObject BuildEffectIcon()
        {
            const float size = 28f;
            var go = new GameObject("EffectIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = size;
            le.minHeight = le.preferredHeight = size;
            le.flexibleWidth = 0;
            le.flexibleHeight = 0;
            // Invisible hit target so circular sprites keep clean corners.
            var hit = go.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            Stretch(iconGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var icon = iconGo.GetComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var view = go.AddComponent<EffectIconView>();
            view.icon = icon;
            return go;
        }

        // ─── Helpers (mirror GameUiShell factories) ─────────────────────────────

        static Transform CreateScrollContent(Transform parent, string name, Vector2 aMin, Vector2 aMax)
            => CreateScrollContent(parent, name, aMin, aMax, 8);

        static Transform CreateScrollContent(Transform parent, string name, Vector2 aMin, Vector2 aMax, int spacing)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            Stretch(scrollGo.GetComponent<RectTransform>(), aMin, aMax, new Vector2(10, 6), new Vector2(-10, -4));
            scrollGo.GetComponent<Image>().sprite = null;
            scrollGo.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.1f, 0.06f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = CreatePanel("Viewport", scrollGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().sprite = null;
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
            vlg.spacing = spacing;
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

        static GameObject CreateFixedBar(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = GameUiStyle.Panel;
            return go;
        }

        static GameObject CreatePanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            if (parent != null) go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), aMin, aMax, offMin, offMax);
            var img = go.GetComponent<Image>();
            img.color = GameUiStyle.Panel;
            return go;
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string value, int size, TextAnchor anchor)
        {
            return GameUiStyle.CreateTmpText(name, parent, value, size, anchor);
        }

        static TextMeshProUGUI CreateDisplayText(string name, Transform parent, string value, int size, TextAnchor anchor, bool bold = true)
        {
            return GameUiStyle.CreateDisplayText(name, parent, value, size, anchor, bold);
        }

        static void MakeUiBold(TextMeshProUGUI text)
        {
            GameUiStyle.ApplyUiFont(text, bold: true);
        }

        static Button CreateButton(string name, Transform parent, out TextMeshProUGUI label)
            => CreateSecondaryButton(name, parent, out label);

        static Button CreatePrimaryButton(string name, Transform parent, out TextMeshProUGUI label)
        {
            var btn = CreateButtonBase(name, parent, out label);
            GameUiStyle.ApplyPrimaryButton(btn.GetComponent<Image>(), label);
            return btn;
        }

        static Button CreateSecondaryButton(string name, Transform parent, out TextMeshProUGUI label)
        {
            var btn = CreateButtonBase(name, parent, out label);
            GameUiStyle.ApplySecondaryButton(btn.GetComponent<Image>(), label);
            return btn;
        }

        static Button CreateDangerButton(string name, Transform parent, out TextMeshProUGUI label)
        {
            var btn = CreateButtonBase(name, parent, out label);
            GameUiStyle.ApplyDangerButton(btn.GetComponent<Image>(), label);
            return btn;
        }

        static Button CreateButtonBase(string name, Transform parent, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            label = CreateText("Label", go.transform, name, 16, TextAnchor.MiddleCenter);
            MakeUiBold(label);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
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
