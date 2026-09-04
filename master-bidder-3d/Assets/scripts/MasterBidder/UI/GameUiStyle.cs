using UnityEngine;

namespace MasterBidder.UI
{
    /// <summary>Shared palette for Master Bidder uGUI (prefabs + runtime tints).</summary>
    public static class GameUiStyle
    {
        public static readonly Color Bg = new Color(0.086f, 0.094f, 0.114f, 0.96f);
        public static readonly Color Panel = new Color(0.129f, 0.141f, 0.169f, 0.97f);
        public static readonly Color PanelLight = new Color(0.169f, 0.184f, 0.220f, 1f);
        public static readonly Color Accent = new Color(0.831f, 0.631f, 0.227f, 1f);
        public static readonly Color TextColor = new Color(0.925f, 0.933f, 0.945f, 1f);
        public static readonly Color Dim = new Color(0.604f, 0.631f, 0.671f, 1f);
        public static readonly Color Good = new Color(0.298f, 0.686f, 0.490f, 1f);
        public static readonly Color Bad = new Color(0.831f, 0.341f, 0.227f, 1f);
        public static readonly Color RivalIdle = new Color(0.25f, 0.27f, 0.32f, 1f);
    }
}
