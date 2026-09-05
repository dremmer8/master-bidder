using System.Collections;
using MasterBidder.Audio;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Drives the presentation Animator that raises and lowers the reveal cloth.
    /// Uses triggers <c>rise</c> / <c>down</c> on the presentation controller.
    /// </summary>
    public class PresentationClothController : MonoBehaviour
    {
        public enum ClothPose
        {
            Raised,
            Lowered,
            Animating
        }

        [Header("Animator")]
        [SerializeField] private Animator animator;

        [SerializeField] private string riseTrigger = "rise";
        [SerializeField] private string downTrigger = "down";
        [SerializeField] private string raisedStateName = "rise_loop_1";
        [SerializeField] private string loweredStateName = "down_loop_1";

        [Header("Timing")]
        [Tooltip("Extra wait after reaching a pose so MagicaCloth can settle.")]
        [SerializeField] private float settleDelay = 0.15f;

        [Tooltip("Safety timeout when waiting for an animator state.")]
        [SerializeField] private float stateWaitTimeout = 5f;

        private ClothPose trackedPose = ClothPose.Raised;
        private bool isAnimating;
        private Coroutine trackRoutine;

        public ClothPose CurrentPose => isAnimating ? ClothPose.Animating : trackedPose;
        public bool IsAnimating => isAnimating;
        public bool IsRaised => !isAnimating && trackedPose == ClothPose.Raised;
        public bool IsLowered => !isAnimating && trackedPose == ClothPose.Lowered;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                Debug.LogError("[PresentationCloth] No Animator assigned.", this);
            }
        }

        /// <summary>Fire the down trigger to lower the cloth over the painting.</summary>
        public void Lower()
        {
            if (animator == null || isAnimating) return;
            if (trackedPose == ClothPose.Lowered) return;

            AudioService.PlayClothDown();
            animator.SetTrigger(downTrigger);
            trackedPose = ClothPose.Lowered;
            BeginTracking(loweredStateName);
        }

        /// <summary>Fire the rise trigger to raise the cloth and reveal the painting.</summary>
        public void Raise()
        {
            if (animator == null || isAnimating) return;
            if (trackedPose == ClothPose.Raised) return;

            AudioService.PlayNextPainting();
            animator.SetTrigger(riseTrigger);
            trackedPose = ClothPose.Raised;
            BeginTracking(raisedStateName);
        }

        /// <summary>Lower the cloth and wait until the lowered idle state is reached.</summary>
        public IEnumerator LowerAndWait()
        {
            if (IsLowered)
            {
                yield break;
            }

            Lower();
            while (isAnimating)
            {
                yield return null;
            }
        }

        /// <summary>Raise the cloth and wait until the raised idle state is reached.</summary>
        public IEnumerator RaiseAndWait()
        {
            if (IsRaised)
            {
                yield break;
            }

            Raise();
            while (isAnimating)
            {
                yield return null;
            }
        }

        private void BeginTracking(string stateName)
        {
            if (trackRoutine != null)
            {
                StopCoroutine(trackRoutine);
            }

            trackRoutine = StartCoroutine(TrackUntilPose(stateName));
        }

        private IEnumerator TrackUntilPose(string stateName)
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
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (settleDelay > 0f)
            {
                yield return new WaitForSeconds(settleDelay);
            }

            isAnimating = false;
            trackRoutine = null;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Lower Cloth")]
        private void DebugLower() => Lower();

        [ContextMenu("Debug/Raise Cloth")]
        private void DebugRaise() => Raise();
#endif
    }
}
