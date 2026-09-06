using System.Collections;
using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Positions the sold-ticket label on the painting's authored front corner
    /// and drives the <c>appear</c> animator trigger.
    /// </summary>
    public class PresentationTicketController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private NineSliceMesh3D canvasMesh;

        [Header("Placement")]
        [Tooltip("Normalized depth on painting bounds (1 = front face).")]
        [Range(0f, 1f)]
        [SerializeField] private float faceDepth = 1f;

        [Tooltip(
            "When false, anchors to local min-X (authored top-right in the hall view). " +
            "When true, anchors to local max-X.")]
        [SerializeField] private bool useMaxXCorner;

        [Tooltip("Local inset from the chosen top/front corner (canvas space).")]
        [SerializeField] private Vector3 cornerInset;

        [Tooltip("Local euler relative to the canvas (matches authored ticket_root pose).")]
        [SerializeField] private Vector3 localEuler = new Vector3(90f, 0f, 0f);

        [Header("Animator")]
        [SerializeField] private string appearTrigger = "appear";
        [SerializeField] private string hiddenStateName = "ticket_hidden_loop";
        [SerializeField] private string visibleStateName = "ticket_visible_loop";
        [SerializeField] private float stateWaitTimeout = 5f;

        private bool isAnimating;
        private Coroutine trackRoutine;

        public bool IsAnimating => isAnimating;
        public NineSliceMesh3D CanvasMesh => canvasMesh;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Parent to the canvas, snap to the authored corner for the current size,
        /// and restart the animator in the hidden loop.
        /// </summary>
        public void BindToCanvas(NineSliceMesh3D mesh)
        {
            if (mesh != null)
                canvasMesh = mesh;

            AttachToPaintingCorner();
            RestartHidden();
        }

        /// <summary>Recompute local pose from the current canvas bounds.</summary>
        public void AttachToPaintingCorner()
        {
            if (canvasMesh == null) return;

            Transform canvasTx = canvasMesh.transform;
            transform.SetParent(canvasTx, false);

            if (!TryGetCornerLocal(out Vector3 localCorner))
                return;

            transform.localPosition = localCorner + cornerInset;
            transform.localRotation = Quaternion.Euler(localEuler);
            transform.localScale = Vector3.one;
        }

        /// <summary>Reset animator to the hidden idle without playing appear.</summary>
        public void RestartHidden()
        {
            if (trackRoutine != null)
            {
                StopCoroutine(trackRoutine);
                trackRoutine = null;
            }

            isAnimating = false;

            if (animator == null) return;

            animator.ResetTrigger(appearTrigger);
            animator.Rebind();
            animator.Update(0f);
            animator.Play(hiddenStateName, 0, 0f);
            animator.Update(0f);
        }

        /// <summary>Fire <c>appear</c> and wait until the visible loop state.</summary>
        public void Appear()
        {
            if (animator == null || isAnimating) return;

            animator.ResetTrigger(appearTrigger);
            animator.SetTrigger(appearTrigger);
            BeginTracking(visibleStateName);
        }

        /// <summary>Fire appear and wait until visible idle is reached.</summary>
        public IEnumerator AppearAndWait()
        {
            Appear();
            while (isAnimating)
                yield return null;
        }

        private void BeginTracking(string stateName)
        {
            if (trackRoutine != null)
                StopCoroutine(trackRoutine);

            trackRoutine = StartCoroutine(TrackUntilState(stateName));
        }

        private IEnumerator TrackUntilState(string stateName)
        {
            isAnimating = true;

            if (animator == null)
            {
                isAnimating = false;
                trackRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < stateWaitTimeout)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && info.IsName(stateName))
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            isAnimating = false;
            trackRoutine = null;
        }

        private bool TryGetCornerLocal(out Vector3 localPoint)
        {
            localPoint = default;
            if (canvasMesh == null) return false;

            Bounds orig = canvasMesh.OriginalBounds;
            if (orig.size.sqrMagnitude < 1e-10f && !canvasMesh.IsInitialized)
            {
                canvasMesh.InitializeMeshes(forceRebind: true);
                orig = canvasMesh.OriginalBounds;
            }

            Mesh3DSlicer.CalculateTargetBounds(
                orig,
                canvasMesh.SizeMeters,
                canvasMesh.Pivot,
                canvasMesh.CustomPivot,
                canvasMesh.Borders,
                out Vector3 targetMin,
                out Vector3 targetMax
            );

            float x = useMaxXCorner ? targetMax.x : targetMin.x;
            localPoint = new Vector3(
                x,
                targetMax.y,
                Mathf.Lerp(targetMin.z, targetMax.z, faceDepth)
            );
            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Bind To Canvas Corner")]
        private void DebugBind()
        {
            if (canvasMesh == null)
                canvasMesh = FindObjectOfType<NineSliceMesh3D>();
            BindToCanvas(canvasMesh);
        }

        [ContextMenu("Debug/Appear")]
        private void DebugAppear() => Appear();
#endif
    }
}
