using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Shared palette + sprite helpers for Master Bidder uGUI (paper/gold atlas).
    /// Typography: Playfair Display (titles) + Manrope (UI), both with Cyrillic.
    /// </summary>
    public static class GameUiStyle
    {
        // Palette aligned with UiAtlas / UiStyleGuide
        public static readonly Color Bg = Hex(0xF5F2EB, 0.96f);
        /// <summary>Warm dark stage behind paper plates (style-guide Primary / MVP room).</summary>
        public static readonly Color ScreenBg = Hex(0x2B241D);
        public static readonly Color Panel = Hex(0xE2D8CC);
        public static readonly Color PanelLight = Hex(0xF3EBE0);
        public static readonly Color Accent = Hex(0xC6A05B);
        public static readonly Color TextColor = Hex(0x3E3E3E);
        public static readonly Color Dim = Hex(0x6F6F6F);
        public static readonly Color Good = Hex(0x2E6B3F);
        public static readonly Color Bad = Hex(0x8B2D2D);
        public static readonly Color RivalIdle = Hex(0x5A534A);
        public static readonly Color OnPrimary = Color.white;
        public static readonly Color OnDark = Color.white;
        public static readonly Color Overlay = new Color(0.08f, 0.06f, 0.04f, 0.55f);
        public static readonly Color SpriteReady = Color.white;
        public static readonly Color SpriteDisabled = new Color(1f, 1f, 1f, 0.42f);
        public static readonly Color SelectedTint = new Color(1f, 0.94f, 0.82f, 1f);

        static TMP_FontAsset _uiFont;
        static TMP_FontAsset _uiBoldFont;
        static TMP_FontAsset _displayFont;
        static TMP_FontAsset _displayBoldFont;
        static TMP_FontAsset _displayItalicFont;

        /// <summary>Manrope — UI body, HUD, buttons, field labels.</summary>
        public static TMP_FontAsset UiFont =>
            _uiFont ??= LoadFont("Fonts/Manrope SDF");

        /// <summary>Manrope Bold — button labels and dense HUD emphasis.</summary>
        public static TMP_FontAsset UiBoldFont =>
            _uiBoldFont ??= LoadFont("Fonts/Manrope Bold SDF") ?? UiFont;

        /// <summary>Playfair Display — titles, collector names, section headings.</summary>
        public static TMP_FontAsset DisplayFont =>
            _displayFont ??= LoadFont("Fonts/PlayfairDisplay SDF");

        /// <summary>Playfair Display Bold — primary titles / banners.</summary>
        public static TMP_FontAsset DisplayBoldFont =>
            _displayBoldFont ??= LoadFont("Fonts/PlayfairDisplay Bold SDF") ?? DisplayFont;

        /// <summary>Playfair Display Italic — speech / quotes.</summary>
        public static TMP_FontAsset DisplayItalicFont =>
            _displayItalicFont ??= LoadFont("Fonts/PlayfairDisplay Italic SDF") ?? DisplayFont;

        /// <summary>Default UI font (Manrope), with LiberationSans fallback.</summary>
        public static TMP_FontAsset DefaultTmpFont => UiFont ?? LiberationFallback;

        static TMP_FontAsset LiberationFallback
        {
            get
            {
                if (TMP_Settings.defaultFontAsset != null)
                    return TMP_Settings.defaultFontAsset;
                return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
        }

        public static void InvalidateFontCache()
        {
            _uiFont = null;
            _uiBoldFont = null;
            _displayFont = null;
            _displayBoldFont = null;
            _displayItalicFont = null;
        }

        static TMP_FontAsset LoadFont(string sdfResourcesPath)
        {
            var font = Resources.Load<TMP_FontAsset>(sdfResourcesPath);
            if (font != null) return font;

            // SDF assets are created by Master Bidder → Create UI Font Assets (editor).
            Debug.LogWarning(
                "[GameUiStyle] Missing TMP font Resources/" + sdfResourcesPath +
                ". Run Master Bidder → Create UI Font Assets.");
            return null;
        }

        public static Color Hex(int rgb, float a = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        public static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.TopLeft;
            }
        }

        public static FontStyles ToTmpFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold: return FontStyles.Bold;
                case FontStyle.Italic: return FontStyles.Italic;
                case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
                default: return FontStyles.Normal;
            }
        }

        /// <summary>uGUI lineSpacing multiplier (1 = default) → TMP percent delta.</summary>
        public static float ToTmpLineSpacing(float uguiMultiplier) => (uguiMultiplier - 1f) * 100f;

        public static void ApplyDefaultFont(TMP_Text text) => ApplyUiFont(text);

        public static void ApplyUiFont(TMP_Text text, bool bold = false)
        {
            if (text == null) return;
            var font = bold ? UiBoldFont : UiFont;
            if (font == null) font = LiberationFallback;
            if (font != null) text.font = font;
            // Real bold face is assigned above — avoid double synthetic bold.
            if (bold) text.fontStyle = FontStyles.Normal;
        }

        public static void ApplyDisplayFont(TMP_Text text, bool bold = false)
        {
            if (text == null) return;
            var font = bold ? DisplayBoldFont : DisplayFont;
            if (font == null) font = DefaultTmpFont;
            if (font != null) text.font = font;
            if (bold) text.fontStyle = FontStyles.Normal;
        }

        public static void ApplyDisplayItalic(TMP_Text text)
        {
            if (text == null) return;
            var font = DisplayItalicFont ?? DisplayFont ?? DefaultTmpFont;
            if (font != null) text.font = font;
            text.fontStyle = FontStyles.Normal;
        }

        public static TextMeshProUGUI CreateTmpText(string name, Transform parent, string value, float size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value ?? "";
            ApplyUiFont(text);
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = ToTmpAlignment(anchor);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static TextMeshProUGUI CreateDisplayText(string name, Transform parent, string value, float size, TextAnchor anchor, bool bold = true)
        {
            var text = CreateTmpText(name, parent, value, size, anchor);
            ApplyDisplayFont(text, bold);
            return text;
        }

        /// <summary>
        /// Applies Playfair/Manrope roles to wired GameUI bindings (prefabs may still
        /// carry LiberationSans until regenerated).
        /// </summary>
        public static void ApplyTypography(GameUiBindings b)
        {
            if (b == null) return;

            ApplyDisplayFont(b.chromeTitle, bold: true);
            ApplyDisplayFont(b.introTitle, bold: true);
            ApplyDisplayFont(b.introSubtitle, bold: true);
            ApplyUiFont(b.introLede);
            ApplyUiFont(b.introRules);
            ApplyUiFont(b.continueLabel, bold: true);
            ApplyUiFont(b.startLabel, bold: true);

            ApplyUiFont(b.briefDay, bold: true);
            ApplyUiFont(b.briefCapital, bold: true);
            ApplyDisplayFont(b.briefActiveName, bold: true);
            ApplyUiFont(b.briefActiveTags, bold: true);
            ApplyDisplayFont(b.briefClientHeading, bold: true);
            ApplyDisplayFont(b.briefWorkshopHeading, bold: true);
            ApplyUiFont(b.briefPanelToggleLabel, bold: true);
            ApplyUiFont(b.enterLabel, bold: true);
            ApplyUiFont(b.resetLabel, bold: true);

            ApplyDisplayFont(b.auctionActiveName, bold: true);
            ApplyUiFont(b.auctionActiveTags, bold: true);
            ApplyUiFont(b.aucHud, bold: true);
            ApplyUiFont(b.orderCard);
            ApplyUiFont(b.livePrice, bold: true);
            ApplyUiFont(b.liveBudget, bold: true);
            ApplyUiFont(b.liveSpeed);
            ApplyUiFont(b.resultBanner, bold: true);
            ApplyUiFont(b.fundsHint);
            ApplyUiFont(b.familiarBadge);
            ApplyUiFont(b.startLotLabel, bold: true);
            ApplyUiFont(b.buyLabel, bold: true);
            ApplyUiFont(b.skipLabel, bold: true);
            ApplyUiFont(b.finishLabel, bold: true);

            if (b.fieldLabels != null)
            {
                for (int i = 0; i < b.fieldLabels.Length; i++)
                    ApplyUiFont(b.fieldLabels[i]);
            }

            if (b.fieldValues != null)
            {
                for (int i = 0; i < b.fieldValues.Length; i++)
                    ApplyUiFont(b.fieldValues[i]);
            }

            ApplyDisplayFont(b.popupName, bold: true);
            ApplyUiFont(b.popupTagline);
            ApplyDisplayItalic(b.popupSpeech);
            ApplyDisplayFont(b.popupTags, bold: true);
            ApplyUiFont(b.popupWarning);
            ApplyUiFont(b.popupStartLabel, bold: true);

            ApplyDisplayFont(b.pcTitle, bold: true);
            ApplyDisplayFont(b.pcArtist, bold: false);
            ApplyUiFont(b.pcMeta);
            ApplyUiFont(b.pcFact);
            ApplyUiFont(b.pcContinueLabel, bold: true);

            ApplyUiFont(b.tutorialText, bold: true);

            ApplyDisplayFont(b.reportStampLabel, bold: true);
            ApplyUiFont(b.reportStampDetail);
            ApplyDisplayFont(b.reportTitle, bold: true);
            ApplyDisplayFont(b.boosterHeading, bold: true);
            ApplyUiFont(b.reportBody);
            ApplyUiFont(b.reportPanelToggleLabel, bold: true);
            ApplyUiFont(b.reportContinueLabel, bold: true);

            ApplyDisplayFont(b.endTitle, bold: true);
            ApplyUiFont(b.restartLabel, bold: true);

            ApplyDisplayFont(b.effectTooltipTitle, bold: true);
            ApplyUiFont(b.effectTooltipBody);

            if (b.langDropdown != null)
            {
                ApplyUiFont(b.langDropdown.captionText);
                ApplyUiFont(b.langDropdown.itemText);
            }
        }

        public static void ApplyWidgetTypography(CollectorCardView view)
        {
            if (view == null) return;
            ApplyUiFont(view.label);
        }

        public static void ApplyWidgetTypography(UpgradeRowView view)
        {
            if (view == null) return;
            ApplyUiFont(view.label);
            ApplyUiFont(view.buyLabel, bold: true);
        }

        public static void ApplyWidgetTypography(BoosterRowView view)
        {
            if (view == null) return;
            ApplyUiFont(view.label);
            ApplyUiFont(view.buyLabel, bold: true);
        }

        public static void ApplyWidgetTypography(PurchaseTagView view)
        {
            if (view == null) return;
            ApplyDisplayFont(view.title, bold: true);
            ApplyUiFont(view.meta);
            ApplyDisplayFont(view.stamp, bold: true);
        }

        public static void SetWordWrap(TextMeshProUGUI text, bool wrap, TextOverflowModes overflow = TextOverflowModes.Overflow)
        {
            if (text == null) return;
            text.enableWordWrapping = wrap;
            text.overflowMode = overflow;
        }

        public static void ApplySliced(Image img, Sprite sprite, Color? tint = null)
        {
            if (img == null) return;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = 1f;
                img.color = tint ?? SpriteReady;
            }
            else if (tint.HasValue)
            {
                img.color = tint.Value;
            }
        }

        public static void ApplyPanel(Image img) => ApplySliced(img, GameUiSprites.Panel, SpriteReady);

        /// <summary>
        /// Wide modal plate: sliced body without the baked crest, plus a fixed-aspect ornament
        /// so the top flourish does not stretch with the card width.
        /// </summary>
        public static void ApplyFramedPanel(Image img)
        {
            ApplySliced(img, GameUiSprites.PanelBody ?? GameUiSprites.Panel, SpriteReady);
            EnsurePanelOrnament(img != null ? img.transform : null);
        }

        public static void ApplyCard(Image img) => ApplySliced(img, GameUiSprites.PanelCard, SpriteReady);

        public static void EnsurePanelOrnament(Transform card)
        {
            if (card == null) return;
            var sprite = GameUiSprites.PanelOrnament;
            if (sprite == null) return;

            Transform existing = card.Find("Ornament");
            GameObject go = existing != null ? existing.gameObject : null;
            if (go == null)
            {
                go = new GameObject("Ornament", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(card, false);
                go.transform.SetAsFirstSibling();
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(126f, 24f);
            rt.anchoredPosition = new Vector2(0f, 6f);

            var ornament = go.GetComponent<Image>();
            ornament.sprite = sprite;
            ornament.type = Image.Type.Simple;
            ornament.preserveAspect = true;
            ornament.raycastTarget = false;
            ornament.color = SpriteReady;
        }

        public static void ApplyPrimaryButton(Image img, TextMeshProUGUI label = null)
        {
            ApplySliced(img, GameUiSprites.BtnPrimary, SpriteReady);
            if (label != null) label.color = OnPrimary;
        }

        public static void ApplyDangerButton(Image img, TextMeshProUGUI label = null)
        {
            ApplySliced(img, GameUiSprites.BtnDanger, SpriteReady);
            if (label != null) label.color = OnPrimary;
        }

        public static void ApplySecondaryButton(Image img, TextMeshProUGUI label = null)
        {
            ApplySliced(img, GameUiSprites.BtnSecondary, SpriteReady);
            if (label != null) label.color = TextColor;
        }

        public static void SetSpriteEnabled(Image img, bool enabled)
        {
            if (img == null) return;
            img.color = enabled ? SpriteReady : SpriteDisabled;
        }
    }
}
