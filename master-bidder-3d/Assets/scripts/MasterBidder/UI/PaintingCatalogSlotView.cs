using MasterBidder.Campaign;
using MasterBidder.Core;
using MasterBidder.Services;
using NineSlice3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// One painting entry on a catalog book spread.
    /// </summary>
    public class PaintingCatalogSlotView : MonoBehaviour
    {
        public GameObject root;
        public RawImage thumbnail;
        public TextMeshProUGUI unknownMark;
        public TextMeshProUGUI[] fieldLabels = new TextMeshProUGUI[5];
        public TextMeshProUGUI[] fieldValues = new TextMeshProUGUI[5];

        Texture2D _boundTexture;

        public void Bind(PaintingData painting, GameState state)
        {
            if (root != null) root.SetActive(painting != null);
            if (painting == null) return;

            bool discovered = CatalogProgress.IsDiscovered(state, painting.artworkId);
            if (unknownMark != null)
            {
                unknownMark.gameObject.SetActive(!discovered);
                unknownMark.text = LocaleService.T("catalog.unknown");
            }

            if (thumbnail != null)
            {
                thumbnail.gameObject.SetActive(discovered);
                if (discovered)
                {
                    _boundTexture = painting.albedoTexture;
                    thumbnail.texture = _boundTexture;
                    thumbnail.color = _boundTexture != null ? Color.white : new Color(0.85f, 0.82f, 0.76f, 1f);
                }
                else
                {
                    thumbnail.texture = null;
                }
            }

            var fields = CampaignConfig.RevealableFields;
            for (int i = 0; i < fieldLabels.Length && i < fields.Length; i++)
            {
                if (fieldLabels[i] != null)
                    fieldLabels[i].text = LocaleService.T("auction.field." + fields[i]);

                if (fieldValues[i] == null) continue;
                bool learned = discovered && CatalogProgress.IsLearned(state, painting.artworkId, i);
                if (learned)
                {
                    fieldValues[i].text = CatalogProgress.FieldValue(painting, fields[i]);
                    fieldValues[i].color = GameUiStyle.TextColor;
                }
                else
                {
                    fieldValues[i].text = discovered ? "—" : string.Empty;
                    fieldValues[i].color = GameUiStyle.Dim;
                }
            }
        }

        public void Clear()
        {
            if (root != null) root.SetActive(false);
            _boundTexture = null;
            if (thumbnail != null) thumbnail.texture = null;
        }
    }
}
