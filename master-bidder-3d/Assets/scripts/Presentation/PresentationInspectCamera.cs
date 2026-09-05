using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Presentation
{
    /// <summary>
    /// Inspect camera for close-up painting viewing.
    /// Hall ↔ inspect blends through a proxy camera.
    /// Scroll-up from the hall enters inspect; scroll-down at max stand-off returns to the hall.
    /// RMB also toggles. Mouse look moves a surface pivot; LMB reframes to the locked pivot.
    /// </summary>
    public class PresentationInspectCamera : MonoBehaviour
    {
        private enum CameraMode
        {
            Hall,
            ToInspect,
            Inspect,
            ToHall
        }

        [Header("Cameras")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera inspectCamera;
        [Tooltip("Optional. Auto-created at runtime if left empty.")]
        [SerializeField] private Camera proxyCamera;

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
        [SerializeField] private float transitionDuration = 0.65f;
        [SerializeField] private bool lockCursorWhileInspecting = true;
        [SerializeField] private bool showPivotMarker = true;
        [SerializeField] private float pivotMarkerScale = 0.008333f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private CameraMode mode = CameraMode.Hall;
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
        private AudioListener proxyListener;

        private float transitionT;
        private Vector3 transitionStartPos;
        private Vector3 transitionEndPos;
        private Quaternion transitionStartRot;
        private Quaternion transitionEndRot;
        private float transitionStartFov;
        private float transitionEndFov;

        private Vector3 surfaceNormal = Vector3.forward;
        private Vector3 surfaceUp = Vector3.up;
        private Vector3 surfaceRight = Vector3.right;
        private Vector3 surfaceCenter;
        private float halfWidth;
        private float halfHeight;

        private const float ZoomSettleEpsilon = 0.001f;
        private const float ZoomLimitHysteresis = 0.005f;

        public bool IsInspecting => mode == CameraMode.Inspect || mode == CameraMode.ToInspect;
        public bool IsTransitioning => mode == CameraMode.ToInspect || mode == CameraMode.ToHall;
        public Vector3 Pivot => pivot;

        public Camera ActiveCamera
        {
            get
            {
                if (IsTransitioning) return proxyCamera != null ? proxyCamera : inspectCamera;
                return mode == CameraMode.Inspect ? inspectCamera : mainCamera;
            }
        }

        private void Awake()
        {
            ResolveRefs();
            EnsureProxyCamera();
            EnsurePivotMarker();
            effectiveMinDistance = Mathf.Max(0.01f, minDistance);
            ApplyCameraRig(CameraMode.Hall, snapListeners: true);
            mode = CameraMode.Hall;
        }

        private void OnDisable()
        {
            if (mode != CameraMode.Hall)
            {
                ExitInspect(snap: true);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (Input.GetMouseButtonDown(1) && !IsTransitioning)
            {
                ToggleInspect();
            }

            if (mode == CameraMode.Hall)
            {
                // Scroll-up from the hall enters inspect with a blended hand-off.
                if (Input.mouseScrollDelta.y > 0f)
                {
                    EnterInspect(snap: false);
                }

                return;
            }

            if (IsTransitioning)
            {
                TickTransition();
                return;
            }

            if (mode != CameraMode.Inspect || inspectCamera == null) return;

            HandleLook();
            HandleZoomInput();
            if (mode != CameraMode.Inspect) return;

            ApplySmoothZoom();

            if (!isPanning)
            {
                UpdatePivotFromLookRay();
            }

            if (Input.GetMouseButtonDown(0))
            {
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
            if (IsTransitioning) return;

            if (mode == CameraMode.Inspect)
            {
                ExitInspect(snap: false);
            }
            else if (mode == CameraMode.Hall)
            {
                EnterInspect(snap: false);
            }
        }

        public void EnterInspect() => EnterInspect(snap: false);

        public void EnterInspect(bool snap)
        {
            if (mode == CameraMode.Inspect || mode == CameraMode.ToInspect) return;
            if (!snap && IsTransitioning) return;

            ResolveRefs();
            EnsureProxyCamera();
            if (inspectCamera == null || canvasMesh == null || mainCamera == null)
            {
                Debug.LogWarning("[InspectCamera] Missing cameras or canvas.", this);
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

            GetFrontalPose(pivot, distance, out Vector3 endPos, out Quaternion endRot);
            float endFov = inspectCamera.fieldOfView;

            if (snap || transitionDuration <= 1e-4f || proxyCamera == null)
            {
                inspectCamera.transform.SetPositionAndRotation(endPos, endRot);
                ApplyCameraRig(CameraMode.Inspect, snapListeners: true);
                mode = CameraMode.Inspect;
                SetCursorLocked(lockCursorWhileInspecting);
                UpdatePivotMarker();
                return;
            }

            transitionStartPos = mainCamera.transform.position;
            transitionStartRot = mainCamera.transform.rotation;
            transitionStartFov = mainCamera.fieldOfView;
            transitionEndPos = endPos;
            transitionEndRot = endRot;
            transitionEndFov = endFov;
            transitionT = 0f;

            inspectCamera.transform.SetPositionAndRotation(endPos, endRot);
            CopyCameraLens(mainCamera, proxyCamera);
            proxyCamera.transform.SetPositionAndRotation(transitionStartPos, transitionStartRot);
            proxyCamera.fieldOfView = transitionStartFov;

            ApplyCameraRig(CameraMode.ToInspect, snapListeners: true);
            mode = CameraMode.ToInspect;
            SetCursorLocked(lockCursorWhileInspecting);
            UpdatePivotMarker();
        }

        public void ExitInspect(bool snap)
        {
            CancelPan();
            hasPivot = false;
            UpdatePivotMarker();

            if (mode == CameraMode.Hall)
            {
                SetCursorLocked(false);
                return;
            }

            if (!snap && mode == CameraMode.ToHall) return;

            ResolveRefs();
            EnsureProxyCamera();

            if (snap || transitionDuration <= 1e-4f || proxyCamera == null || mainCamera == null)
            {
                ApplyCameraRig(CameraMode.Hall, snapListeners: true);
                mode = CameraMode.Hall;
                SetCursorLocked(false);
                return;
            }

            Camera fromCam = mode == CameraMode.ToInspect && proxyCamera != null && proxyCamera.enabled
                ? proxyCamera
                : inspectCamera;
            if (fromCam == null) fromCam = proxyCamera != null ? proxyCamera : mainCamera;

            transitionStartPos = fromCam.transform.position;
            transitionStartRot = fromCam.transform.rotation;
            transitionStartFov = fromCam.fieldOfView;
            transitionEndPos = mainCamera.transform.position;
            transitionEndRot = mainCamera.transform.rotation;
            transitionEndFov = mainCamera.fieldOfView;
            transitionT = 0f;

            CopyCameraLens(fromCam, proxyCamera);
            proxyCamera.transform.SetPositionAndRotation(transitionStartPos, transitionStartRot);
            proxyCamera.fieldOfView = transitionStartFov;

            ApplyCameraRig(CameraMode.ToHall, snapListeners: true);
            mode = CameraMode.ToHall;
            SetCursorLocked(false);
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

            bool atMin = distance <= effectiveMinDistance + ZoomSettleEpsilon
                         && targetDistance <= effectiveMinDistance + ZoomSettleEpsilon;
            if (scroll > 0f && atMin) return;

            bool atMax = distance >= maxDistance - ZoomSettleEpsilon
                         && targetDistance >= maxDistance - ZoomSettleEpsilon;
            // Scroll further out at max stand-off → blend back to the hall.
            if (scroll < 0f && atMax)
            {
                ExitInspect(snap: false);
                return;
            }

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
            GetFrontalPose(targetPivot, standOff, out Vector3 pos, out Quaternion rot);

            if (immediate || inspectCamera == null)
            {
                inspectCamera.transform.SetPositionAndRotation(pos, rot);
                return;
            }

            inspectCamera.transform.SetPositionAndRotation(pos, rot);
        }

        private void GetFrontalPose(Vector3 targetPivot, float standOff, out Vector3 pos, out Quaternion rot)
        {
            RefreshSurface();
            pos = targetPivot + surfaceNormal * standOff;
            rot = Quaternion.LookRotation(-surfaceNormal, surfaceUp);
        }

        private void TickTransition()
        {
            if (proxyCamera == null)
            {
                FinishTransition();
                return;
            }

            float duration = Mathf.Max(0.05f, transitionDuration);
            transitionT = Mathf.Min(1f, transitionT + Time.deltaTime / duration);
            float u = transitionT * transitionT * (3f - 2f * transitionT); // smoothstep

            proxyCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(transitionStartPos, transitionEndPos, u),
                Quaternion.Slerp(transitionStartRot, transitionEndRot, u));
            proxyCamera.fieldOfView = Mathf.Lerp(transitionStartFov, transitionEndFov, u);

            if (transitionT >= 1f - 1e-4f)
            {
                FinishTransition();
            }
        }

        private void FinishTransition()
        {
            if (mode == CameraMode.ToInspect)
            {
                if (inspectCamera != null)
                {
                    inspectCamera.transform.SetPositionAndRotation(transitionEndPos, transitionEndRot);
                    inspectCamera.fieldOfView = transitionEndFov;
                }

                ApplyCameraRig(CameraMode.Inspect, snapListeners: true);
                mode = CameraMode.Inspect;
                SetCursorLocked(lockCursorWhileInspecting);
                UpdatePivotMarker();
                return;
            }

            if (mode == CameraMode.ToHall)
            {
                ApplyCameraRig(CameraMode.Hall, snapListeners: true);
                mode = CameraMode.Hall;
                SetCursorLocked(false);
                UpdatePivotMarker();
            }
        }

        private void ApplyCameraRig(CameraMode rig, bool snapListeners)
        {
            bool hall = rig == CameraMode.Hall;
            bool inspect = rig == CameraMode.Inspect;
            bool proxy = rig == CameraMode.ToInspect || rig == CameraMode.ToHall;

            if (mainCamera != null) mainCamera.enabled = hall;
            if (inspectCamera != null)
            {
                inspectCamera.gameObject.SetActive(true);
                inspectCamera.enabled = inspect;
            }

            if (proxyCamera != null)
            {
                proxyCamera.gameObject.SetActive(true);
                proxyCamera.enabled = proxy;
            }

            if (snapListeners)
            {
                if (mainListener != null) mainListener.enabled = hall;
                if (inspectListener != null) inspectListener.enabled = inspect;
                if (proxyListener != null) proxyListener.enabled = proxy;
            }
        }

        private void EnsureProxyCamera()
        {
            if (proxyCamera != null)
            {
                if (proxyListener == null)
                {
                    proxyListener = proxyCamera.GetComponent<AudioListener>();
                }

                return;
            }

            ResolveRefs();
            if (mainCamera == null) return;

            var go = new GameObject("InspectProxyCamera");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.hideFlags = HideFlags.DontSave;
            proxyCamera = go.AddComponent<Camera>();
            proxyListener = go.AddComponent<AudioListener>();
            proxyListener.enabled = false;
            proxyCamera.enabled = false;
            CopyCameraLens(mainCamera, proxyCamera);
            proxyCamera.depth = mainCamera.depth + 1f;
            proxyCamera.tag = "Untagged";
        }

        private static void CopyCameraLens(Camera from, Camera to)
        {
            if (from == null || to == null) return;

            to.clearFlags = from.clearFlags;
            to.backgroundColor = from.backgroundColor;
            to.cullingMask = from.cullingMask;
            to.orthographic = from.orthographic;
            to.fieldOfView = from.fieldOfView;
            to.orthographicSize = from.orthographicSize;
            to.nearClipPlane = from.nearClipPlane;
            to.farClipPlane = from.farClipPlane;
            to.allowHDR = from.allowHDR;
            to.allowMSAA = from.allowMSAA;
            to.allowDynamicResolution = from.allowDynamicResolution;
            to.renderingPath = from.renderingPath;
            to.useOcclusionCulling = from.useOcclusionCulling;
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

            bool show = showPivotMarker && IsInspecting && hasPivot && mode == CameraMode.Inspect;
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
                    if (cam == mainCamera || cam == proxyCamera) continue;
                    if (cam.name.Contains("Proxy")) continue;
                    inspectCamera = cam;
                    break;
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
