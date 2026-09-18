using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace MasterBidder.UI
{
    /// <summary>
    /// Hold-to-study: full-height dark fill grows across the row; label/value
    /// lerp to white so the line morphs into the learned look.
    /// </summary>
    public class FieldStudyHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public static readonly Color LearnedRowColor = new Color(0.18f, 0.14f, 0.11f, 0.92f);

        /// <summary>Progress range where text finishes turning white.</summary>
        const float TextWhiteStart = 0.2f;
        const float TextWhiteEnd = 0.65f;

        public int FieldIndex;
        public Image StudyTrack;
        public Image StudyFill;

        static Sprite _whiteSprite;

        GameUiShell _shell;
        TextMeshProUGUI _label;
        TextMeshProUGUI _value;
        bool _holding;

        public void Bind(GameUiShell shell, int fieldIndex, Image studyTrack, Image studyFill)
        {
            _shell = shell;
            FieldIndex = fieldIndex;
            StudyTrack = studyTrack;
            StudyFill = studyFill;
            CacheTexts();
            EnsureFillSprite();
            RefreshFromShell();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_shell == null || !_shell.TryBeginFieldStudy(FieldIndex)) return;
            _holding = true;
            ApplyProgressVisual(_shell.GetFieldStudyProgress(FieldIndex), forceShowBar: true);
        }

        public void OnPointerUp(PointerEventData eventData) => StopHold();

        public void OnPointerExit(PointerEventData eventData) => StopHold();

        void StopHold()
        {
            if (!_holding) return;
            _holding = false;
            _shell?.EndFieldStudy(FieldIndex);
            RefreshFromShell();
        }

        /// <summary>Stop filling only — keep current bar fill visible.</summary>
        public void ForceStopFill()
        {
            _holding = false;
            RefreshFromShell();
        }

        void Update()
        {
            if (!_holding || _shell == null) return;
            if (!_shell.IsFieldStudyAllowed(FieldIndex))
            {
                _holding = false;
                _shell.CancelFieldStudy(FieldIndex);
                RefreshFromShell(); // freeze bar at current progress
                return;
            }

            float progress = _shell.TickFieldStudy(FieldIndex, Time.deltaTime);
            ApplyProgressVisual(progress, forceShowBar: true);
            if (progress >= 1f)
            {
                _holding = false;
                _shell.EndFieldStudy(FieldIndex);
                RefreshFromShell();
            }
        }

        /// <summary>Sync bar + text tint from saved progress (after reveal refresh).</summary>
        public void RefreshFromShell()
        {
            if (_shell == null)
            {
                _holding = false;
                SetBarActive(false);
                return;
            }

            _holding = _holding && _shell.IsFieldStudyAllowed(FieldIndex);

            float progress = _shell.GetFieldStudyProgress(FieldIndex);
            bool learned = progress >= 1f;
            // Show frozen progress on revealed rows even after lot resolve; hide only when masked/empty.
            bool show = !learned
                        && progress > 0.001f
                        && _shell.ShouldShowStudyProgressBar(FieldIndex);
            if (!show)
            {
                SetBarActive(false);
                return;
            }

            ApplyProgressVisual(progress, forceShowBar: true);
        }

        void ApplyProgressVisual(float progress, bool forceShowBar)
        {
            progress = Mathf.Clamp01(progress);
            if (forceShowBar)
                SetBarActive(true);

            if (StudyFill != null)
            {
                EnsureFillSprite();
                StudyFill.fillAmount = progress;
                StudyFill.color = LearnedRowColor;
            }

            CacheTexts();
            float textT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TextWhiteStart, TextWhiteEnd, progress));
            if (_label != null)
                _label.color = Color.Lerp(GameUiStyle.Dim, GameUiStyle.OnDark, textT);
            if (_value != null)
                _value.color = Color.Lerp(GameUiStyle.TextColor, GameUiStyle.OnDark, textT);
        }

        void CacheTexts()
        {
            if (_label != null && _value != null) return;
            var t = transform;
            _label = t.Find("L")?.GetComponent<TextMeshProUGUI>();
            _value = t.Find("V")?.GetComponent<TextMeshProUGUI>();
        }

        void SetBarActive(bool visible)
        {
            Transform root = null;
            if (StudyFill != null && StudyFill.transform.parent != null
                && StudyFill.transform.parent.name == "StudyBar")
                root = StudyFill.transform.parent;
            else if (StudyTrack != null && StudyTrack.transform.parent != null)
                root = StudyTrack.transform.parent;

            if (root != null)
                root.gameObject.SetActive(visible);
            else if (StudyFill != null)
                StudyFill.gameObject.SetActive(visible);
        }

        void EnsureFillSprite()
        {
            if (StudyFill == null) return;
            if (StudyFill.sprite == null)
                StudyFill.sprite = WhiteSprite();
            StudyFill.type = Image.Type.Filled;
            StudyFill.fillMethod = Image.FillMethod.Horizontal;
            StudyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        public static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _whiteSprite.name = "StudyBarWhite";
            return _whiteSprite;
        }

        /// <summary>Full-row dark fill behind label/value text.</summary>
        public static void EnsureBarUnderRow(Transform row, out Image track, out Image fill)
        {
            track = null;
            fill = null;
            if (row == null) return;

            var existing = row.Find("StudyBar");
            GameObject barRoot;
            if (existing != null)
                barRoot = existing.gameObject;
            else
            {
                barRoot = new GameObject("StudyBar", typeof(RectTransform));
                barRoot.transform.SetParent(row, false);
            }

            // Always behind text; full row height.
            barRoot.transform.SetAsFirstSibling();
            var barRt = barRoot.GetComponent<RectTransform>();
            if (barRt == null) barRt = barRoot.AddComponent<RectTransform>();
            StretchFull(barRt);

            track = barRoot.transform.Find("Track")?.GetComponent<Image>();
            fill = barRoot.transform.Find("Fill")?.GetComponent<Image>();

            if (track == null)
            {
                var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
                trackGo.transform.SetParent(barRoot.transform, false);
                StretchFull(trackGo.GetComponent<RectTransform>());
                track = trackGo.GetComponent<Image>();
                track.sprite = WhiteSprite();
                track.type = Image.Type.Simple;
                track.raycastTarget = false;
            }

            // Track stays invisible — fill alone paints the learned color.
            track.color = new Color(0f, 0f, 0f, 0f);

            if (fill == null)
            {
                var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillGo.transform.SetParent(barRoot.transform, false);
                StretchFull(fillGo.GetComponent<RectTransform>());
                fill = fillGo.GetComponent<Image>();
                fill.sprite = WhiteSprite();
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;
            }

            fill.color = LearnedRowColor;
            barRoot.SetActive(false);
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
