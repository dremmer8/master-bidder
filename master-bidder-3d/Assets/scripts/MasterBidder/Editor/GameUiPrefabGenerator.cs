using System.IO;
using MasterBidder.Flow;
using MasterBidder.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Bootstraps editable UI prefabs under Assets/content/ui/.
    /// Menu: Master Bidder → Generate UI Prefabs
    /// After generation, tweak layout/colors on the prefabs; GameUiShell instantiates them at runtime.
    /// Re-running overwrites prefabs with the default hierarchy from GameUiHierarchyFactory.
    /// </summary>
    public static class GameUiPrefabGenerator
    {
        public const string UiFolder = "Assets/content/ui";
        public const string WidgetsFolder = "Assets/content/ui/widgets";
        public const string GameUiPrefabPath = UiFolder + "/GameUI.prefab";
        public const string CollectorCardPath = WidgetsFolder + "/CollectorCard.prefab";
        public const string UpgradeRowPath = WidgetsFolder + "/UpgradeRow.prefab";
        public const string BoosterRowPath = WidgetsFolder + "/BoosterRow.prefab";

        [MenuItem("Master Bidder/Generate UI Prefabs", priority = 20)]
        public static void GenerateMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameUiPrefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Generate UI Prefabs",
                        "UI prefabs already exist. Re-generate and overwrite with the default layout?\n\n" +
                        "Cancel if you have customized the prefabs.",
                        "Overwrite",
                        "Cancel"))
                    return;
            }

            bool ok = Generate();
            if (ok)
            {
                EditorUtility.DisplayDialog(
                    "UI Prefabs",
                    "Created/updated:\n" +
                    $"• {GameUiPrefabPath}\n" +
                    $"• {CollectorCardPath}\n" +
                    $"• {UpgradeRowPath}\n" +
                    $"• {BoosterRowPath}\n\n" +
                    "Assigned on GameUiShell in the open scene (if present).\n" +
                    "Edit the prefabs to control look; Play uses them at runtime.",
                    "OK");
            }
        }

        /// <summary>Batchmode entry: -executeMethod MasterBidder.Editor.GameUiPrefabGenerator.GenerateBatch</summary>
        public static void GenerateBatch()
        {
            if (!Generate())
                EditorApplication.Exit(1);
        }

        public static bool Generate()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "content/ui/widgets"));
            AssetDatabase.Refresh();

            var collectorCard = GameUiHierarchyFactory.BuildCollectorCard();
            var upgradeRow = GameUiHierarchyFactory.BuildUpgradeRow();
            var boosterRow = GameUiHierarchyFactory.BuildBoosterRow();
            var gameUi = GameUiHierarchyFactory.BuildGameUi();

            PrefabUtility.SaveAsPrefabAsset(collectorCard, CollectorCardPath);
            PrefabUtility.SaveAsPrefabAsset(upgradeRow, UpgradeRowPath);
            PrefabUtility.SaveAsPrefabAsset(boosterRow, BoosterRowPath);
            PrefabUtility.SaveAsPrefabAsset(gameUi, GameUiPrefabPath);

            Object.DestroyImmediate(collectorCard);
            Object.DestroyImmediate(upgradeRow);
            Object.DestroyImmediate(boosterRow);
            Object.DestroyImmediate(gameUi);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignToOpenScene();
            Debug.Log("[GameUiPrefabGenerator] UI prefabs ready under " + UiFolder);
            return true;
        }

        static void AssignToOpenScene()
        {
            var gameUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameUiPrefabPath);
            var card = AssetDatabase.LoadAssetAtPath<GameObject>(CollectorCardPath);
            var upgrade = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradeRowPath);
            var booster = AssetDatabase.LoadAssetAtPath<GameObject>(BoosterRowPath);

            var shell = Object.FindObjectOfType<GameUiShell>();
            if (shell == null)
            {
                var flow = Object.FindObjectOfType<AppFlow>();
                if (flow != null)
                    shell = flow.GetComponent<GameUiShell>() ?? flow.gameObject.AddComponent<GameUiShell>();
            }

            if (shell == null) return;

            var so = new SerializedObject(shell);
            so.FindProperty("gameUiPrefab").objectReferenceValue = gameUi;
            so.FindProperty("collectorCardPrefab").objectReferenceValue = card;
            so.FindProperty("upgradeRowPrefab").objectReferenceValue = upgrade;
            so.FindProperty("boosterRowPrefab").objectReferenceValue = booster;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shell);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
