using UnityEngine;

namespace MasterBidder.UI
{
    /// <summary>
    /// Loads permanent-upgrade and temporary-booster icons from Resources/MetaIcons.
    /// Filenames match CampaignConfig ids (e.g. cool-nerves.png, insurance.png).
    /// </summary>
    public static class GameUiMetaIcons
    {
        const string ResPath = "MetaIcons/";
        const string EditorFolder = "Assets/content/ui/Resources/MetaIcons";

        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
#if UNITY_EDITOR
            var editor = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{EditorFolder}/{id}.png");
            if (editor != null) return editor;
#endif
            return Resources.Load<Sprite>(ResPath + id);
        }
    }
}
