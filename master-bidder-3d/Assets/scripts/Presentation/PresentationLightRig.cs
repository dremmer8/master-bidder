using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Aims a light so its local +Z (forward) points at the painting's top-center,
    /// with an adjustable vertical offset along the canvas up axis.
    /// Rotation eases smoothly toward the target to avoid jumps.
    /// </summary>
    [ExecuteAlways]
    public class PresentationLightRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform lightTransform;
        [SerializeField] private NineSliceMesh3D canvasMesh;

        [Header("Aim")]
        [Tooltip("World-meters offset along the canvas up axis from the painting top-center.")]
        [SerializeField] private float verticalOffset = 0.05f;

        [Tooltip("Normalized depth on the painting bounds (0 = back, 1 = front). 0.5 = mid-thickness.")]
        [Range(0f, 1f)]
        [SerializeField] private float faceDepth = 1f;

        [Header("Smoothing")]
        [Tooltip("Higher = snappier. Exponential ease toward target rotation.")]
        [SerializeField, Min(0.01f)] private float aimSmoothSpeed = 5f;

        [SerializeField] private bool continuousAim = true;
        [SerializeField] private bool drawGizmos = true;

        private Quaternion targetRotation = Quaternion.identity;
        private bool hasTarget;

        public Transform LightTransform => lightTransform;
        public NineSliceMesh3D CanvasMesh => canvasMesh;
        public float VerticalOffset
        {
            get => verticalOffset;
            set => verticalOffset = value;
        }

        public Vector3 AimPoint { get; private set; }
        public bool HasTarget => hasTarget;

        private void OnEnable()
        {
            ResolveRefs();
            RefreshTargetRotation();
            ApplyRotationImmediate();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || lightTransform == null) return;

            if (continuousAim)
            {
                RefreshTargetRotation();
            }

            if (!hasTarget) return;

            ApplyRotationSmooth(Time.deltaTime);
        }

        private void OnValidate()
        {
            aimSmoothSpeed = Mathf.Max(0.01f, aimSmoothSpeed);
            if (!isActiveAndEnabled) return;

            RefreshTargetRotation();
            if (!Application.isPlaying)
            {
                ApplyRotationImmediate();
            }
        }

        /// <summary>
        /// Updates the aim target. In Play mode rotation eases toward it; in Edit mode it snaps.
        /// </summary>
        public void AimAtPainting()
        {
            ResolveRefs();
            RefreshTargetRotation();

            if (!Application.isPlaying)
            {
                ApplyRotationImmediate();
            }
        }

        /// <summary>
        /// Updates the aim target and snaps rotation immediately (no tween).
        /// </summary>
        public void AimAtPaintingImmediate()
        {
            ResolveRefs();
            RefreshTargetRotation();
            ApplyRotationImmediate();
        }

        /// <summary>
        /// World-space aim target: top-center of the painting bounds + vertical offset.
        /// </summary>
        public bool TryGetAimPoint(out Vector3 worldAimPoint)
        {
            worldAimPoint = default;
            if (canvasMesh == null) return false;

            if (!TryGetPaintingTopCenterLocal(out Vector3 localTopCenter)) return false;

            Transform canvasTx = canvasMesh.transform;
            worldAimPoint = canvasTx.TransformPoint(localTopCenter) + canvasTx.up * verticalOffset;
            return true;
        }

        private void RefreshTargetRotation()
        {
            ResolveRefs();
            if (lightTransform == null || canvasMesh == null) return;
            if (!TryGetAimPoint(out Vector3 aimPoint)) return;

            AimPoint = aimPoint;
            Vector3 direction = aimPoint - lightTransform.position;
            if (direction.sqrMagnitude < 1e-8f) return;

            Vector3 forward = direction.normalized;
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = canvasMesh.transform.right;
            }

            targetRotation = Quaternion.LookRotation(forward, up);
            hasTarget = true;
        }

        private void ApplyRotationImmediate()
        {
            if (!hasTarget || lightTransform == null) return;
            lightTransform.rotation = targetRotation;
        }

        private void ApplyRotationSmooth(float deltaTime)
        {
            float angle = Quaternion.Angle(lightTransform.rotation, targetRotation);
            if (angle < 0.05f)
            {
                lightTransform.rotation = targetRotation;
                return;
            }

            float t = 1f - Mathf.Exp(-aimSmoothSpeed * deltaTime);
            lightTransform.rotation = Quaternion.Slerp(lightTransform.rotation, targetRotation, t);
        }

        private bool TryGetPaintingTopCenterLocal(out Vector3 localPoint)
        {
            localPoint = default;

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

            localPoint = new Vector3(
                Mathf.Lerp(targetMin.x, targetMax.x, 0.5f),
                targetMax.y,
                Mathf.Lerp(targetMin.z, targetMax.z, faceDepth)
            );
            return true;
        }

        private void ResolveRefs()
        {
            if (lightTransform == null)
            {
                lightTransform = transform;
            }

            if (canvasMesh == null)
            {
                canvasMesh = GetComponentInChildren<NineSliceMesh3D>(true);
                if (canvasMesh == null && transform.parent != null)
                {
                    canvasMesh = transform.parent.GetComponentInChildren<NineSliceMesh3D>(true);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            if (!TryGetAimPoint(out Vector3 aim)) return;

            Transform lightTx = lightTransform != null ? lightTransform : transform;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(lightTx.position, aim);
            Gizmos.DrawWireSphere(aim, 0.03f);

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
            Gizmos.DrawRay(lightTx.position, lightTx.forward * Vector3.Distance(lightTx.position, aim));
        }
    }
}
