using System.Collections.Generic;
using UnityEngine;

namespace NineSlice3D.Demo
{
    /// <summary>
    /// Demonstrates runtime switching of painting configurations, auto-resizing,
    /// and texture replacement on a 3D canvas mesh.
    /// </summary>
    public class PaintingSwitcherDemo : MonoBehaviour
    {
        [Header("Target Mesh Deformer")]
        [SerializeField] private NineSliceMesh3D nineSliceMesh;

        [Header("Available Paintings")]
        [SerializeField] private List<PaintingData> paintings = new List<PaintingData>();

        private int currentIndex = 0;

        private void Start()
        {
            if (nineSliceMesh == null)
            {
                nineSliceMesh = GetComponent<NineSliceMesh3D>();
            }

            if (paintings.Count > 0 && nineSliceMesh != null)
            {
                ApplyPainting(0);
            }
        }

        /// <summary>
        /// Applies the painting at the specified index.
        /// </summary>
        public void ApplyPainting(int index)
        {
            if (paintings == null || paintings.Count == 0 || nineSliceMesh == null) return;

            currentIndex = Mathf.Clamp(index, 0, paintings.Count - 1);
            PaintingData data = paintings[currentIndex];
            if (data != null)
            {
                nineSliceMesh.ApplyPaintingConfig(data);
                Debug.Log($"[PaintingSwitcher] Applied: '{data.paintingTitle}' by {data.artist} ({data.width}x{data.height} cm)");
            }
        }

        /// <summary>
        /// Cycles to the next painting in the list.
        /// </summary>
        public void NextPainting()
        {
            if (paintings == null || paintings.Count == 0) return;
            int nextIdx = (currentIndex + 1) % paintings.Count;
            ApplyPainting(nextIdx);
        }

        /// <summary>
        /// Cycles to the previous painting in the list.
        /// </summary>
        public void PreviousPainting()
        {
            if (paintings == null || paintings.Count == 0) return;
            int prevIdx = (currentIndex - 1 + paintings.Count) % paintings.Count;
            ApplyPainting(prevIdx);
        }
    }
}
