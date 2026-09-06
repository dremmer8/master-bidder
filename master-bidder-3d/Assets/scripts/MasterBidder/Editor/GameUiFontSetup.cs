using System.IO;
using MasterBidder.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Builds TMP SDF font assets (Playfair Display + Manrope) with Cyrillic coverage.
    /// Menu: Master Bidder → Create UI Font Assets
    ///
    /// Unity 2023 rejects TMP 3.0.6 CreateFontAsset's Texture2D(0,0) atlas, and many
    /// TMP_FontAsset setters are internal — this path builds assets via SerializedObject.
    /// </summary>
    public static class GameUiFontSetup
    {
        public const string SourceRoot = "Assets/content/ui/fonts";
        public const string OutputFolder = "Assets/content/ui/Resources/Fonts";

        public const string PlayfairRegularTtf = SourceRoot + "/PlayfairDisplay/PlayfairDisplay-Regular.ttf";
        public const string PlayfairBoldTtf = SourceRoot + "/PlayfairDisplay/PlayfairDisplay-Bold.ttf";
        public const string PlayfairItalicTtf = SourceRoot + "/PlayfairDisplay/PlayfairDisplay-Italic.ttf";
        public const string PlayfairBoldItalicTtf = SourceRoot + "/PlayfairDisplay/PlayfairDisplay-BoldItalic.ttf";
        public const string ManropeRegularTtf = SourceRoot + "/Manrope/Manrope-Regular.ttf";
        public const string ManropeMediumTtf = SourceRoot + "/Manrope/Manrope-Medium.ttf";
        public const string ManropeSemiBoldTtf = SourceRoot + "/Manrope/Manrope-SemiBold.ttf";
        public const string ManropeBoldTtf = SourceRoot + "/Manrope/Manrope-Bold.ttf";

        public const string PlayfairRegularAsset = OutputFolder + "/PlayfairDisplay SDF.asset";
        public const string PlayfairBoldAsset = OutputFolder + "/PlayfairDisplay Bold SDF.asset";
        public const string PlayfairItalicAsset = OutputFolder + "/PlayfairDisplay Italic SDF.asset";
        public const string ManropeRegularAsset = OutputFolder + "/Manrope SDF.asset";
        public const string ManropeBoldAsset = OutputFolder + "/Manrope Bold SDF.asset";

        const int SamplingPointSize = 90;
        const int AtlasPadding = 9;
        const int AtlasSize = 1024;

        const string SeedCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "№₽«»—–…•·×÷±°";

        [MenuItem("Master Bidder/Create UI Font Assets", priority = 21)]
        public static void CreateMenu()
        {
            if (!CreateAll())
            {
                EditorUtility.DisplayDialog("UI Fonts", "Failed to create font assets. See Console.", "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "UI Fonts",
                "Created Playfair Display + Manrope TMP assets under:\n" + OutputFolder +
                "\n\nRe-run Master Bidder → Generate UI Prefabs to bake fonts into prefabs.",
                "OK");
        }

        /// <summary>Batchmode: -executeMethod MasterBidder.Editor.GameUiFontSetup.CreateBatch</summary>
        public static void CreateBatch()
        {
            EditorApplication.Exit(CreateAll() ? 0 : 1);
        }

        public static bool CreateAll()
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "content/ui/fonts/PlayfairDisplay/PlayfairDisplay-Regular.ttf")) ||
                !File.Exists(Path.Combine(Application.dataPath, "content/ui/fonts/Manrope/Manrope-Regular.ttf")))
            {
                Debug.LogError("[GameUiFontSetup] Source TTFs missing under " + SourceRoot);
                return false;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "content/ui/Resources/Fonts"));
            AssetDatabase.Refresh();

            var playfairRegular = CreateDynamicAsset(PlayfairRegularTtf, PlayfairRegularAsset, "PlayfairDisplay SDF");
            var playfairBold = CreateDynamicAsset(PlayfairBoldTtf, PlayfairBoldAsset, "PlayfairDisplay Bold SDF");
            var playfairItalic = CreateDynamicAsset(PlayfairItalicTtf, PlayfairItalicAsset, "PlayfairDisplay Italic SDF");
            var manropeRegular = CreateDynamicAsset(ManropeRegularTtf, ManropeRegularAsset, "Manrope SDF");
            var manropeBold = CreateDynamicAsset(ManropeBoldTtf, ManropeBoldAsset, "Manrope Bold SDF");

            AssetDatabase.LoadAssetAtPath<Font>(ManropeMediumTtf);
            AssetDatabase.LoadAssetAtPath<Font>(ManropeSemiBoldTtf);
            AssetDatabase.LoadAssetAtPath<Font>(PlayfairBoldItalicTtf);

            if (playfairRegular == null || playfairBold == null || playfairItalic == null ||
                manropeRegular == null || manropeBold == null)
            {
                Debug.LogError("[GameUiFontSetup] One or more TMP font assets failed to create.");
                return false;
            }

            WireWeightTable(playfairRegular, playfairBold, playfairItalic);
            WireWeightTable(manropeRegular, manropeBold, null);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameUiStyle.InvalidateFontCache();
            Debug.Log("[GameUiFontSetup] UI fonts ready: Playfair Display + Manrope (Cyrillic).");
            return true;
        }

        static TMP_FontAsset CreateDynamicAsset(string fontPath, string assetPath, string assetName)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError("[GameUiFontSetup] Missing Font at " + fontPath);
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            var fontAsset = BuildDynamicFontAsset(font, assetName);
            if (fontAsset == null)
            {
                Debug.LogError("[GameUiFontSetup] CreateFontAsset failed for " + fontPath);
                return null;
            }

            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            fontAsset.TryAddCharacters(SeedCharacters, out var missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning("[GameUiFontSetup] Missing glyphs in " + assetName + ": " + Truncate(missing, 120));

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        static TMP_FontAsset BuildDynamicFontAsset(Font font, string assetName)
        {
            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(font, SamplingPointSize) != FontEngineError.Success)
            {
                Debug.LogWarning(
                    "[GameUiFontSetup] Unable to load font face for [" + font.name +
                    "]. Enable Include Font Data in Font Import Settings.", font);
                return null;
            }

            ShaderUtilities.GetShaderPropertyIDs();
            var sdfShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (sdfShader == null)
            {
                Debug.LogError("[GameUiFontSetup] TMP Distance Field shader missing. Import TMP Essential Resources.");
                return null;
            }

            var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.Alpha8, false);
            ClearAtlas(texture);

            var material = new Material(sdfShader);
            material.SetTexture(ShaderUtilities.ID_MainTex, texture);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
            material.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);

            var fontAsset = ScriptableObject.CreateInstance<TMP_FontAsset>();
            fontAsset.name = assetName;
            fontAsset.faceInfo = FontEngine.GetFaceInfo();
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            fontAsset.atlasTextures = new[] { texture };
            fontAsset.material = material;
            material.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);

            // Internal setters — write via SerializedObject (TMP 3.0.6).
            var so = new SerializedObject(fontAsset);
            so.FindProperty("m_Version").stringValue = "1.1.0";
            so.FindProperty("m_SourceFontFile").objectReferenceValue = font;
            so.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = font;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font, out string guid, out long _))
                so.FindProperty("m_SourceFontFileGUID").stringValue = guid;
            so.FindProperty("m_AtlasWidth").intValue = AtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
            so.FindProperty("m_AtlasPadding").intValue = AtlasPadding;
            so.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDFAA;
            // Avoid TMP 3.0.6 clearing atlas to 0×0 on player build (broken on Unity 2023).
            var clearOnBuild = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearOnBuild != null) clearOnBuild.boolValue = false;

            var freeRects = so.FindProperty("m_FreeGlyphRects");
            freeRects.ClearArray();
            freeRects.arraySize = 1;
            SetGlyphRect(freeRects.GetArrayElementAtIndex(0), 0, 0, AtlasSize - 1, AtlasSize - 1);

            var usedRects = so.FindProperty("m_UsedGlyphRects");
            usedRects.ClearArray();
            usedRects.arraySize = 0;

            so.ApplyModifiedPropertiesWithoutUndo();
            fontAsset.ReadFontAssetDefinition();
            return fontAsset;
        }

        static void SetGlyphRect(SerializedProperty prop, int x, int y, int w, int h)
        {
            // UnityEngine.TextCore.GlyphRect serialized fields
            prop.FindPropertyRelative("m_X").intValue = x;
            prop.FindPropertyRelative("m_Y").intValue = y;
            prop.FindPropertyRelative("m_Width").intValue = w;
            prop.FindPropertyRelative("m_Height").intValue = h;
        }

        static void ClearAtlas(Texture2D texture)
        {
            var clear = new Color32[texture.width * texture.height];
            texture.SetPixels32(clear);
            texture.Apply(false, false);
        }

        static void WireWeightTable(TMP_FontAsset regular, TMP_FontAsset bold, TMP_FontAsset italic)
        {
            if (regular == null) return;

            var weights = regular.fontWeightTable;
            if (weights == null || weights.Length < 10)
                return;

            var regularSlot = weights[4];
            regularSlot.regularTypeface = regular;
            regularSlot.italicTypeface = italic != null ? italic : regular;
            weights[4] = regularSlot;

            var boldSlot = weights[7];
            boldSlot.regularTypeface = bold != null ? bold : regular;
            boldSlot.italicTypeface = italic != null ? italic : (bold != null ? bold : regular);
            weights[7] = boldSlot;

            EditorUtility.SetDirty(regular);
        }

        static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "…";
        }
    }
}
