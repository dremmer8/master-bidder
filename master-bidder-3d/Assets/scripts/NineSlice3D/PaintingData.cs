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
        [Tooltip("Title of the artwork.")]
        public string paintingTitle = "Untitled";

        [Tooltip("Artist name.")]
        public string artist = "Unknown";

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

            // 2. Apply texture to the canvas material slot
            if (albedoTexture != null)
            {
                meshDeformer.SetSubmeshTexture(canvasMaterialSlotIndex, albedoTexture, texturePropertyName);
            }
        }
    }
}
