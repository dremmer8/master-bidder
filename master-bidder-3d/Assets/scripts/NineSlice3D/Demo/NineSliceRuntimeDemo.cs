using UnityEngine;

namespace NineSlice3D.Demo
{
    /// <summary>
    /// Demonstrates runtime control over dimensions, submesh materials, and constant texel density tiling.
    /// </summary>
    public class NineSliceRuntimeDemo : MonoBehaviour
    {
        [Header("Target Component")]
        [SerializeField] private NineSliceMesh3D nineSliceMesh;

        [Header("Runtime Dimensions (Centimeters)")]
        [SerializeField] private Vector3 testSizeCm = new Vector3(80f, 120f, 5f);

        [Header("Submesh Material Swap Test")]
        [Tooltip("Material slot index to swap (0 = Canvas Body Picture, 1 = Frame Sides, etc.).")]
        [SerializeField] private int targetMaterialSlot = 0;
        [SerializeField] private Material alternateMaterial;
        [SerializeField] private Texture alternatePictureTexture;

        [Header("Animation Test")]
        [SerializeField] private bool enableBreathingAnimation = false;
        [SerializeField] private Vector3 minAnimatedSize = new Vector3(50f, 50f, 5f);
        [SerializeField] private Vector3 maxAnimatedSize = new Vector3(150f, 150f, 5f);
        [SerializeField] private float animationSpeed = 1f;

        private void Start()
        {
            if (nineSliceMesh == null)
            {
                nineSliceMesh = GetComponent<NineSliceMesh3D>();
            }

            if (nineSliceMesh != null)
            {
                // Set initial size in centimeters
                nineSliceMesh.SetSize(testSizeCm, MeasurementUnit.Centimeters);

                // Configure constant texel density for Sides (slot 1) calibrated to 50x50 cm
                nineSliceMesh.SetSubmeshTilingCalibration(
                    slotIndex: 1,
                    referenceSizeCm: new Vector2(50f, 50f),
                    scaleX: false,
                    scaleY: true
                );
            }
        }

        private void Update()
        {
            if (nineSliceMesh == null) return;

            if (enableBreathingAnimation)
            {
                float t = (Mathf.Sin(Time.time * animationSpeed) + 1f) * 0.5f;
                Vector3 currentSize = Vector3.Lerp(minAnimatedSize, maxAnimatedSize, t);
                nineSliceMesh.SetSize(currentSize, MeasurementUnit.Centimeters);
            }
        }

        /// <summary>
        /// Swaps the material on the specified slot at runtime.
        /// </summary>
        public void SwapMaterial()
        {
            if (nineSliceMesh != null && alternateMaterial != null)
            {
                nineSliceMesh.SetMaterial(targetMaterialSlot, alternateMaterial);
            }
        }

        /// <summary>
        /// Replaces the picture texture directly on a submesh material slot.
        /// </summary>
        public void SwapPictureTexture()
        {
            if (nineSliceMesh != null && alternatePictureTexture != null)
            {
                nineSliceMesh.SetSubmeshTexture(targetMaterialSlot, alternatePictureTexture);
            }
        }

        /// <summary>
        /// Resizes picture width in cm.
        /// </summary>
        public void SetWidthCm(float widthCm)
        {
            if (nineSliceMesh != null)
            {
                nineSliceMesh.Width = widthCm;
            }
        }

        /// <summary>
        /// Resizes picture height in cm.
        /// </summary>
        public void SetHeightCm(float heightCm)
        {
            if (nineSliceMesh != null)
            {
                nineSliceMesh.Height = heightCm;
            }
        }

        /// <summary>
        /// Resizes picture depth in cm.
        /// </summary>
        public void SetDepthCm(float depthCm)
        {
            if (nineSliceMesh != null)
            {
                nineSliceMesh.Depth = depthCm;
            }
        }
    }
}
