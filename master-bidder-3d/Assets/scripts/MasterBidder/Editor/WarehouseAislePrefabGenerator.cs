using System.IO;
using UnityEditor;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Builds a one-point-perspective warehouse aisle from box primitives.
    /// Menu: Master Bidder → Generate Warehouse Aisle Prefab
    ///
    /// Place the prefab in the scene, point Main Camera down +Z from camera_anchor,
    /// and use a transparent UI screen root so the aisle shows through.
    /// </summary>
    public static class WarehouseAislePrefabGenerator
    {
        public const string PrefabsFolder = "Assets/content/scene/prefabs";
        public const string PrefabPath = PrefabsFolder + "/warehouse_aisle.prefab";

        const string WoodDarkPath = "Assets/content/scene/mat/wood_dark.mat";
        const string WallPlasterPath = "Assets/content/scene/mat/wall_plaster.mat";
        const string FloorStagePath = "Assets/content/scene/mat/floor_stage.mat";

        // Corridor extents (meters-ish, matches auction hall scale).
        const float Length = 14f;
        const float Width = 5.2f;
        const float Height = 4.2f;
        const float WallThickness = 0.18f;
        const float ShelfDepth = 1.05f;
        const int BayCount = 5;
        const int ShelfLevels = 3;

        [MenuItem("Master Bidder/Generate Warehouse Aisle Prefab", priority = 40)]
        public static void GenerateMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Generate Warehouse Aisle",
                        "warehouse_aisle.prefab already exists. Overwrite?",
                        "Overwrite",
                        "Cancel"))
                    return;
            }

            if (!Generate())
                return;

            EditorUtility.DisplayDialog(
                "Warehouse Aisle",
                $"Created/updated:\n• {PrefabPath}\n\n" +
                "Drop into the scene, align Main Camera with camera_anchor (look +Z),\n" +
                "and use a transparent UI screen so the aisle shows through.",
                "OK");
        }

        public static bool Generate()
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(WoodDarkPath);
            var plaster = AssetDatabase.LoadAssetAtPath<Material>(WallPlasterPath);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>(FloorStagePath);
            if (wood == null || plaster == null || floorMat == null)
            {
                Debug.LogError(
                    "[WarehouseAisle] Missing materials. Expected:\n" +
                    WoodDarkPath + "\n" + WallPlasterPath + "\n" + FloorStagePath);
                return false;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "content/scene/prefabs"));
            AssetDatabase.Refresh();

            var root = new GameObject("warehouse_aisle");
            try
            {
                BuildAisle(root.transform, wood, plaster, floorMat);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
                Selection.activeObject = prefab;
            Debug.Log($"[WarehouseAisle] Saved {PrefabPath}");
            return true;
        }

        static void BuildAisle(Transform root, Material wood, Material plaster, Material floorMat)
        {
            // Floor / ceiling / end walls enclose the corridor.
            Box("floor", root, new Vector3(0f, -WallThickness * 0.5f, Length * 0.5f),
                new Vector3(Width, WallThickness, Length), floorMat);
            Box("ceiling", root, new Vector3(0f, Height + WallThickness * 0.5f, Length * 0.5f),
                new Vector3(Width, WallThickness, Length), plaster);
            Box("back_wall", root, new Vector3(0f, Height * 0.5f, Length + WallThickness * 0.5f),
                new Vector3(Width, Height + WallThickness * 2f, WallThickness), plaster);
            Box("near_lintel", root, new Vector3(0f, Height * 0.85f, -WallThickness * 0.5f),
                new Vector3(Width, Height * 0.3f, WallThickness), plaster);

            var shelvesL = new GameObject("shelves_L").transform;
            shelvesL.SetParent(root, false);
            var shelvesR = new GameObject("shelves_R").transform;
            shelvesR.SetParent(root, false);

            // Racks flush to outer walls, depth = ShelfDepth toward aisle.
            float rackCenterX = Width * 0.5f - WallThickness - ShelfDepth * 0.5f;

            BuildRack(shelvesL, -rackCenterX, wood, plaster, facingRight: true);
            BuildRack(shelvesR, rackCenterX, wood, plaster, facingRight: false);

            // Soft fill lights along the aisle centerline (warm, like auction hall spots).
            for (int i = 0; i < 3; i++)
            {
                float z = Length * (0.25f + i * 0.25f);
                var lightGo = new GameObject($"aisle_light_{i + 1}");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, Height - 0.15f, z);
                lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 9f;
                light.spotAngle = 70f;
                light.intensity = 1.35f;
                light.color = new Color(1f, 0.92f, 0.78f);
            }

            // Camera hint: stand at near end, look down the aisle (+Z).
            var cam = new GameObject("camera_anchor");
            cam.transform.SetParent(root, false);
            cam.transform.localPosition = new Vector3(0f, 1.55f, 0.35f);
            cam.transform.localRotation = Quaternion.identity; // forward = +Z
        }

        static void BuildRack(Transform parent, float centerX, Material wood, Material plaster, bool facingRight)
        {
            float bayLen = Length / BayCount;
            float uprightW = 0.12f;
            float shelfThick = 0.07f;
            float postH = Height - 0.05f;

            // Outer back panel of the rack (against wall).
            float backX = facingRight
                ? centerX - ShelfDepth * 0.5f + WallThickness * 0.5f
                : centerX + ShelfDepth * 0.5f - WallThickness * 0.5f;
            Box("back_panel", parent, new Vector3(backX, postH * 0.5f, Length * 0.5f),
                new Vector3(WallThickness, postH, Length), plaster);

            for (int bay = 0; bay <= BayCount; bay++)
            {
                float z = bay * bayLen;
                Box($"upright_{bay}", parent, new Vector3(centerX, postH * 0.5f, z),
                    new Vector3(ShelfDepth, postH, uprightW), wood);
            }

            // Top beam along Z.
            Box("top_beam", parent, new Vector3(centerX, postH - shelfThick * 0.5f, Length * 0.5f),
                new Vector3(ShelfDepth, shelfThick, Length), wood);

            for (int level = 0; level < ShelfLevels; level++)
            {
                float y = 0.55f + level * ((postH - 0.7f) / (ShelfLevels - 1));
                Box($"shelf_{level}", parent, new Vector3(centerX, y, Length * 0.5f),
                    new Vector3(ShelfDepth, shelfThick, Length - uprightW), wood);

                // Painting / crate placeholders facing the aisle.
                for (int bay = 0; bay < BayCount; bay++)
                {
                    float z = (bay + 0.5f) * bayLen;
                    float faceSign = facingRight ? 1f : -1f;
                    float artX = centerX + faceSign * (ShelfDepth * 0.15f);

                    // Thin framed slab = stored painting.
                    var art = Box($"painting_{level}_{bay}", parent,
                        new Vector3(artX, y + 0.38f, z),
                        new Vector3(0.06f, 0.72f, 0.55f), plaster);
                    // Slight inward lean toward aisle for readable silhouette.
                    art.transform.localRotation = Quaternion.Euler(0f, facingRight ? -8f : 8f, 0f);

                    // Small crate under lowest shelf only.
                    if (level == 0 && bay % 2 == 0)
                    {
                        float crateX = centerX + faceSign * 0.05f;
                        Box($"crate_{bay}", parent,
                            new Vector3(crateX, 0.28f, z + bayLen * 0.22f),
                            new Vector3(0.55f, 0.45f, 0.45f), wood);
                    }
                }
            }
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;

            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic
                                                       | StaticEditorFlags.ContributeGI
                                                       | StaticEditorFlags.OccluderStatic
                                                       | StaticEditorFlags.OccludeeStatic
                                                       | StaticEditorFlags.NavigationStatic
                                                       | StaticEditorFlags.OffMeshLinkGeneration
                                                       | StaticEditorFlags.ReflectionProbeStatic);

            return go;
        }
    }
}
