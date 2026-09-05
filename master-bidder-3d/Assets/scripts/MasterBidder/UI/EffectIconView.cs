using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Single active upgrade/booster chip in the auction HUD (MVP hud-effects).
    /// </summary>
    public class EffectIconView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image icon;
        public string Title;
        public string Body;
        public Action<EffectIconView> OnHover;
        public Action OnLeave;

        public void OnPointerEnter(PointerEventData eventData) => OnHover?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => OnLeave?.Invoke();
    }
}
