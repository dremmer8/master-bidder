using System.IO;
using UnityEditor;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Banquet table + wall bulletin board from primitives (close-up board view).
    /// Menu: Master Bidder → Generate Banquet Board Prefab
    ///
    /// Align Main Camera with camera_anchor (look +Z, nearly flush with the board).
    /// Use a transparent UI screen root so the room shows through.
    /// </summary>
    public static class BanquetBoardPrefabGenerator
    {
        public const string PrefabsFolder = "Assets/content/scene/prefabs";
        public const string PrefabPath = PrefabsFolder + "/banquet_board_room.prefab";

        const string WoodDarkPath = "Assets/content/scene/mat/wood_dark.mat";
        const string WallPlasterPath = "Assets/content/scene/mat/wall_plaster.mat";
        const string FloorStagePath = "Assets/content/scene/mat/floor_stage.mat";

        const float Width = 5.4f;
        const float Depth = 3.2f;
        const float Height = 3.4f;
        const float WallThickness = 0.16f;

        [MenuItem("Master Bidder/Generate Banquet Board Prefab", priority = 41)]
        public static void GenerateMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Generate Banquet Board",
                        "banquet_board_room.prefab already exists. Overwrite?",
                        "Overwrite",
                        "Cancel"))
                    return;
            }

            if (!Generate())
                return;

            EditorUtility.DisplayDialog(
                "Banquet Board",
                $"Created/updated:\n• {PrefabPath}\n\n" +
                "Drop into the scene, align Main Camera with camera_anchor (look +Z),\n" +
                "and use a transparent UI screen so the room shows through.",
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
                    "[BanquetBoard] Missing materials. Expected:\n" +
                    WoodDarkPath + "\n" + WallPlasterPath + "\n" + FloorStagePath);
                return false;
            }

            Directory.CreateDirectory(Path.Combine(Application.dataPath, "content/scene/prefabs"));
            AssetDatabase.Refresh();

            var root = new GameObject("banquet_board_room");
            try
            {
                BuildRoom(root.transform, wood, plaster, floorMat);
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
            Debug.Log($"[BanquetBoard] Saved {PrefabPath}");
            return true;
        }

        static void BuildRoom(Transform root, Material wood, Material plaster, Material floorMat)
        {
            Box("floor", root, new Vector3(0f, -WallThickness * 0.5f, Depth * 0.5f),
                new Vector3(Width, WallThickness, Depth), floorMat);
            Box("ceiling", root, new Vector3(0f, Height + WallThickness * 0.5f, Depth * 0.5f),
                new Vector3(Width, WallThickness, Depth), plaster);
            Box("back_wall", root, new Vector3(0f, Height * 0.5f, Depth + WallThickness * 0.5f),
                new Vector3(Width + WallThickness * 2f, Height + WallThickness * 2f, WallThickness), plaster);
            Box("wall_L", root, new Vector3(-Width * 0.5f - WallThickness * 0.5f, Height * 0.5f, Depth * 0.5f),
                new Vector3(WallThickness, Height + WallThickness * 2f, Depth), plaster);
            Box("wall_R", root, new Vector3(Width * 0.5f + WallThickness * 0.5f, Height * 0.5f, Depth * 0.5f),
                new Vector3(WallThickness, Height + WallThickness * 2f, Depth), plaster);

            BuildBulletinBoard(root, wood, plaster);
            BuildBanquetTable(root, wood, plaster, floorMat);

            // Warm key light on the board + soft fill on the table.
            AddSpot(root, "board_light", new Vector3(0f, Height - 0.25f, Depth * 0.55f),
                Quaternion.Euler(55f, 0f, 0f), 2.1f, 55f, 6f);
            AddSpot(root, "table_light", new Vector3(-1.35f, Height - 0.4f, 1.1f),
                Quaternion.Euler(70f, 25f, 0f), 1.2f, 50f, 5f);

            // Almost flush with the board — board fills the frame; table sits lower-left.
            var cam = new GameObject("camera_anchor");
            cam.transform.SetParent(root, false);
            cam.transform.localPosition = new Vector3(0.15f, 1.45f, Depth - 1.35f);
            cam.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
        }

        static void BuildBulletinBoard(Transform root, Material wood, Material plaster)
        {
            var board = new GameObject("bulletin_board").transform;
            board.SetParent(root, false);
            board.localPosition = new Vector3(0f, 1.7f, Depth - 0.08f);

            const float bw = 2.9f;
            const float bh = 1.85f;
            const float frame = 0.07f;

            // Cork / plaster face.
            Box("board_face", board, Vector3.zero, new Vector3(bw, bh, 0.04f), plaster);
            // Wood frame strips.
            Box("frame_top", board, new Vector3(0f, bh * 0.5f + frame * 0.5f, 0.01f),
                new Vector3(bw + frame * 2f, frame, 0.08f), wood);
            Box("frame_bot", board, new Vector3(0f, -bh * 0.5f - frame * 0.5f, 0.01f),
                new Vector3(bw + frame * 2f, frame, 0.08f), wood);
            Box("frame_L", board, new Vector3(-bw * 0.5f - frame * 0.5f, 0f, 0.01f),
                new Vector3(frame, bh, 0.08f), wood);
            Box("frame_R", board, new Vector3(bw * 0.5f + frame * 0.5f, 0f, 0.01f),
                new Vector3(frame, bh, 0.08f), wood);

            // Notice papers — irregular grid, slight tilts.
            float[,] notes =
            {
                { -1.05f, 0.55f, 0.28f, 0.36f, -6f },
                { -0.55f, 0.62f, 0.32f, 0.28f, 4f },
                { -0.05f, 0.48f, 0.26f, 0.38f, -3f },
                { 0.45f, 0.58f, 0.30f, 0.30f, 7f },
                { 0.95f, 0.50f, 0.28f, 0.34f, -5f },
                { -0.95f, 0.05f, 0.34f, 0.30f, 3f },
                { -0.40f, 0.00f, 0.26f, 0.36f, -8f },
                { 0.15f, 0.08f, 0.32f, 0.28f, 2f },
                { 0.70f, -0.02f, 0.28f, 0.32f, -4f },
                { -1.00f, -0.50f, 0.30f, 0.28f, 5f },
                { -0.45f, -0.55f, 0.26f, 0.34f, -2f },
                { 0.10f, -0.48f, 0.34f, 0.30f, 6f },
                { 0.65f, -0.52f, 0.28f, 0.32f, -7f },
                { 1.05f, -0.10f, 0.24f, 0.36f, 3f },
                { -0.15f, -0.15f, 0.22f, 0.26f, -9f },
            };

            for (int i = 0; i < notes.GetLength(0); i++)
            {
                float x = notes[i, 0];
                float y = notes[i, 1];
                float w = notes[i, 2];
                float h = notes[i, 3];
                float yaw = notes[i, 4];
                var paper = Box($"notice_{i}", board, new Vector3(x, y, -0.035f),
                    new Vector3(w, h, 0.012f), plaster);
                paper.transform.localRotation = Quaternion.Euler(0f, 0f, yaw);
            }
        }

        static void BuildBanquetTable(Transform root, Material wood, Material plaster, Material linen)
        {
            var table = new GameObject("banquet_table").transform;
            table.SetParent(root, false);
            // Lower-left in camera view — between camera and board.
            table.localPosition = new Vector3(-1.55f, 0f, Depth - 2.05f);

            const float topY = 0.78f;
            const float topW = 1.35f;
            const float topD = 0.85f;

            Box("tabletop", table, new Vector3(0f, topY, 0f), new Vector3(topW, 0.06f, topD), wood);
            // Light cloth / runner on top.
            Box("table_cloth", table, new Vector3(0f, topY + 0.035f, 0f),
                new Vector3(topW * 0.92f, 0.02f, topD * 0.92f), linen);

            float legH = topY - 0.03f;
            float insetX = topW * 0.5f - 0.08f;
            float insetZ = topD * 0.5f - 0.08f;
            Box("leg_FL", table, new Vector3(-insetX, legH * 0.5f, -insetZ), new Vector3(0.07f, legH, 0.07f), wood);
            Box("leg_FR", table, new Vector3(insetX, legH * 0.5f, -insetZ), new Vector3(0.07f, legH, 0.07f), wood);
            Box("leg_BL", table, new Vector3(-insetX, legH * 0.5f, insetZ), new Vector3(0.07f, legH, 0.07f), wood);
            Box("leg_BR", table, new Vector3(insetX, legH * 0.5f, insetZ), new Vector3(0.07f, legH, 0.07f), wood);

            float surface = topY + 0.05f;

            // Tall glass (stem + bowl as stacked boxes).
            Box("glass_stem", table, new Vector3(-0.35f, surface + 0.12f, -0.12f),
                new Vector3(0.04f, 0.22f, 0.04f), plaster);
            Box("glass_bowl", table, new Vector3(-0.35f, surface + 0.30f, -0.12f),
                new Vector3(0.11f, 0.14f, 0.11f), plaster);
            Box("glass_base", table, new Vector3(-0.35f, surface + 0.015f, -0.12f),
                new Vector3(0.12f, 0.025f, 0.12f), plaster);

            // Serving tray + appetizer bites.
            Box("tray", table, new Vector3(0.25f, surface + 0.015f, 0.05f),
                new Vector3(0.55f, 0.025f, 0.38f), wood);

            float[,] snacks =
            {
                { 0.08f, 0.05f, 0.10f, 0.06f, 0.10f },
                { 0.22f, 0.12f, 0.08f, 0.05f, 0.08f },
                { 0.36f, -0.05f, 0.09f, 0.07f, 0.09f },
                { 0.18f, -0.10f, 0.07f, 0.04f, 0.11f },
                { 0.42f, 0.10f, 0.06f, 0.05f, 0.06f },
                { -0.02f, 0.15f, 0.08f, 0.05f, 0.07f },
            };
            for (int i = 0; i < snacks.GetLength(0); i++)
            {
                Box($"snack_{i}", table,
                    new Vector3(snacks[i, 0], surface + 0.035f + snacks[i, 3] * 0.5f, snacks[i, 1]),
                    new Vector3(snacks[i, 2], snacks[i, 3], snacks[i, 4]), plaster);
            }

            // Extra small plate near glass.
            Box("plate", table, new Vector3(-0.05f, surface + 0.012f, -0.22f),
                new Vector3(0.22f, 0.02f, 0.22f), plaster);
            Box("canape", table, new Vector3(-0.05f, surface + 0.04f, -0.22f),
                new Vector3(0.08f, 0.04f, 0.08f), wood);
        }

        static void AddSpot(Transform root, string name, Vector3 pos, Quaternion rot,
            float intensity, float angle, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.intensity = intensity;
            light.spotAngle = angle;
            light.range = range;
            light.color = new Color(1f, 0.93f, 0.8f);
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
