using System.IO;
using MasterBidder.Flow;
using MasterBidder.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Bootstraps editable UI prefabs under Assets/content/ui/.
    /// Menu: Master Bidder → Generate UI Prefabs
    ///
    /// Workflow:
    /// 1. Generate once to create the default hierarchy (fonts, spacing, anchors, sprites).
    /// 2. Edit panels freely in the Prefab Editor — RectTransform, TMP fonts/sizes,
    ///    VerticalLayoutGroup spacing, LayoutElement heights, colors, sprites, etc.
    /// 3. GameUiShell instantiates these prefabs and only fills dynamic text / state tints.
    ///
    /// Re-running overwrites prefabs with the default hierarchy from GameUiHierarchyFactory
    /// (confirm dialog warns if assets already exist).
    /// </summary>
    public static class GameUiPrefabGenerator
    {
        public const string UiFolder = "Assets/content/ui";
        public const string WidgetsFolder = "Assets/content/ui/widgets";
        public const string GameUiPrefabPath = UiFolder + "/GameUI.prefab";
        public const string CollectorCardPath = WidgetsFolder + "/CollectorCard.prefab";
        public const string UpgradeRowPath = WidgetsFolder + "/UpgradeRow.prefab";
        public const string BoosterRowPath = WidgetsFolder + "/BoosterRow.prefab";
        public const string PurchaseTagPath = WidgetsFolder + "/PurchaseTag.prefab";

        [MenuItem("Master Bidder/Generate UI Prefabs", priority = 20)]
        public static void GenerateMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameUiPrefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Generate UI Prefabs",
                        "UI prefabs already exist. Re-generate and overwrite with the default layout?\n\n" +
                        "Your Inspector customizations (fonts, spacing, anchors, colors) will be lost.\n" +
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
                    $"• {BoosterRowPath}\n" +
                    $"• {PurchaseTagPath}\n\n" +
                    "Assigned on GameUiShell in the open scene (if present).\n\n" +
                    "Edit layout/fonts/spacing on the prefabs — runtime will keep your changes.\n" +
                    "Tip: run Master Bidder → Import UI Atlas Sprites first for 9-slice borders.",
                    "OK");
            }
        }

        /// <summary>Batchmode entry: -executeMethod MasterBidder.Editor.GameUiPrefabGenerator.GenerateBatch</summary>
        public static void GenerateBatch()
        {
            if (!Generate())
                EditorApplication.Exit(1);
            else
                EditorApplication.Exit(0);
        }

        public static bool EnsureTmpEssentials()
        {
            if (TMP_Settings.defaultFontAsset != null ||
                Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") != null)
                return true;

            string package = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..",
                "Library/PackageCache/com.unity.textmeshpro@3.0.6/Package Resources/TMP Essential Resources.unitypackage"));
            if (!File.Exists(package))
            {
                Debug.LogError("[GameUiPrefabGenerator] TMP Essential Resources package not found: " + package);
                return false;
            }

            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            return TMP_Settings.defaultFontAsset != null ||
                   Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") != null;
        }

        public static bool Generate()
        {
            if (!EnsureTmpEssentials())
            {
                Debug.LogError("[GameUiPrefabGenerator] TextMesh Pro essentials missing. Open Window → TextMeshPro → Import TMP Essential Resources.");
                return false;
            }

            if (!GameUiFontSetup.CreateAll())
            {
                Debug.LogError("[GameUiPrefabGenerator] UI font assets missing/failed. Run Master Bidder → Create UI Font Assets.");
                return false;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "content/ui/widgets"));
            AssetDatabase.Refresh();

            var collectorCard = GameUiHierarchyFactory.BuildCollectorCard();
            var upgradeRow = GameUiHierarchyFactory.BuildUpgradeRow();
            var boosterRow = GameUiHierarchyFactory.BuildBoosterRow();
            var purchaseTag = GameUiHierarchyFactory.BuildPurchaseTag();
            var gameUi = GameUiHierarchyFactory.BuildGameUi();

            PrefabUtility.SaveAsPrefabAsset(collectorCard, CollectorCardPath);
            PrefabUtility.SaveAsPrefabAsset(upgradeRow, UpgradeRowPath);
            PrefabUtility.SaveAsPrefabAsset(boosterRow, BoosterRowPath);
            PrefabUtility.SaveAsPrefabAsset(purchaseTag, PurchaseTagPath);
            PrefabUtility.SaveAsPrefabAsset(gameUi, GameUiPrefabPath);

            Object.DestroyImmediate(collectorCard);
            Object.DestroyImmediate(upgradeRow);
            Object.DestroyImmediate(boosterRow);
            Object.DestroyImmediate(purchaseTag);
            Object.DestroyImmediate(gameUi);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignToOpenScene();
            Debug.Log("[GameUiPrefabGenerator] UI prefabs ready under " + UiFolder +
                      " — edit them in the Prefab Editor; GameUiShell will not overwrite layout/fonts.");
            return true;
        }

        static void AssignToOpenScene()
        {
            var gameUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameUiPrefabPath);
            var card = AssetDatabase.LoadAssetAtPath<GameObject>(CollectorCardPath);
            var upgrade = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradeRowPath);
            var booster = AssetDatabase.LoadAssetAtPath<GameObject>(BoosterRowPath);
            var purchaseTag = AssetDatabase.LoadAssetAtPath<GameObject>(PurchaseTagPath);

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
            var purchaseProp = so.FindProperty("purchaseTagPrefab");
            if (purchaseProp != null)
                purchaseProp.objectReferenceValue = purchaseTag;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shell);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
