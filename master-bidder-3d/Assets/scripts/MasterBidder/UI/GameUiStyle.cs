using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Shared palette + sprite helpers for Master Bidder uGUI (paper/gold atlas).
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

        public static Color Hex(int rgb, float a = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, a);
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

        public static void ApplyPrimaryButton(Image img, Text label = null)
        {
            ApplySliced(img, GameUiSprites.BtnPrimary, SpriteReady);
            if (label != null) label.color = OnPrimary;
        }

        public static void ApplyDangerButton(Image img, Text label = null)
        {
            ApplySliced(img, GameUiSprites.BtnDanger, SpriteReady);
            if (label != null) label.color = OnPrimary;
        }

        public static void ApplySecondaryButton(Image img, Text label = null)
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
