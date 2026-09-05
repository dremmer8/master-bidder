using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Configures atlas-extracted UI PNGs as Sprites with 9-slice (adaptable) borders.
    /// Menu: Master Bidder → Import UI Atlas Sprites
    /// </summary>
    public static class UiSpriteImporter
    {
        public const string SlicedFolder = "Assets/content/ui/sprites/sliced";
        public const string ResourcesFolder = "Assets/content/ui/Resources/UiSprites";
        public const string MetaIconsFolder = "Assets/content/ui/Resources/MetaIcons";
        public const string AtlasPath = "Assets/content/ui/sprites/UiAtlas.png";

        // Unity spriteBorder = (left, bottom, right, top)
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "panel", new Vector4(22, 22, 22, 30) },
            { "panel_body", new Vector4(22, 22, 22, 30) },
            { "panel_ornament", new Vector4(0, 0, 0, 0) },
            { "panel_card", new Vector4(18, 18, 18, 18) },
            { "btn_primary", new Vector4(18, 18, 18, 18) },
            { "btn_danger", new Vector4(18, 18, 18, 18) },
            { "btn_secondary", new Vector4(18, 18, 18, 18) },
            { "btn_secondary_alt", new Vector4(18, 18, 18, 18) },
            { "btn_secondary_sm", new Vector4(16, 16, 16, 16) },
            { "btn_secondary_sm2", new Vector4(16, 16, 16, 16) },
            { "btn_skip", new Vector4(16, 16, 16, 16) },
            { "btn_calendar", new Vector4(16, 16, 16, 16) },
            { "bar_currency", new Vector4(56, 16, 18, 16) },
            { "bar_day", new Vector4(56, 16, 18, 16) },
            { "banner", new Vector4(42, 18, 42, 18) },
            { "divider", new Vector4(40, 4, 40, 4) },
            { "toast_success", new Vector4(52, 18, 44, 18) },
            { "toast_info", new Vector4(52, 18, 44, 18) },
            { "toast_warn", new Vector4(52, 18, 44, 18) },
            { "toast_error", new Vector4(52, 18, 44, 18) },
            { "icon_heart", new Vector4(12, 12, 12, 12) },
            { "icon_share", new Vector4(12, 12, 12, 12) },
            { "icon_info", new Vector4(12, 12, 12, 12) },
            { "icon_settings", new Vector4(12, 12, 12, 12) },
            { "icon_volume", new Vector4(12, 12, 12, 12) },
            { "icon_mute", new Vector4(12, 12, 12, 12) },
            { "icon_close", new Vector4(12, 12, 12, 12) },
            { "swatch_cream", new Vector4(8, 8, 8, 8) },
            { "swatch_taupe", new Vector4(8, 8, 8, 8) },
            { "swatch_dark", new Vector4(8, 8, 8, 8) },
            { "swatch_gold", new Vector4(8, 8, 8, 8) },
            { "swatch_black", new Vector4(8, 8, 8, 8) },
        };

        [MenuItem("Master Bidder/Import UI Atlas Sprites", priority = 18)]
        public static void ImportMenu()
        {
            int n = ImportAll();
            EditorUtility.DisplayDialog(
                "UI Atlas Sprites",
                $"Configured {n} sprites with 9-slice borders under:\n• {SlicedFolder}\n• {ResourcesFolder}\n• {MetaIconsFolder}\n\n" +
                "Run Master Bidder → Generate UI Prefabs to rebuild UI with these sprites.",
                "OK");
        }

        [MenuItem("Master Bidder/Import UI Sprites And Generate Prefabs", priority = 19)]
        public static void ImportAndGenerate()
        {
            ImportAll();
            GameUiPrefabGenerator.Generate();
            EditorUtility.DisplayDialog(
                "UI Atlas",
                "Sprites imported with adaptable (9-slice) borders and UI prefabs regenerated.",
                "OK");
        }

        /// <summary>Batchmode: -executeMethod MasterBidder.Editor.UiSpriteImporter.ImportBatch</summary>
        public static void ImportBatch()
        {
            ImportAll();
            if (!GameUiPrefabGenerator.Generate())
                EditorApplication.Exit(1);
        }

        public static int ImportAll()
        {
            int count = 0;
            count += ConfigureFolder(SlicedFolder);
            count += ConfigureFolder(ResourcesFolder);
            count += ConfigureFolder(MetaIconsFolder);
            ConfigureSingle(AtlasPath, SpriteImportMode.Single, Vector4.zero);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UiSpriteImporter] Configured {count} UI sprites with 9-slice borders.");
            return count;
        }

        static int ConfigureFolder(string folder)
        {
            if (!Directory.Exists(ToAbsolute(folder))) return 0;
            int n = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png")) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                Vector4 border = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
                if (ConfigureSingle(path, SpriteImportMode.Single, border))
                    n++;
            }
            return n;
        }

        static bool ConfigureSingle(string assetPath, SpriteImportMode mode, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != mode)
            {
                importer.spriteImportMode = mode;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                dirty = true;
            }
            if (importer.alphaIsTransparency != true)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                dirty = true;
            }
            if (importer.spritePixelsPerUnit != 100f)
            {
                importer.spritePixelsPerUnit = 100f;
                dirty = true;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                dirty = true;
            }

            var platform = importer.GetDefaultPlatformTextureSettings();
            if (platform.maxTextureSize < 2048)
            {
                platform.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(platform);
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
            return true;
        }

        static string ToAbsolute(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, assetPath);
        }
    }
}
