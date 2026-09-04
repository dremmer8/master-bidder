using System.Collections;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Orchestrates cloth reveal and canvas painting swaps.
    /// Use the custom Inspector buttons (Play mode) to run: lower → random painting → raise.
    /// </summary>
    public class PresentationOperator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private PresentationClothController cloth;
        [SerializeField] private CanvasController canvas;
        [SerializeField] private PresentationLightRig lightRig;
        [SerializeField] private PresentationInspectCamera inspectCamera;

        [Header("Demo")]
        [Tooltip("When false, Spacebar random-present is disabled (game flow owns input).")]
        [SerializeField] private bool demoHotkeysEnabled = true;

        private bool isBusy;
        private Coroutine activeSequence;

        public bool DemoHotkeysEnabled
        {
            get => demoHotkeysEnabled;
            set => demoHotkeysEnabled = value;
        }

        public bool IsBusy => isBusy;
        public PresentationClothController Cloth => cloth;
        public CanvasController Canvas => canvas;
        public PresentationLightRig LightRig => lightRig;
        public PresentationInspectCamera InspectCamera => inspectCamera;

        private void Awake()
        {
            if (cloth == null)
            {
                cloth = GetComponent<PresentationClothController>();
            }

            if (canvas == null)
            {
                canvas = GetComponent<CanvasController>();
            }

            if (lightRig == null)
            {
                lightRig = GetComponentInChildren<PresentationLightRig>(true);
            }

            if (inspectCamera == null)
            {
                inspectCamera = GetComponent<PresentationInspectCamera>();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (demoHotkeysEnabled && Input.GetKeyDown(KeyCode.Space))
            {
                PresentRandomPainting();
            }
        }

        /// <summary>
        /// Full reveal sequence: lower cloth → swap to a random painting → raise cloth.
        /// </summary>
        public void PresentRandomPainting()
        {
            if (isBusy)
            {
                Debug.LogWarning("[PresentationOperator] Sequence already running.", this);
                return;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PresentationOperator] Enter Play mode to run the presentation sequence.", this);
                return;
            }

            ExitInspectMode();
            activeSequence = StartCoroutine(PresentRandomPaintingRoutine());
        }

        /// <summary>
        /// Full reveal sequence for a painting matched by MVP artwork id.
        /// </summary>
        public void PresentPaintingById(string artworkId)
        {
            PresentPaintingById(artworkId, null);
        }

        public void PresentPaintingById(string artworkId, System.Action onComplete)
        {
            if (string.IsNullOrEmpty(artworkId) || canvas == null)
            {
                onComplete?.Invoke();
                return;
            }

            var list = canvas.Paintings;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].artworkId == artworkId)
                {
                    PresentPainting(i, onComplete);
                    return;
                }
            }

            Debug.LogWarning($"[PresentationOperator] No painting with artworkId '{artworkId}' in CanvasController list.", this);
            onComplete?.Invoke();
        }

        /// <summary>
        /// Present a specific PaintingData (adds to canvas list if needed).
        /// </summary>
        public void PresentPaintingData(NineSlice3D.PaintingData data, System.Action onComplete = null)
        {
            if (data == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (isBusy)
            {
                Debug.LogWarning("[PresentationOperator] Sequence already running.", this);
                onComplete?.Invoke();
                return;
            }

            if (!Application.isPlaying)
            {
                onComplete?.Invoke();
                return;
            }

            ExitInspectMode();
            activeSequence = StartCoroutine(PresentPaintingDataRoutine(data, onComplete));
        }

        public void PresentPainting(int index)
        {
            PresentPainting(index, null);
        }

        public void PresentPainting(int index, System.Action onComplete)
        {
            if (isBusy)
            {
                Debug.LogWarning("[PresentationOperator] Sequence already running.", this);
                onComplete?.Invoke();
                return;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PresentationOperator] Enter Play mode to run the presentation sequence.", this);
                onComplete?.Invoke();
                return;
            }

            ExitInspectMode();
            activeSequence = StartCoroutine(PresentPaintingRoutine(index, onComplete));
        }

        public void LowerCloth() => cloth?.Lower();
        public void RaiseCloth() => cloth?.Raise();
        public void SwapToRandomPainting()
        {
            ExitInspectMode();
            if (canvas != null && canvas.ApplyRandomPainting())
            {
                AimLight();
            }
        }

        public void SwapToNextPainting()
        {
            ExitInspectMode();
            if (canvas != null && canvas.NextPainting())
            {
                AimLight();
            }
        }

        public void AimLight() => lightRig?.AimAtPainting();

        public void ExitInspectMode()
        {
            if (inspectCamera != null && inspectCamera.IsInspecting)
            {
                inspectCamera.ExitInspect(snap: true);
            }
        }

        public void CancelSequence()
        {
            if (activeSequence != null)
            {
                StopCoroutine(activeSequence);
                activeSequence = null;
            }

            isBusy = false;
        }

        private IEnumerator PresentRandomPaintingRoutine()
        {
            yield return RunRevealSequence(() => canvas != null && canvas.ApplyRandomPainting(), null);
        }

        private IEnumerator PresentPaintingRoutine(int index, System.Action onComplete)
        {
            yield return RunRevealSequence(() => canvas != null && canvas.ApplyPainting(index), onComplete);
        }

        private IEnumerator PresentPaintingDataRoutine(NineSlice3D.PaintingData data, System.Action onComplete)
        {
            yield return RunRevealSequence(() => canvas != null && canvas.ApplyPainting(data), onComplete);
        }

        private IEnumerator RunRevealSequence(System.Func<bool> swapAction, System.Action onComplete)
        {
            if (cloth == null || canvas == null)
            {
                Debug.LogError("[PresentationOperator] Cloth or Canvas controller missing.", this);
                onComplete?.Invoke();
                yield break;
            }

            isBusy = true;

            yield return cloth.LowerAndWait();

            bool swapped = swapAction();
            if (!swapped)
            {
                Debug.LogWarning("[PresentationOperator] Painting swap failed; still raising cloth.", this);
            }
            else
            {
                AimLight();
            }

            yield return cloth.RaiseAndWait();

            isBusy = false;
            activeSequence = null;
            onComplete?.Invoke();
        }
    }
}
