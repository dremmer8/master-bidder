using System.Collections.Generic;
using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Swaps painting configs on a <see cref="NineSliceMesh3D"/> canvas at runtime.
    /// </summary>
    public class CanvasController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private NineSliceMesh3D canvasMesh;

        [Header("Available Paintings")]
        [SerializeField] private List<PaintingData> paintings = new List<PaintingData>();

        private int currentIndex = -1;

        public NineSliceMesh3D CanvasMesh => canvasMesh;
        public IReadOnlyList<PaintingData> Paintings => paintings;
        public int CurrentIndex => currentIndex;
        public PaintingData CurrentPainting =>
            currentIndex >= 0 && currentIndex < paintings.Count ? paintings[currentIndex] : null;
        public int PaintingCount => paintings != null ? paintings.Count : 0;

        private void Awake()
        {
            if (canvasMesh == null)
            {
                canvasMesh = GetComponentInChildren<NineSliceMesh3D>(true);
            }

            SyncIndexFromActivePainting();
        }

        /// <summary>Applies the painting at the given list index.</summary>
        public bool ApplyPainting(int index)
        {
            if (!HasPaintings() || canvasMesh == null) return false;

            index = Mathf.Clamp(index, 0, paintings.Count - 1);
            PaintingData data = paintings[index];
            if (data == null) return false;

            canvasMesh.ApplyPaintingConfig(data);
            currentIndex = index;
            Debug.Log($"[CanvasController] Applied: '{data.paintingTitle}' by {data.artist} ({data.width}x{data.height} cm)");
            return true;
        }

        /// <summary>Applies a specific painting config (adds to list if missing).</summary>
        public bool ApplyPainting(PaintingData data)
        {
            if (data == null || canvasMesh == null) return false;

            int index = paintings.IndexOf(data);
            if (index < 0)
            {
                paintings.Add(data);
                index = paintings.Count - 1;
            }

            return ApplyPainting(index);
        }

        /// <summary>Cycles to the next painting in the list.</summary>
        public bool NextPainting()
        {
            if (!HasPaintings()) return false;
            int next = currentIndex < 0 ? 0 : (currentIndex + 1) % paintings.Count;
            return ApplyPainting(next);
        }

        /// <summary>Cycles to the previous painting in the list.</summary>
        public bool PreviousPainting()
        {
            if (!HasPaintings()) return false;
            int prev = currentIndex < 0
                ? 0
                : (currentIndex - 1 + paintings.Count) % paintings.Count;
            return ApplyPainting(prev);
        }

        /// <summary>
        /// Picks a random painting different from the current one (when possible).
        /// </summary>
        public bool ApplyRandomPainting()
        {
            if (!HasPaintings() || canvasMesh == null) return false;

            int index;
            if (paintings.Count == 1)
            {
                index = 0;
            }
            else
            {
                do
                {
                    index = Random.Range(0, paintings.Count);
                } while (index == currentIndex);
            }

            return ApplyPainting(index);
        }

        private bool HasPaintings()
        {
            if (paintings == null || paintings.Count == 0)
            {
                Debug.LogWarning("[CanvasController] No paintings assigned.", this);
                return false;
            }

            return true;
        }

        private void SyncIndexFromActivePainting()
        {
            if (canvasMesh == null || paintings == null) return;

            PaintingData active = canvasMesh.ActivePainting;
            if (active == null) return;

            int index = paintings.IndexOf(active);
            if (index >= 0)
            {
                currentIndex = index;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Next Painting")]
        private void DebugNext() => NextPainting();

        [ContextMenu("Debug/Random Painting")]
        private void DebugRandom() => ApplyRandomPainting();
#endif
    }
}
