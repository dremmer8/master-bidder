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

        private bool isBusy;
        private Coroutine activeSequence;

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

            if (Input.GetKeyDown(KeyCode.Space))
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
        /// Full reveal sequence for a specific painting index.
        /// </summary>
        public void PresentPainting(int index)
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
            activeSequence = StartCoroutine(PresentPaintingRoutine(index));
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
            yield return RunRevealSequence(() => canvas != null && canvas.ApplyRandomPainting());
        }

        private IEnumerator PresentPaintingRoutine(int index)
        {
            yield return RunRevealSequence(() => canvas != null && canvas.ApplyPainting(index));
        }

        private IEnumerator RunRevealSequence(System.Func<bool> swapAction)
        {
            if (cloth == null || canvas == null)
            {
                Debug.LogError("[PresentationOperator] Cloth or Canvas controller missing.", this);
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
        }
    }
}
