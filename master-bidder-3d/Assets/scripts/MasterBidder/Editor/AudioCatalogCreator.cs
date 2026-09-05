using MasterBidder.Audio;
using UnityEditor;
using UnityEngine;

namespace MasterBidder.Editor
{
    public static class AudioCatalogCreator
    {
        const string ResourcesDir = "Assets/content/audio/Resources";
        const string AssetPath = ResourcesDir + "/AudioCatalog.asset";

        [MenuItem("Master Bidder/Create Audio Catalog", priority = 16)]
        public static void Create()
        {
            EnsureFolders();
            var existing = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog(
                    "Audio Catalog",
                    "Already exists at:\n" + AssetPath + "\n\nAssign it on AppFlow → Audio Catalog (optional; also auto-loads from Resources).",
                    "OK");
                return;
            }

            var asset = ScriptableObject.CreateInstance<AudioCatalog>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[AudioCatalog] Created " + AssetPath);
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/content"))
                AssetDatabase.CreateFolder("Assets", "content");
            if (!AssetDatabase.IsValidFolder("Assets/content/audio"))
                AssetDatabase.CreateFolder("Assets/content", "audio");
            if (!AssetDatabase.IsValidFolder(ResourcesDir))
                AssetDatabase.CreateFolder("Assets/content/audio", "Resources");
        }
    }
}
