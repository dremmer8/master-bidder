using UnityEngine;

namespace MasterBidder.UI
{
    /// <summary>
    /// Loads sliced UI sprites from Resources/UiSprites (exported from the paper-gold atlas).
    /// </summary>
    public static class GameUiSprites
    {
        const string ResPath = "UiSprites/";

        static Sprite _panel;
        static Sprite _panelCard;
        static Sprite _btnPrimary;
        static Sprite _btnDanger;
        static Sprite _btnSecondary;
        static Sprite _btnSkip;
        static Sprite _btnCalendar;
        static Sprite _barCurrency;
        static Sprite _barDay;
        static Sprite _banner;
        static Sprite _divider;
        static Sprite _toastSuccess;
        static Sprite _toastInfo;
        static Sprite _toastWarn;
        static Sprite _toastError;
        static Sprite _iconClose;
        static Sprite _iconInfo;
        static bool _loaded;

        public static Sprite Panel => Ensure(ref _panel, "panel");
        public static Sprite PanelCard => Ensure(ref _panelCard, "panel_card");
        public static Sprite BtnPrimary => Ensure(ref _btnPrimary, "btn_primary");
        public static Sprite BtnDanger => Ensure(ref _btnDanger, "btn_danger");
        public static Sprite BtnSecondary => Ensure(ref _btnSecondary, "btn_secondary");
        public static Sprite BtnSkip => Ensure(ref _btnSkip, "btn_skip");
        public static Sprite BtnCalendar => Ensure(ref _btnCalendar, "btn_calendar");
        public static Sprite BarCurrency => Ensure(ref _barCurrency, "bar_currency");
        public static Sprite BarDay => Ensure(ref _barDay, "bar_day");
        public static Sprite Banner => Ensure(ref _banner, "banner");
        public static Sprite Divider => Ensure(ref _divider, "divider");
        public static Sprite ToastSuccess => Ensure(ref _toastSuccess, "toast_success");
        public static Sprite ToastInfo => Ensure(ref _toastInfo, "toast_info");
        public static Sprite ToastWarn => Ensure(ref _toastWarn, "toast_warn");
        public static Sprite ToastError => Ensure(ref _toastError, "toast_error");
        public static Sprite IconClose => Ensure(ref _iconClose, "icon_close");
        public static Sprite IconInfo => Ensure(ref _iconInfo, "icon_info");

        public static void Warmup()
        {
            if (_loaded) return;
            _ = Panel;
            _ = PanelCard;
            _ = BtnPrimary;
            _ = BtnDanger;
            _ = BtnSecondary;
            _ = BarCurrency;
            _ = BarDay;
            _ = Banner;
            _loaded = true;
        }

        static Sprite Ensure(ref Sprite cache, string name)
        {
            if (cache != null) return cache;
#if UNITY_EDITOR
            cache = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/content/ui/sprites/sliced/{name}.png");
            if (cache != null) return cache;
            cache = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/content/ui/Resources/UiSprites/{name}.png");
            if (cache != null) return cache;
#endif
            cache = Resources.Load<Sprite>(ResPath + name);
            return cache;
        }
    }
}
