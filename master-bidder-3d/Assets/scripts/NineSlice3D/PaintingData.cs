using UnityEngine;

namespace NineSlice3D
{
    public enum ArtworkOrientation
    {
        Landscape,
        Portrait,
        Square
    }

    [CreateAssetMenu(fileName = "NewPaintingData", menuName = "Master Bidder/Painting Config", order = 100)]
    public class PaintingData : ScriptableObject
    {
        [Header("Artwork Info")]
        [Tooltip("Stable MVP artwork id (kebab-case), e.g. mona-lisa. Used for matching and saves.")]
        public string artworkId = "";

        [Tooltip("Title of the artwork (English display).")]
        public string paintingTitle = "Untitled";

        [Tooltip("Artist name (English display).")]
        public string artist = "Unknown";

        [Header("Gameplay (MVP)")]
        [Tooltip("Russian title — matching and RU locale display.")]
        public string titleRu = "";

        [Tooltip("Russian artist name — matching key for artist orders.")]
        public string artistRu = "";

        [Tooltip("Creation year / date string as shown in MVP.")]
        public string year = "";

        [Tooltip("Russian period tag — matching key.")]
        public string periodRu = "";

        [Tooltip("Russian genre tag — matching key.")]
        public string genreRu = "";

        [Tooltip("Interesting fact (Russian).")]
        [TextArea(2, 6)]
        public string factRu = "";

        [Tooltip("Rarity tier: common, rare, or epic.")]
        public string rarity = "common";

        [Tooltip("Base hammer price before jitter (MVP).")]
        public int basePrice = 10000;

        [Header("Texture")]
        [Tooltip("Albedo / Artwork texture to be applied to the canvas.")]
        public Texture2D albedoTexture;

        [Header("Real-world Dimensions")]
        [Tooltip("Unit of measurement for the width/height/depth below.")]
        public MeasurementUnit unit = MeasurementUnit.Centimeters;

        [Tooltip("Horizontal dimension (X axis in Unity). Width of the painting.")]
        [Min(0.01f)]
        public float width = 50f;

        [Tooltip("Vertical dimension (Y axis in Unity). Height of the painting.")]
        [Min(0.01f)]
        public float height = 50f;

        [Tooltip("Depth / Thickness of the frame (Z axis in Unity).")]
        [Min(0.01f)]
        public float depth = 4f;

        [Header("Pivot Alignment (Optional Override)")]
        [Tooltip("If enabled, applies this specific pivot anchor when loading the painting.")]
        public bool overridePivot = false;

        [Tooltip("Pivot anchor preset to use when override is enabled.")]
        public PivotAnchor pivotAnchor = PivotAnchor.Center;

        [Tooltip("Custom normalized pivot (0..1) when pivotAnchor is set to Custom.")]
        public Vector3 customPivot = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("Target Submesh")]
        [Tooltip("Material slot index for the painting canvas (default: 0).")]
        public int canvasMaterialSlotIndex = 0;

        [Tooltip("Shader texture property name to replace (e.g. _MainTex or _BaseMap).")]
        public string texturePropertyName = "_MainTex";

        public Vector3 Size => new Vector3(width, height, depth);

        public Vector3 SizeInMeters => Mesh3DSlicer.ConvertUnitsToMeters(Size, unit);
        public Vector3 SizeInCentimeters => Mesh3DSlicer.ConvertMetersToUnits(SizeInMeters, MeasurementUnit.Centimeters);

        public ArtworkOrientation Orientation
        {
            get
            {
                if (Mathf.Approximately(width, height)) return ArtworkOrientation.Square;
                return width > height ? ArtworkOrientation.Landscape : ArtworkOrientation.Portrait;
            }
        }

        public float AspectRatio => height > 0.0001f ? (width / height) : 1f;

        /// <summary>
        /// Applies this painting configuration (dimensions and albedo texture) directly to a NineSliceMesh3D component.
        /// </summary>
        public void ApplyTo(NineSliceMesh3D meshDeformer)
        {
            if (meshDeformer == null) return;

            // 1. Apply dimensions (X = width, Y = height, Z = depth)
            meshDeformer.SetSize(Size, unit);

            // 2. Apply pivot if overridden
            if (overridePivot)
            {
                meshDeformer.Pivot = pivotAnchor;
                if (pivotAnchor == PivotAnchor.Custom)
                {
                    meshDeformer.CustomPivot = customPivot;
                }
            }

            // 3. Apply texture to the canvas material slot
            if (albedoTexture != null)
            {
                meshDeformer.SetSubmeshTexture(canvasMaterialSlotIndex, albedoTexture, texturePropertyName);
            }
        }
    }
}
