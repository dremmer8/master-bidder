using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MasterBidder.Content;
using MasterBidder.Flow;
using NineSlice3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Imports MVP artwork/collector JSON into ScriptableObjects and wires AppFlow.
    /// Menu: Master Bidder → Sync MVP Content / Setup AppFlow In Open Scene
    /// </summary>
    public static class MvpContentImporter
    {
        const string ArtworksJsonPath = "Assets/content/paintings/mvp_artworks.json";
        const string CollectorsJsonPath = "Assets/content/collectors/mvp_collectors.json";
        const string PaintingsFolder = "Assets/content/paintings/configs";
        const string CollectorsFolder = "Assets/content/collectors";
        const string CatalogPath = "Assets/content/GameCatalog.asset";

        [Serializable]
        class ArtworkDto
        {
            public string id;
            public string titleRu;
            public string artistRu;
            public string year;
            public string periodRu;
            public string genreRu;
            public string factRu;
            public string rarity;
            public int basePrice;
        }

        [Serializable]
        class ArtworkList
        {
            public ArtworkDto[] items;
        }

        [Serializable]
        class CollectorDto
        {
            public string id;
            public string nameRu;
            public string taglineRu;
            public float personalModifier;
            public int baseBudget;
            public string orderGenre;
            public string orderPeriod;
            public string orderArtist;
        }

        [Serializable]
        class CollectorList
        {
            public CollectorDto[] items;
        }

        [MenuItem("Master Bidder/Sync MVP Content", priority = 10)]
        public static void SyncMvpContent()
        {
            var artworks = LoadArtworks();
            var collectors = LoadCollectors();
            if (artworks == null || collectors == null) return;

            var artworkById = new Dictionary<string, ArtworkDto>();
            foreach (var a in artworks)
            {
                if (a != null && !string.IsNullOrEmpty(a.id))
                    artworkById[a.id] = a;
            }

            int updated = 0;
            int unmatched = 0;
            var matchedPaintings = new List<PaintingData>();
            var unmatchedIds = new List<string>(artworkById.Keys);

            EnsureMissingPaintings(artworkById);

            string[] guids = AssetDatabase.FindAssets("t:PaintingData", new[] { PaintingsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var painting = AssetDatabase.LoadAssetAtPath<PaintingData>(path);
                if (painting == null) continue;

                string id = ResolveArtworkId(painting, artworkById);
                if (string.IsNullOrEmpty(id) || !artworkById.TryGetValue(id, out var dto))
                {
                    unmatched++;
                    Debug.LogWarning($"[MvpContentImporter] No MVP row for painting asset '{painting.name}' (id guess: '{id}').");
                    continue;
                }

                ApplyArtwork(painting, dto);
                EditorUtility.SetDirty(painting);
                matchedPaintings.Add(painting);
                unmatchedIds.Remove(id);
                updated++;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "content/collectors/portraits"));
            ImportCollectorPortraits();

            var collectorAssets = new List<CollectorData>();
            foreach (var dto in collectors)
            {
                string assetPath = $"{CollectorsFolder}/Collector_{ToPascal(dto.id)}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<CollectorData>(assetPath);
                if (existing == null)
                {
                    existing = ScriptableObject.CreateInstance<CollectorData>();
                    AssetDatabase.CreateAsset(existing, assetPath);
                }

                existing.collectorId = dto.id;
                existing.nameRu = dto.nameRu;
                existing.taglineRu = dto.taglineRu;
                existing.personalModifier = dto.personalModifier;
                existing.baseBudget = dto.baseBudget;
                existing.orderGenre = dto.orderGenre;
                existing.orderPeriod = dto.orderPeriod;
                existing.orderArtist = dto.orderArtist;
                existing.portrait = LoadPortraitSprite(dto.id);
                EditorUtility.SetDirty(existing);
                collectorAssets.Add(existing);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.paintings = matchedPaintings;
            catalog.collectors = collectorAssets;
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine($"[MvpContentImporter] Updated {updated} paintings, {collectorAssets.Count} collectors.");
            sb.AppendLine($"Catalog: {catalog.paintings.Count} paintings, {catalog.collectors.Count} collectors → {CatalogPath}");
            if (unmatchedIds.Count > 0)
            {
                sb.AppendLine($"MVP ids with no Unity PaintingData ({unmatchedIds.Count}):");
                foreach (var id in unmatchedIds)
                    sb.AppendLine("  - " + id);
            }
            if (unmatched > 0)
                sb.AppendLine($"Unity paintings with no MVP row: {unmatched}");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("MVP Content Sync", sb.ToString(), "OK");
        }

        [MenuItem("Master Bidder/Fix Collector Portrait Sprites", priority = 12)]
        public static void FixCollectorPortraitSprites()
        {
            ImportCollectorPortraits();
            var collectors = LoadCollectors();
            if (collectors != null)
            {
                foreach (var dto in collectors)
                {
                    string assetPath = $"{CollectorsFolder}/Collector_{ToPascal(dto.id)}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<CollectorData>(assetPath);
                    if (existing == null) continue;
                    existing.portrait = LoadPortraitSprite(dto.id);
                    EditorUtility.SetDirty(existing);
                }
                AssetDatabase.SaveAssets();
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Collector Portraits",
                "Configured portrait PNGs as Sprites and rebound CollectorData assets.\nIf images are still blank, select a portrait PNG and confirm Texture Type = Sprite (2D and UI).",
                "OK");
        }

        [MenuItem("Master Bidder/Setup AppFlow In Open Scene", priority = 11)]
        public static void SetupAppFlowInScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup AppFlow",
                    "GameCatalog not found. Run Master Bidder → Sync MVP Content first.",
                    "OK");
                return;
            }

            var existing = UnityEngine.Object.FindObjectOfType<AppFlow>();
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("GameFlow");
                go.AddComponent<AppFlow>();
            }

            var flow = go.GetComponent<AppFlow>();
            var so = new SerializedObject(flow);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            var presentation = UnityEngine.Object.FindObjectOfType<MasterBidder.Presentation.PresentationOperator>();
            if (presentation != null)
                so.FindProperty("presentation").objectReferenceValue = presentation;
            so.FindProperty("disablePresentationDemoHotkeys").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            EditorUtility.DisplayDialog(
                "Setup AppFlow",
                "GameFlow object is in the open scene with GameCatalog assigned.\nSave the scene (Ctrl+S).",
                "OK");
        }

        static ArtworkDto[] LoadArtworks()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ArtworksJsonPath);
            if (ta == null)
            {
                EditorUtility.DisplayDialog("Sync MVP Content", $"Missing {ArtworksJsonPath}", "OK");
                return null;
            }
            string wrapped = "{\"items\":" + ta.text + "}";
            var list = JsonUtility.FromJson<ArtworkList>(wrapped);
            return list?.items;
        }

        static CollectorDto[] LoadCollectors()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(CollectorsJsonPath);
            if (ta == null)
            {
                EditorUtility.DisplayDialog("Sync MVP Content", $"Missing {CollectorsJsonPath}", "OK");
                return null;
            }
            string wrapped = "{\"items\":" + ta.text + "}";
            var list = JsonUtility.FromJson<CollectorList>(wrapped);
            return list?.items;
        }

        static void ApplyArtwork(PaintingData painting, ArtworkDto dto)
        {
            painting.artworkId = dto.id;
            painting.titleRu = dto.titleRu;
            painting.artistRu = dto.artistRu;
            painting.year = dto.year;
            painting.periodRu = dto.periodRu;
            painting.genreRu = dto.genreRu;
            painting.factRu = dto.factRu;
            painting.rarity = dto.rarity;
            painting.basePrice = dto.basePrice;
            if (string.IsNullOrEmpty(painting.paintingTitle) || painting.paintingTitle == "Untitled")
                painting.paintingTitle = ToTitleFromId(dto.id);
        }

        static string ResolveArtworkId(PaintingData painting, Dictionary<string, ArtworkDto> artworkById)
        {
            string guessed = GuessIdFromAssetName(painting.name);
            if (!string.IsNullOrEmpty(guessed) && artworkById.ContainsKey(guessed))
                return guessed;
            if (!string.IsNullOrEmpty(painting.artworkId) && artworkById.ContainsKey(painting.artworkId))
                return painting.artworkId;
            return guessed;
        }

        static void EnsureMissingPaintings(Dictionary<string, ArtworkDto> artworkById)
        {
            // Create The Raft of the Medusa if MVP has it but Unity does not.
            const string raftId = "the-raft-of-the-medusa";
            if (!artworkById.ContainsKey(raftId)) return;

            string assetPath = $"{PaintingsFolder}/Painting_TheRaftOfTheMedusa.asset";
            if (AssetDatabase.LoadAssetAtPath<PaintingData>(assetPath) != null) return;

            string mvpWebp = Path.GetFullPath(Path.Combine(Application.dataPath, "../../mvp/assets/art/the-raft-of-the-medusa.webp"));
            string unityTex = "Assets/content/paintings/tex/the_raft_of_the_medusa.webp";
            string unityTexFull = Path.Combine(Application.dataPath, "content/paintings/tex/the_raft_of_the_medusa.webp");
            if (File.Exists(mvpWebp) && !File.Exists(unityTexFull))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(unityTexFull));
                File.Copy(mvpWebp, unityTexFull, true);
                AssetDatabase.ImportAsset(unityTex);
            }

            var painting = ScriptableObject.CreateInstance<PaintingData>();
            painting.paintingTitle = "The Raft of the Medusa";
            painting.artist = "Théodore Géricault";
            painting.width = 716f;
            painting.height = 491f;
            painting.depth = 5f;
            painting.unit = MeasurementUnit.Centimeters;
            ApplyArtwork(painting, artworkById[raftId]);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(unityTex);
            if (tex != null) painting.albedoTexture = tex;

            AssetDatabase.CreateAsset(painting, assetPath);
            EditorUtility.SetDirty(painting);
            Debug.Log("[MvpContentImporter] Created missing Painting_TheRaftOfTheMedusa.");
        }

        static void ImportCollectorPortraits()
        {
            string srcDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../mvp/assets/collectors"));
            string dstDir = Path.Combine(Application.dataPath, "content/collectors/portraits");
            Directory.CreateDirectory(dstDir);
            if (!Directory.Exists(srcDir)) return;

            // Prefer PNG copies already in the Unity portraits folder; convert note:
            // WebP is unreliable as uGUI sprites — Sync expects .png next to collectors.
            foreach (string file in Directory.GetFiles(srcDir, "*.webp"))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                string pngPath = Path.Combine(dstDir, id + ".png");
                string assetPath = $"Assets/content/collectors/portraits/{id}.png";

                // If PNG missing, copy webp as fallback only when png truly absent
                // (conversion is done offline to PNG — do not re-copy webp over png).
                if (!File.Exists(pngPath))
                {
                    Debug.LogWarning(
                        $"[MvpContentImporter] Missing portrait PNG for '{id}'. " +
                        $"Expected {assetPath}. Run portrait conversion or place a PNG there.");
                    continue;
                }

                AssetDatabase.ImportAsset(assetPath);
                ConfigurePortraitImporter(assetPath);
            }

            // Also configure any PNGs already present
            foreach (string file in Directory.GetFiles(dstDir, "*.png"))
            {
                string assetPath = "Assets/content/collectors/portraits/" + Path.GetFileName(file);
                ConfigurePortraitImporter(assetPath);
            }
        }

        static void ConfigurePortraitImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }

        static Sprite LoadPortraitSprite(string collectorId)
        {
            string png = $"Assets/content/collectors/portraits/{collectorId}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            if (sprite != null) return sprite;
            // Legacy fallback
            string webp = $"Assets/content/collectors/portraits/{collectorId}.webp";
            return AssetDatabase.LoadAssetAtPath<Sprite>(webp);
        }

        static string GuessIdFromAssetName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            string name = assetName;
            if (name.StartsWith("Painting_", StringComparison.Ordinal))
                name = name.Substring("Painting_".Length);
            return PascalToKebab(name);
        }

        static string PascalToKebab(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return pascal;
            var sb = new StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (i > 0)
                {
                    bool upper = char.IsUpper(c);
                    bool digit = char.IsDigit(c);
                    bool prevDigit = char.IsDigit(pascal[i - 1]);
                    bool prevLetter = char.IsLetter(pascal[i - 1]);
                    if (upper || (digit && !prevDigit && prevLetter))
                        sb.Append('-');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        static string ToPascal(string kebab)
        {
            if (string.IsNullOrEmpty(kebab)) return kebab;
            var parts = kebab.Split('-');
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }

        static string ToTitleFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            return Regex.Replace(id.Replace('-', ' '), @"\b[a-z]", m => m.Value.ToUpperInvariant());
        }
    }
}
