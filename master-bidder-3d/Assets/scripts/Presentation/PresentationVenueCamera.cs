using System;
using MasterBidder.Core;
using UnityEngine;

namespace MasterBidder.Presentation
{
    public enum VenueId
    {
        Hall,
        Banquet,
        Warehouse
    }

    /// <summary>
    /// Smooth Hall ↔ Banquet ↔ Warehouse blends via a proxy camera
    /// (same smoothstep hand-off pattern as <see cref="PresentationInspectCamera"/>).
    /// Destination cameras stay put; only the proxy travels.
    /// </summary>
    public class PresentationVenueCamera : MonoBehaviour
    {
        [Header("Cameras")]
        [SerializeField] Camera hallCamera;
        [SerializeField] Camera banquetCamera;
        [SerializeField] Camera warehouseCamera;
        [Tooltip("Optional. Auto-created at runtime if left empty.")]
        [SerializeField] Camera proxyCamera;

        [Header("Blend")]
        [SerializeField] float transitionDuration = 0.65f;

        VenueId current = VenueId.Hall;
        VenueId pending = VenueId.Hall;
        bool transitioning;
        float transitionT;
        Vector3 startPos;
        Vector3 endPos;
        Quaternion startRot;
        Quaternion endRot;
        float startFov;
        float endFov;
        Action onArrived;

        AudioListener hallListener;
        AudioListener banquetListener;
        AudioListener warehouseListener;
        AudioListener proxyListener;

        public bool IsTransitioning => transitioning;
        public VenueId Current => current;

        public static VenueId VenueForScreen(GameScreen screen)
        {
            switch (screen)
            {
                case GameScreen.Brief:
                    return VenueId.Banquet;
                case GameScreen.Report:
                    return VenueId.Warehouse;
                default:
                    return VenueId.Hall;
            }
        }

        void Awake()
        {
            ResolveRefs();
            EnsureProxyCamera();
            CacheListeners();
            // Inspect camera also snaps to hall on Awake — stay consistent until AppFlow picks a screen.
            ApplyRig(VenueId.Hall, snapListeners: true);
            current = VenueId.Hall;
        }

        void Update()
        {
            if (!Application.isPlaying || !transitioning) return;
            TickTransition();
        }

        public void GoToForScreen(GameScreen screen, bool snap = false, Action onComplete = null)
        {
            GoTo(VenueForScreen(screen), snap, onComplete);
        }

        public void GoTo(VenueId venue, bool snap = false, Action onComplete = null)
        {
            ResolveRefs();
            EnsureProxyCamera();
            CacheListeners();

            var target = ResolveCamera(venue);
            if (target == null)
            {
                Debug.LogWarning($"[VenueCamera] Missing camera for {venue}.", this);
                onComplete?.Invoke();
                return;
            }

            if (!transitioning && current == venue && target.enabled)
            {
                onComplete?.Invoke();
                return;
            }

            onArrived = onComplete;
            pending = venue;

            if (snap || transitionDuration <= 1e-4f || proxyCamera == null)
            {
                FinishAt(venue);
                return;
            }

            var fromCam = FindActiveSource() ?? ResolveCamera(current) ?? hallCamera;
            if (fromCam == null)
            {
                FinishAt(venue);
                return;
            }

            startPos = fromCam.transform.position;
            startRot = fromCam.transform.rotation;
            startFov = fromCam.fieldOfView;
            endPos = target.transform.position;
            endRot = target.transform.rotation;
            endFov = target.fieldOfView;

            CopyCameraLens(fromCam, proxyCamera);
            proxyCamera.transform.SetPositionAndRotation(startPos, startRot);
            proxyCamera.fieldOfView = startFov;

            SetAllCamerasEnabled(false);
            proxyCamera.gameObject.SetActive(true);
            proxyCamera.enabled = true;
            SetListener(proxyListener, true);

            transitionT = 0f;
            transitioning = true;
        }

        void TickTransition()
        {
            float duration = Mathf.Max(0.05f, transitionDuration);
            transitionT = Mathf.Min(1f, transitionT + Time.deltaTime / duration);
            float u = transitionT * transitionT * (3f - 2f * transitionT);

            proxyCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, endPos, u),
                Quaternion.Slerp(startRot, endRot, u));
            proxyCamera.fieldOfView = Mathf.Lerp(startFov, endFov, u);

            if (transitionT >= 1f - 1e-4f)
                FinishAt(pending);
        }

        void FinishAt(VenueId venue)
        {
            transitioning = false;
            current = venue;
            ApplyRig(venue, snapListeners: true);

            var cb = onArrived;
            onArrived = null;
            cb?.Invoke();
        }

        void ApplyRig(VenueId venue, bool snapListeners)
        {
            SetAllCamerasEnabled(false);
            var cam = ResolveCamera(venue);
            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
            }

            if (snapListeners)
            {
                SetListener(hallListener, venue == VenueId.Hall);
                SetListener(banquetListener, venue == VenueId.Banquet);
                SetListener(warehouseListener, venue == VenueId.Warehouse);
                SetListener(proxyListener, false);
            }
        }

        void SetAllCamerasEnabled(bool enabled)
        {
            if (hallCamera != null) hallCamera.enabled = enabled;
            if (banquetCamera != null) banquetCamera.enabled = enabled;
            if (warehouseCamera != null) warehouseCamera.enabled = enabled;
            if (proxyCamera != null) proxyCamera.enabled = enabled;
        }

        Camera ResolveCamera(VenueId venue)
        {
            switch (venue)
            {
                case VenueId.Banquet: return banquetCamera;
                case VenueId.Warehouse: return warehouseCamera;
                default: return hallCamera;
            }
        }

        Camera FindActiveSource()
        {
            if (proxyCamera != null && proxyCamera.enabled) return proxyCamera;
            if (banquetCamera != null && banquetCamera.enabled) return banquetCamera;
            if (warehouseCamera != null && warehouseCamera.enabled) return warehouseCamera;
            if (hallCamera != null && hallCamera.enabled) return hallCamera;
            return null;
        }

        void ResolveRefs()
        {
            if (hallCamera == null)
            {
                var main = Camera.main;
                if (main != null) hallCamera = main;
            }

            if (banquetCamera == null)
            {
                var go = GameObject.Find("banquete Camera");
                if (go != null) banquetCamera = go.GetComponent<Camera>();
            }

            if (warehouseCamera == null)
            {
                var go = GameObject.Find("warehouse Camera (1)");
                if (go == null) go = GameObject.Find("warehouse Camera");
                if (go != null) warehouseCamera = go.GetComponent<Camera>();
            }
        }

        void EnsureProxyCamera()
        {
            if (proxyCamera != null) return;
            if (hallCamera == null) ResolveRefs();
            if (hallCamera == null) return;

            var go = new GameObject("VenueProxyCamera");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.hideFlags = HideFlags.DontSave;
            proxyCamera = go.AddComponent<Camera>();
            proxyListener = go.AddComponent<AudioListener>();
            proxyListener.enabled = false;
            proxyCamera.enabled = false;
            CopyCameraLens(hallCamera, proxyCamera);
            proxyCamera.depth = hallCamera.depth + 2f;
            proxyCamera.tag = "Untagged";
        }

        void CacheListeners()
        {
            hallListener = EnsureListener(hallCamera, hallListener);
            banquetListener = EnsureListener(banquetCamera, banquetListener);
            warehouseListener = EnsureListener(warehouseCamera, warehouseListener);
            if (proxyCamera != null && proxyListener == null)
                proxyListener = proxyCamera.GetComponent<AudioListener>();
        }

        static AudioListener EnsureListener(Camera cam, AudioListener existing)
        {
            if (cam == null) return existing;
            if (existing != null) return existing;
            var listener = cam.GetComponent<AudioListener>();
            if (listener == null)
                listener = cam.gameObject.AddComponent<AudioListener>();
            listener.enabled = false;
            return listener;
        }

        static void SetListener(AudioListener listener, bool enabled)
        {
            if (listener != null) listener.enabled = enabled;
        }

        static void CopyCameraLens(Camera from, Camera to)
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
        }
    }
}
