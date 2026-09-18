using MasterBidder.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Vertical hang-tag for a purchased lot: painting thumb, meta fields,
    /// perforated tear-off with broker/title duplicate and match stamp.
    /// </summary>
    public class HangTagView : MonoBehaviour
    {
        public Image background;
        public Image holeRing;
        public Image holeCore;
        public Image thumbnail;
        public GameObject thumbnailRoot;

        public TextMeshProUGUI titleLabel;
        public TextMeshProUGUI titleValue;
        public TextMeshProUGUI styleLabel;
        public TextMeshProUGUI styleValue;
        public TextMeshProUGUI genreLabel;
        public TextMeshProUGUI genreValue;
        public TextMeshProUGUI authorLabel;
        public TextMeshProUGUI authorValue;
        public TextMeshProUGUI ownerLabel;
        public TextMeshProUGUI ownerValue;

        public TextMeshProUGUI brokerLabel;
        public TextMeshProUGUI brokerValue;
        public TextMeshProUGUI tearTitleLabel;
        public TextMeshProUGUI tearTitleValue;

        public Image stampBackground;
        public TextMeshProUGUI stamp;

        public void ApplyLocalizedLabels()
        {
            Set(titleLabel, LocaleService.T("hangTag.title") + ":");
            Set(styleLabel, LocaleService.T("hangTag.style") + ":");
            Set(genreLabel, LocaleService.T("hangTag.genre") + ":");
            Set(authorLabel, LocaleService.T("hangTag.author") + ":");
            Set(ownerLabel, LocaleService.T("hangTag.owner") + ":");
            Set(brokerLabel, LocaleService.T("hangTag.broker") + ":");
            Set(tearTitleLabel, LocaleService.T("hangTag.title") + ":");
        }

        public void Bind(
            Sprite paintingThumb,
            string title,
            string style,
            string genre,
            string author,
            string owner,
            string broker,
            bool matched)
        {
            ApplyLocalizedLabels();

            if (thumbnail != null)
            {
                thumbnail.sprite = paintingThumb;
                thumbnail.color = paintingThumb != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                thumbnail.preserveAspect = true;
            }

            Set(titleValue, title);
            Set(tearTitleValue, title);
            Set(styleValue, style);
            Set(genreValue, genre);
            Set(authorValue, author);
            Set(ownerValue, owner);
            Set(brokerValue, broker);

            if (stamp != null)
            {
                stamp.text = LocaleService.T(matched ? "report.correct" : "report.incorrect");
                stamp.color = matched ? GameUiStyle.Good : GameUiStyle.Bad;
            }

            if (stampBackground != null)
            {
                var c = matched ? GameUiStyle.Good : GameUiStyle.Bad;
                stampBackground.color = new Color(c.r, c.g, c.b, 0.18f);
            }
        }

        static void Set(TMP_Text text, string value)
        {
            if (text == null) return;
            text.text = value ?? "";
        }
    }
}
