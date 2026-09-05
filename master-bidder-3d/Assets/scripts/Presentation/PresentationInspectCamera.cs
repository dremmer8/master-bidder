using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Alternative inspect camera for close-up painting viewing.
    /// RMB toggles mode. Mouse look moves a surface pivot via look-ray.
    /// Scroll zooms smoothly toward the pivot, clamped by artwork texel size.
    /// LMB locks the current pivot once and smoothly reframes to face it; look does not retarget that pivot.
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
        [Tooltip("Hard floor for zoom. Effective min is raised further from artwork texel size.")]
        [SerializeField] private float minDistance = 0.2f;
        [SerializeField] private float maxDistance = 2.5f;
        [Tooltip("Stop zooming in once a texture texel covers this many screen pixels (larger = allow closer).")]
        [SerializeField] private float maxPixelsPerTexel = 1.5f;

        [Header("Controls")]
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float zoomSensitivity = 0.35f;
        [SerializeField] private float zoomSmoothSpeed = 7f;
        [SerializeField] private float panSmoothSpeed = 9f;
        [SerializeField] private bool lockCursorWhileInspecting = true;
        [SerializeField] private bool showPivotMarker = true;
        [SerializeField] private float pivotMarkerScale = 0.008333f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private bool isInspecting;
        private bool hasPivot;
        private Vector3 pivot;
        private float distance;
        private float targetDistance;
        private float effectiveMinDistance;
        private bool isPanning;
        private float reframeTimeout;
        private Vector3 reframePivot;
        private Transform pivotMarker;
        private AudioListener mainListener;
        private AudioListener inspectListener;

        private Vector3 surfaceNormal = Vector3.forward;
        private Vector3 surfaceUp = Vector3.up;
        private Vector3 surfaceRight = Vector3.right;
        private Vector3 surfaceCenter;
        private float halfWidth;
        private float halfHeight;

        private const float ZoomSettleEpsilon = 0.001f;
        private const float ZoomLimitHysteresis = 0.005f;

        public bool IsInspecting => isInspecting;
        public Vector3 Pivot => pivot;
        public Camera ActiveCamera => isInspecting ? inspectCamera : mainCamera;

        private void Awake()
        {
            ResolveRefs();
            EnsurePivotMarker();
            effectiveMinDistance = Mathf.Max(0.01f, minDistance);
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

            HandleLook();
            HandleZoomInput();
            ApplySmoothZoom();

            // Live look→pivot tracking only outside reframe.
            if (!isPanning)
            {
                UpdatePivotFromLookRay();
            }

            if (Input.GetMouseButtonDown(0))
            {
                // One-shot sample at click time (also retargets mid-reframe).
                UpdatePivotFromLookRay();
                if (hasPivot)
                {
                    PanToFacePivot();
                }
            }

            if (isPanning)
            {
                ApplyLockedReframe();
            }

            ConstrainLookTowardPainting();
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
            RefreshZoomLimits();
            distance = targetDistance = Mathf.Clamp(enterDistance, effectiveMinDistance, maxDistance);

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

            // Freeze the aim point at click time — look will not move it during reframe.
            reframePivot = pivot;
            isPanning = true;
            reframeTimeout = 1.8f;
        }

        /// <summary>
        /// Softly relocates to a frontal view of the pivot locked at LMB press.
        /// Look/zoom still work and blend with the move; they do not change the target point.
        /// </summary>
        private void ApplyLockedReframe()
        {
            if (inspectCamera == null)
            {
                isPanning = false;
                return;
            }

            pivot = reframePivot;
            hasPivot = true;

            RefreshZoomLimits();
            float standOff = Mathf.Clamp(distance, effectiveMinDistance, maxDistance);
            distance = targetDistance = standOff;

            Vector3 endPos = reframePivot + surfaceNormal * standOff;
            Quaternion frontalRot = Quaternion.LookRotation(-surfaceNormal, surfaceUp);

            Transform cam = inspectCamera.transform;
            float eased = 1f - Mathf.Exp(-panSmoothSpeed * Time.deltaTime);

            cam.position = Vector3.Lerp(cam.position, endPos, eased);

            float posError = Vector3.Distance(cam.position, endPos);
            // Release as soon as the body arrives — don't keep steering look for a long tail.
            const float posSettle = 0.012f;
            if (posError <= posSettle)
            {
                cam.position = endPos;
                if (Quaternion.Angle(cam.rotation, frontalRot) < 8f)
                {
                    cam.rotation = frontalRot;
                }

                distance = targetDistance = standOff;
                isPanning = false;
                return;
            }

            // Aim at the locked pivot while traveling; settle toward frontal as we arrive.
            Vector3 toPivot = reframePivot - cam.position;
            Quaternion aimRot = toPivot.sqrMagnitude > 1e-8f
                ? Quaternion.LookRotation(toPivot.normalized, surfaceUp)
                : frontalRot;

            float arrive = 1f - Mathf.Clamp01(posError / Mathf.Max(standOff, 0.05f));
            Quaternion endRot = Quaternion.Slerp(aimRot, frontalRot, arrive * arrive);
            cam.rotation = Quaternion.Slerp(cam.rotation, endRot, eased);

            Vector3 forward = cam.forward;
            Vector3 up = surfaceUp;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = surfaceRight;
            }

            cam.rotation = Quaternion.LookRotation(forward, up);

            reframeTimeout -= Time.deltaTime;
            if (reframeTimeout > 0f) return;

            isPanning = false;
        }

        private void HandleLook()
        {
            float mx = Input.GetAxisRaw("Mouse X") * lookSensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * lookSensitivity;
            if (Mathf.Abs(mx) < 1e-5f && Mathf.Abs(my) < 1e-5f) return;

            Transform cam = inspectCamera.transform;
            cam.Rotate(-my, mx, 0f, Space.Self);
            StabilizeLookRoll(cam);
            ConstrainLookTowardPainting();
        }

        /// <summary>
        /// Keeps the inspect camera aimed at the painting — no free look into the room.
        /// Look may travel across the canvas; past the edges it sticks to the rim.
        /// </summary>
        private void ConstrainLookTowardPainting()
        {
            if (inspectCamera == null) return;
            if (!RefreshSurface()) return;

            Transform cam = inspectCamera.transform;
            Ray ray = new Ray(cam.position, cam.forward);

            // Still looking at the canvas interior — allow it.
            if (TryRaycastPainting(ray, out _, clampToBounds: false))
            {
                StabilizeLookRoll(cam);
                return;
            }

            // Past the rim or facing away: pull aim back onto the painting.
            Vector3 aim;
            if (TryRaycastPainting(ray, out Vector3 edgeHit, clampToBounds: true))
            {
                aim = edgeHit;
            }
            else if (hasPivot)
            {
                aim = isPanning ? reframePivot : pivot;
            }
            else
            {
                aim = surfaceCenter;
            }

            Vector3 toAim = aim - cam.position;
            if (toAim.sqrMagnitude < 1e-8f) return;

            cam.rotation = Quaternion.LookRotation(toAim.normalized, surfaceUp);
            StabilizeLookRoll(cam);
        }

        private void StabilizeLookRoll(Transform cam)
        {
            Vector3 forward = cam.forward;
            Vector3 up = surfaceUp;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = surfaceRight;
            }

            cam.rotation = Quaternion.LookRotation(forward, up);
        }

        private void HandleZoomInput()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 1e-5f || !hasPivot) return;

            RefreshZoomLimits();

            // Already hard-stopped at the close limit — ignore further zoom-in so we don't
            // keep re-triggering tiny corrections that read as camera jitter.
            bool atMin = distance <= effectiveMinDistance + ZoomSettleEpsilon
                         && targetDistance <= effectiveMinDistance + ZoomSettleEpsilon;
            if (scroll > 0f && atMin) return;

            bool atMax = distance >= maxDistance - ZoomSettleEpsilon
                         && targetDistance >= maxDistance - ZoomSettleEpsilon;
            if (scroll < 0f && atMax) return;

            targetDistance = Mathf.Clamp(
                targetDistance - scroll * zoomSensitivity,
                effectiveMinDistance,
                maxDistance);
        }

        private void ApplySmoothZoom()
        {
            if (inspectCamera == null) return;

            targetDistance = Mathf.Clamp(targetDistance, effectiveMinDistance, maxDistance);

            float remaining = targetDistance - distance;
            if (Mathf.Abs(remaining) <= ZoomSettleEpsilon)
            {
                distance = targetDistance;
                return;
            }

            float eased = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime);
            float next = distance + remaining * eased;
            if (Mathf.Abs(targetDistance - next) <= ZoomSettleEpsilon)
            {
                next = targetDistance;
            }

            // Move along the view axis only. Re-anchoring to a moving surface pivot
            // every frame caused visible jitter when hitting the zoom stop.
            float delta = distance - next;
            inspectCamera.transform.position += inspectCamera.transform.forward * delta;
            distance = next;
        }

        private void RefreshZoomLimits()
        {
            float computed = ComputeTexelLimitedMinDistance();
            if (effectiveMinDistance < 0.01f
                || Mathf.Abs(computed - effectiveMinDistance) > ZoomLimitHysteresis)
            {
                effectiveMinDistance = computed;
            }

            if (targetDistance > 0f && targetDistance < effectiveMinDistance)
            {
                targetDistance = effectiveMinDistance;
            }
        }

        /// <summary>
        /// Closest allowed stand-off so a painting texel does not exceed
        /// <see cref="maxPixelsPerTexel"/> on screen, based on real canvas size / texture resolution.
        /// </summary>
        private float ComputeTexelLimitedMinDistance()
        {
            float floor = Mathf.Max(0.01f, minDistance);
            if (inspectCamera == null) return floor;

            if (!TryResolveArtworkMetrics(out float widthM, out float heightM, out int texW, out int texH))
            {
                return floor;
            }

            float texelM_X = widthM / texW;
            float texelM_Y = heightM / texH;

            float vFovRad = inspectCamera.fieldOfView * Mathf.Deg2Rad;
            float halfV = Mathf.Tan(vFovRad * 0.5f);
            if (halfV < 1e-6f) return floor;

            float aspect = Mathf.Max(1e-4f, inspectCamera.aspect);
            float halfH = halfV * aspect;

            float pxPerTexel = Mathf.Max(0.25f, maxPixelsPerTexel);
            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);

            // Distance where one texel spans maxPixelsPerTexel screen pixels.
            float dFromY = (texelM_Y * screenH) / (pxPerTexel * 2f * halfV);
            float dFromX = (texelM_X * screenW) / (pxPerTexel * 2f * halfH);

            return Mathf.Max(floor, dFromX, dFromY);
        }

        private bool TryResolveArtworkMetrics(out float widthM, out float heightM, out int texW, out int texH)
        {
            widthM = halfWidth * 2f;
            heightM = halfHeight * 2f;
            texW = 0;
            texH = 0;

            PaintingData painting = canvasMesh != null ? canvasMesh.ActivePainting : null;
            if (painting != null)
            {
                Vector3 sizeM = painting.SizeInMeters;
                if (sizeM.x > 1e-4f && sizeM.y > 1e-4f)
                {
                    widthM = sizeM.x;
                    heightM = sizeM.y;
                }

                if (painting.albedoTexture != null)
                {
                    texW = painting.albedoTexture.width;
                    texH = painting.albedoTexture.height;
                }
            }

            if (texW < 1 || texH < 1)
            {
                Texture tex = ResolveArtworkTexture(painting);
                if (tex != null)
                {
                    texW = tex.width;
                    texH = tex.height;
                }
            }

            return widthM > 1e-4f && heightM > 1e-4f && texW >= 1 && texH >= 1;
        }

        private Texture ResolveArtworkTexture(PaintingData painting)
        {
            if (painting != null && painting.albedoTexture != null)
            {
                return painting.albedoTexture;
            }

            if (canvasMesh == null) return null;

            int slot = painting != null ? painting.canvasMaterialSlotIndex : 0;
            string prop = painting != null ? painting.texturePropertyName : "_MainTex";

            MeshRenderer rend = canvasMesh.GetComponentInChildren<MeshRenderer>();
            if (rend == null || rend.sharedMaterials == null) return null;

            Material[] mats = rend.sharedMaterials;
            if (slot < 0 || slot >= mats.Length) return null;

            Material mat = mats[slot];
            if (mat == null) return null;

            if (!string.IsNullOrEmpty(prop) && mat.HasProperty(prop))
            {
                return mat.GetTexture(prop);
            }

            if (mat.HasProperty("_BaseMap"))
            {
                return mat.GetTexture("_BaseMap");
            }

            if (mat.HasProperty("_MainTex"))
            {
                return mat.GetTexture("_MainTex");
            }

            return mat.mainTexture;
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
            isPanning = false;
            reframeTimeout = 0f;
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
