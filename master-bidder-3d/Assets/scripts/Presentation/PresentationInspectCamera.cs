using System.Collections;
using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Alternative inspect camera for close-up painting viewing.
    /// RMB toggles mode. Mouse look moves a surface pivot via look-ray.
    /// Scroll zooms toward the pivot. LMB smoothly reframes to face the pivot head-on.
    /// </summary>
    public class PresentationInspectCamera : MonoBehaviour
    {
        [Header("Cameras")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera inspectCamera;

        [Header("Painting")]
        [SerializeField] private NineSliceMesh3D canvasMesh;
        [Tooltip("Normalized depth on painting bounds (1 = front face).")]
        [Range(0f, 1f)]
        [SerializeField] private float faceDepth = 1f;

        [Header("Framing")]
        [Tooltip("Starting / frontal stand-off distance from the painting surface.")]
        [SerializeField] private float enterDistance = 0.55f;
        [SerializeField] private float minDistance = 0.12f;
        [SerializeField] private float maxDistance = 2.5f;

        [Header("Controls")]
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float zoomSensitivity = 0.35f;
        [SerializeField] private float panSmoothSpeed = 6f;
        [SerializeField] private bool lockCursorWhileInspecting = true;
        [SerializeField] private bool showPivotMarker = true;
        [SerializeField] private float pivotMarkerScale = 0.008333f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private bool isInspecting;
        private bool hasPivot;
        private Vector3 pivot;
        private float distance;
        private bool isPanning;
        private Coroutine panRoutine;
        private Transform pivotMarker;
        private AudioListener mainListener;
        private AudioListener inspectListener;

        private Vector3 surfaceNormal = Vector3.forward;
        private Vector3 surfaceUp = Vector3.up;
        private Vector3 surfaceRight = Vector3.right;
        private Vector3 surfaceCenter;
        private float halfWidth;
        private float halfHeight;

        public bool IsInspecting => isInspecting;
        public Vector3 Pivot => pivot;
        public Camera ActiveCamera => isInspecting ? inspectCamera : mainCamera;

        private void Awake()
        {
            ResolveRefs();
            EnsurePivotMarker();
            SetInspectActive(false, snap: true);
        }

        private void OnDisable()
        {
            if (isInspecting)
            {
                ExitInspect(snap: true);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (Input.GetMouseButtonDown(1))
            {
                ToggleInspect();
            }

            if (!isInspecting || inspectCamera == null) return;
            if (isPanning) return;

            HandleLook();
            UpdatePivotFromLookRay();
            HandleZoom();

            if (Input.GetMouseButtonDown(0) && hasPivot)
            {
                PanToFacePivot();
            }

            UpdatePivotMarker();
        }

        public void ToggleInspect()
        {
            if (isInspecting)
            {
                ExitInspect(snap: false);
            }
            else
            {
                EnterInspect();
            }
        }

        public void EnterInspect()
        {
            ResolveRefs();
            if (inspectCamera == null || canvasMesh == null)
            {
                Debug.LogWarning("[InspectCamera] Missing inspect camera or canvas.", this);
                return;
            }

            if (!RefreshSurface())
            {
                Debug.LogWarning("[InspectCamera] Could not resolve painting surface.", this);
                return;
            }

            CancelPan();

            pivot = surfaceCenter;
            hasPivot = true;
            distance = Mathf.Clamp(enterDistance, minDistance, maxDistance);

            PlaceFrontal(pivot, distance, immediate: true);
            SetInspectActive(true, snap: true);
            isInspecting = true;
            UpdatePivotMarker();
        }

        public void ExitInspect(bool snap)
        {
            CancelPan();
            isInspecting = false;
            hasPivot = false;
            SetInspectActive(false, snap);
            SetCursorLocked(false);
            UpdatePivotMarker();
        }

        public void PanToFacePivot()
        {
            if (!hasPivot || inspectCamera == null) return;

            CancelPan();
            panRoutine = StartCoroutine(PanToFacePivotRoutine());
        }

        private IEnumerator PanToFacePivotRoutine()
        {
            isPanning = true;
            RefreshSurface();

            float targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            Vector3 endPos = pivot + surfaceNormal * targetDistance;
            Quaternion endRot = Quaternion.LookRotation(-surfaceNormal, surfaceUp);
            float timeout = 3f;

            while (timeout > 0f)
            {
                timeout -= Time.deltaTime;
                float eased = 1f - Mathf.Exp(-panSmoothSpeed * Time.deltaTime);
                Transform cam = inspectCamera.transform;
                cam.position = Vector3.Lerp(cam.position, endPos, eased);
                cam.rotation = Quaternion.Slerp(cam.rotation, endRot, eased);

                if (Quaternion.Angle(cam.rotation, endRot) < 0.2f &&
                    (cam.position - endPos).sqrMagnitude < 1e-6f)
                {
                    break;
                }

                yield return null;
            }

            inspectCamera.transform.SetPositionAndRotation(endPos, endRot);
            distance = targetDistance;
            isPanning = false;
            panRoutine = null;
        }

        private void HandleLook()
        {
            float mx = Input.GetAxisRaw("Mouse X") * lookSensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * lookSensitivity;
            if (Mathf.Abs(mx) < 1e-5f && Mathf.Abs(my) < 1e-5f) return;

            Transform cam = inspectCamera.transform;
            cam.Rotate(-my, mx, 0f, Space.Self);

            // Kill roll so the horizon stays stable relative to painting up.
            Vector3 forward = cam.forward;
            Vector3 up = surfaceUp;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = surfaceRight;
            }

            cam.rotation = Quaternion.LookRotation(forward, up);
        }

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 1e-5f || !hasPivot) return;

            Vector3 offset = inspectCamera.transform.position - pivot;
            float current = offset.magnitude;
            if (current < 1e-5f)
            {
                offset = surfaceNormal * distance;
                current = distance;
            }

            float next = Mathf.Clamp(current - scroll * zoomSensitivity, minDistance, maxDistance);
            distance = next;
            inspectCamera.transform.position = pivot + offset.normalized * next;
        }

        private void UpdatePivotFromLookRay()
        {
            if (!RefreshSurface()) return;

            Ray ray = new Ray(inspectCamera.transform.position, inspectCamera.transform.forward);
            if (TryRaycastPainting(ray, out Vector3 hit, clampToBounds: true))
            {
                pivot = hit;
                hasPivot = true;
            }
        }

        private void PlaceFrontal(Vector3 targetPivot, float standOff, bool immediate)
        {
            RefreshSurface();
            Vector3 pos = targetPivot + surfaceNormal * standOff;
            Quaternion rot = Quaternion.LookRotation(-surfaceNormal, surfaceUp);

            if (immediate || inspectCamera == null)
            {
                inspectCamera.transform.SetPositionAndRotation(pos, rot);
                return;
            }

            inspectCamera.transform.SetPositionAndRotation(pos, rot);
        }

        private bool RefreshSurface()
        {
            if (canvasMesh == null) return false;

            Bounds orig = canvasMesh.OriginalBounds;
            if (orig.size.sqrMagnitude < 1e-10f && !canvasMesh.IsInitialized)
            {
                canvasMesh.InitializeMeshes(forceRebind: true);
                orig = canvasMesh.OriginalBounds;
            }

            Mesh3DSlicer.CalculateTargetBounds(
                canvasMesh.OriginalBounds,
                canvasMesh.SizeMeters,
                canvasMesh.Pivot,
                canvasMesh.CustomPivot,
                canvasMesh.Borders,
                out Vector3 min,
                out Vector3 max
            );

            Vector3 localCenter = new Vector3(
                Mathf.Lerp(min.x, max.x, 0.5f),
                Mathf.Lerp(min.y, max.y, 0.5f),
                Mathf.Lerp(min.z, max.z, faceDepth)
            );

            Transform tx = canvasMesh.transform;
            surfaceCenter = tx.TransformPoint(localCenter);
            surfaceRight = tx.TransformDirection(Vector3.right).normalized;
            surfaceUp = tx.TransformDirection(Vector3.up).normalized;
            surfaceNormal = tx.TransformDirection(Vector3.forward).normalized;

            // Ensure normal points toward the room / away from painting back.
            // If the main camera exists, flip so it faces roughly toward the viewer side.
            if (mainCamera != null)
            {
                Vector3 toViewer = (mainCamera.transform.position - surfaceCenter).normalized;
                if (Vector3.Dot(surfaceNormal, toViewer) < 0f)
                {
                    surfaceNormal = -surfaceNormal;
                }
            }

            halfWidth = Vector3.Distance(
                tx.TransformPoint(new Vector3(min.x, localCenter.y, localCenter.z)),
                tx.TransformPoint(new Vector3(max.x, localCenter.y, localCenter.z))) * 0.5f;
            halfHeight = Vector3.Distance(
                tx.TransformPoint(new Vector3(localCenter.x, min.y, localCenter.z)),
                tx.TransformPoint(new Vector3(localCenter.x, max.y, localCenter.z))) * 0.5f;

            return true;
        }

        private bool TryRaycastPainting(Ray ray, out Vector3 hit, bool clampToBounds)
        {
            hit = default;
            float denom = Vector3.Dot(ray.direction, surfaceNormal);
            if (Mathf.Abs(denom) < 1e-6f) return false;

            float t = Vector3.Dot(surfaceCenter - ray.origin, surfaceNormal) / denom;
            if (t < 0f) return false;

            Vector3 point = ray.origin + ray.direction * t;
            Vector3 local = point - surfaceCenter;
            float x = Vector3.Dot(local, surfaceRight);
            float y = Vector3.Dot(local, surfaceUp);

            bool inside = Mathf.Abs(x) <= halfWidth && Mathf.Abs(y) <= halfHeight;
            if (!inside && !clampToBounds)
            {
                return false;
            }

            if (clampToBounds)
            {
                x = Mathf.Clamp(x, -halfWidth, halfWidth);
                y = Mathf.Clamp(y, -halfHeight, halfHeight);
            }

            hit = surfaceCenter + surfaceRight * x + surfaceUp * y;
            return true;
        }

        private void SetInspectActive(bool enabled, bool snap)
        {
            if (inspectCamera != null)
            {
                inspectCamera.enabled = enabled;
                inspectCamera.gameObject.SetActive(true);
            }

            if (mainCamera != null)
            {
                mainCamera.enabled = !enabled;
            }

            if (mainListener != null) mainListener.enabled = !enabled;
            if (inspectListener != null) inspectListener.enabled = enabled;

            SetCursorLocked(enabled && lockCursorWhileInspecting);
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void CancelPan()
        {
            if (panRoutine != null)
            {
                StopCoroutine(panRoutine);
                panRoutine = null;
            }

            isPanning = false;
        }

        private void EnsurePivotMarker()
        {
            if (!showPivotMarker) return;
            if (pivotMarker != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "InspectPivotMarker";
            go.hideFlags = HideFlags.DontSave;
            Object.Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Unlit/Color"));
                renderer.sharedMaterial.color = new Color(1f, 0.85f, 0.2f, 1f);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            pivotMarker = go.transform;
            pivotMarker.localScale = Vector3.one * pivotMarkerScale;
            pivotMarker.gameObject.SetActive(false);
        }

        private void UpdatePivotMarker()
        {
            if (pivotMarker == null)
            {
                if (showPivotMarker) EnsurePivotMarker();
                else return;
            }

            bool show = showPivotMarker && isInspecting && hasPivot;
            pivotMarker.gameObject.SetActive(show);
            if (show)
            {
                pivotMarker.position = pivot;
                pivotMarker.localScale = Vector3.one * pivotMarkerScale;
            }
        }

        private void ResolveRefs()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (canvasMesh == null)
            {
                var canvas = GetComponent<CanvasController>();
                if (canvas != null) canvasMesh = canvas.CanvasMesh;
                if (canvasMesh == null) canvasMesh = GetComponentInChildren<NineSliceMesh3D>(true);
            }

            if (inspectCamera == null)
            {
                var cameras = GetComponentsInChildren<Camera>(true);
                foreach (var cam in cameras)
                {
                    if (cam != mainCamera)
                    {
                        inspectCamera = cam;
                        break;
                    }
                }
            }

            if (mainCamera != null && mainListener == null)
            {
                mainListener = mainCamera.GetComponent<AudioListener>();
            }

            if (inspectCamera != null && inspectListener == null)
            {
                inspectListener = inspectCamera.GetComponent<AudioListener>();
                if (inspectListener == null)
                {
                    inspectListener = inspectCamera.gameObject.AddComponent<AudioListener>();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            if (!Application.isPlaying && canvasMesh != null)
            {
                RefreshSurface();
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(surfaceCenter, surfaceCenter + surfaceNormal * 0.3f);
            Gizmos.DrawWireSphere(surfaceCenter, 0.02f);

            if (hasPivot)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pivot, 0.02f);
                if (inspectCamera != null)
                {
                    Gizmos.DrawLine(inspectCamera.transform.position, pivot);
                }
            }
        }
    }
}
