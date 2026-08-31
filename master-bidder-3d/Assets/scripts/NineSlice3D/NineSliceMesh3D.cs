using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineSlice3D
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Mesh/9-Slice Mesh 3D")]
    public class NineSliceMesh3D : MonoBehaviour
    {
        [Serializable]
        public class SourceMeshBinding
        {
            public MeshFilter filter;
            public Mesh sourceMesh;
        }

        [Serializable]
        public class MeshData
        {
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Mesh sourceMesh;
            public Mesh instancedMesh;
            public Vector3[] originalVertices;
            public Vector3[] deformedVertices;
            public Matrix4x4 localToRoot;
            public Matrix4x4 rootToLocal;
        }

        [Header("Active Painting Config")]
        [Tooltip("Assign a PaintingData config asset to automatically set dimensions and albedo texture.")]
        [SerializeField] private PaintingData activePaintingConfig;

        [Header("Size & Units")]
        [Tooltip("The unit of measurement used in the inspector fields below.")]
        [SerializeField] private MeasurementUnit measurementUnit = MeasurementUnit.Centimeters;

        [Tooltip("Target size in the selected measurement unit.")]
        [SerializeField] private Vector3 targetSize = new Vector3(50f, 50f, 5f);

        [Header("Pivot Alignment")]
        [SerializeField] private PivotAnchor pivotAnchor = PivotAnchor.PreserveOriginalPivot;

        [Tooltip("Custom normalized pivot (0..1 on X, Y, Z). (0,0,0) is bottom-left-back, (0.5,0.5,0.5) is center, (1,1,1) is top-right-front.")]
        [SerializeField] private Vector3 customPivot = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("9-Slice Borders (Original Local Units)")]
        [SerializeField] private SliceBorder3D borders = SliceBorder3D.Default;

        [Header("Submesh & Texture Tiling (Texel Density)")]
        [Tooltip("Configure texture tiling scaling for specific submesh material slots to maintain constant texel density.")]
        [SerializeField] private List<SubmeshTilingConfig> submeshTilingConfigs = new List<SubmeshTilingConfig>();

        [Header("Options")]
        [Tooltip("If true, all MeshFilters in child objects will be deformed together in root local space.")]
        [SerializeField] private bool includeChildren = true;

        [Tooltip("Recalculate mesh normals after slicing deformation.")]
        [SerializeField] private bool recalculateNormals = true;

        [Tooltip("Recalculate mesh tangents after slicing deformation.")]
        [SerializeField] private bool recalculateTangents = false;

        [Tooltip("Update BoxCollider or MeshCollider if attached.")]
        [SerializeField] private bool updateColliders = true;

        [SerializeField, HideInInspector] private Bounds originalCombinedBounds;
        [SerializeField, HideInInspector] private bool isInitialized = false;
        [SerializeField, HideInInspector] private List<SourceMeshBinding> sourceMeshBindings = new List<SourceMeshBinding>();

        private readonly List<MeshData> meshDataList = new List<MeshData>();
        private MaterialPropertyBlock propertyBlock;
        private bool isDirty = false;

        #region Properties

        public PaintingData ActivePainting
        {
            get => activePaintingConfig;
            set
            {
                activePaintingConfig = value;
                if (activePaintingConfig != null)
                {
                    ApplyPaintingConfig(activePaintingConfig);
                }
            }
        }

        public MeasurementUnit DisplayUnit
        {
            get => measurementUnit;
            set
            {
                if (measurementUnit != value)
                {
                    Vector3 sizeInMeters = SizeMeters;
                    measurementUnit = value;
                    targetSize = Mesh3DSlicer.ConvertMetersToUnits(sizeInMeters, measurementUnit);
                }
            }
        }

        public Vector3 TargetSize
        {
            get => targetSize;
            set
            {
                targetSize = new Vector3(Mathf.Max(0.0001f, value.x), Mathf.Max(0.0001f, value.y), Mathf.Max(0.0001f, value.z));
                SetDirty();
            }
        }

        public Vector3 SizeMeters
        {
            get => Mesh3DSlicer.ConvertUnitsToMeters(targetSize, measurementUnit);
            set
            {
                targetSize = Mesh3DSlicer.ConvertMetersToUnits(value, measurementUnit);
                SetDirty();
            }
        }

        public Vector3 SizeCentimeters
        {
            get => Mesh3DSlicer.ConvertMetersToUnits(SizeMeters, MeasurementUnit.Centimeters);
            set
            {
                Vector3 inMeters = Mesh3DSlicer.ConvertUnitsToMeters(value, MeasurementUnit.Centimeters);
                SizeMeters = inMeters;
            }
        }

        public float Width
        {
            get => targetSize.x;
            set => TargetSize = new Vector3(value, targetSize.y, targetSize.z);
        }

        public float Height
        {
            get => targetSize.y;
            set => TargetSize = new Vector3(targetSize.x, value, targetSize.z);
        }

        public float Depth
        {
            get => targetSize.z;
            set => TargetSize = new Vector3(targetSize.x, targetSize.y, value);
        }

        public SliceBorder3D Borders
        {
            get => borders;
            set
            {
                borders = value;
                SetDirty();
            }
        }

        public PivotAnchor Pivot
        {
            get => pivotAnchor;
            set
            {
                pivotAnchor = value;
                SetDirty();
            }
        }

        public Vector3 CustomPivot
        {
            get => customPivot;
            set
            {
                customPivot = new Vector3(
                    Mathf.Clamp01(value.x),
                    Mathf.Clamp01(value.y),
                    Mathf.Clamp01(value.z)
                );
                SetDirty();
            }
        }

        public Vector3 NormalizedPivot => Mesh3DSlicer.GetNormalizedPivot(pivotAnchor, customPivot, originalCombinedBounds);

        public List<SubmeshTilingConfig> SubmeshConfigs => submeshTilingConfigs;
        public Bounds OriginalBounds => originalCombinedBounds;
        public Vector3 OriginalSizeMeters => originalCombinedBounds.size;
        public bool IsInitialized => isInitialized;
        public IReadOnlyList<MeshData> MeshTargets => meshDataList;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            InitializeMeshes(forceRebind: true);
            ApplyDeformation();
        }

        private void OnEnable()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (meshDataList.Count == 0 || NeedsRebind())
            {
                InitializeMeshes(forceRebind: true);
            }
            ApplyDeformation();
        }

        private void Start()
        {
            if (activePaintingConfig != null)
            {
                ApplyPaintingConfig(activePaintingConfig);
            }
            else
            {
                ApplyDeformation();
            }
        }

        private void Update()
        {
            if (isDirty)
            {
                isDirty = false;
                ApplyDeformation();
            }
        }

        private void OnValidate()
        {
            targetSize.x = Mathf.Max(0.0001f, targetSize.x);
            targetSize.y = Mathf.Max(0.0001f, targetSize.y);
            targetSize.z = Mathf.Max(0.0001f, targetSize.z);
            SetDirty();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In Editor, restore source meshes on filters so scene saving doesn't save null mesh overrides
                foreach (var item in meshDataList)
                {
                    if (item.filter != null && item.sourceMesh != null)
                    {
                        item.filter.sharedMesh = item.sourceMesh;
                    }
                }
            }
#endif
        }

        private void OnDestroy()
        {
            CleanupInstancedMeshes();
        }

        private bool NeedsRebind()
        {
            if (meshDataList.Count == 0) return true;
            foreach (var item in meshDataList)
            {
                if (item.filter == null || item.instancedMesh == null || item.filter.sharedMesh != item.instancedMesh)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Initialization & Mesh Caching

        public void InitializeMeshes(bool forceRebind = false)
        {
            if (isInitialized && !forceRebind && meshDataList.Count > 0 && !NeedsRebind())
            {
                return;
            }

            CleanupInstancedMeshes();
            meshDataList.Clear();

            List<MeshFilter> filters = new List<MeshFilter>();
            if (includeChildren)
            {
                GetComponentsInChildren(true, filters);
            }
            else
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null) filters.Add(mf);
            }

            if (filters.Count == 0)
            {
                isInitialized = false;
                return;
            }

            Bounds totalBounds = new Bounds();
            bool boundsInitialized = false;

            foreach (var mf in filters)
            {
                if (mf == null) continue;

                Mesh src = null;

                // 1. Check if mf.sharedMesh is an original asset mesh
                if (mf.sharedMesh != null && !mf.sharedMesh.name.EndsWith("_9Sliced"))
                {
                    src = mf.sharedMesh;
                }

                // 2. Check saved serialized binding
                if (src == null)
                {
                    var binding = sourceMeshBindings.Find(b => b.filter == mf);
                    if (binding != null && binding.sourceMesh != null)
                    {
                        src = binding.sourceMesh;
                    }
                }

                // 3. Fallback in Editor from prefab source
#if UNITY_EDITOR
                if (src == null)
                {
                    MeshFilter prefabMf = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(mf);
                    if (prefabMf != null && prefabMf.sharedMesh != null)
                    {
                        src = prefabMf.sharedMesh;
                    }
                }
#endif

                // 4. Fallback if sharedMesh was already instanced
                if (src == null && mf.sharedMesh != null)
                {
                    src = mf.sharedMesh;
                }

                if (src == null) continue;

                // Save or update serialized binding
                var existingBinding = sourceMeshBindings.Find(b => b.filter == mf);
                if (existingBinding != null)
                {
                    existingBinding.sourceMesh = src;
                }
                else
                {
                    sourceMeshBindings.Add(new SourceMeshBinding { filter = mf, sourceMesh = src });
                }

                Matrix4x4 localToRoot = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Matrix4x4 rootToLocal = localToRoot.inverse;

                Vector3[] srcVerts;
                try
                {
                    srcVerts = src.vertices;
                }
                catch (Exception)
                {
                    continue;
                }

                if (srcVerts == null || srcVerts.Length == 0) continue;

                for (int i = 0; i < srcVerts.Length; i++)
                {
                    Vector3 rootPoint = localToRoot.MultiplyPoint3x4(srcVerts[i]);
                    if (!boundsInitialized)
                    {
                        totalBounds = new Bounds(rootPoint, Vector3.zero);
                        boundsInitialized = true;
                    }
                    else
                    {
                        totalBounds.Encapsulate(rootPoint);
                    }
                }

                Mesh instanced = Instantiate(src);
                instanced.name = $"{src.name}_9Sliced";
                instanced.hideFlags = HideFlags.DontSave;

                mf.sharedMesh = instanced;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();

                var data = new MeshData
                {
                    filter = mf,
                    renderer = mr,
                    sourceMesh = src,
                    instancedMesh = instanced,
                    originalVertices = srcVerts,
                    deformedVertices = new Vector3[srcVerts.Length],
                    localToRoot = localToRoot,
                    rootToLocal = rootToLocal
                };

                meshDataList.Add(data);
            }

            if (boundsInitialized)
            {
                originalCombinedBounds = totalBounds;
                if (!isInitialized)
                {
                    targetSize = Mesh3DSlicer.ConvertMetersToUnits(originalCombinedBounds.size, measurementUnit);
                    AutoPopulateSubmeshConfigs();
                }
                isInitialized = true;
            }

            SetDirty();
        }

        /// <summary>
        /// Populates default submesh tiling configs based on renderer materials if list is empty.
        /// </summary>
        public void AutoPopulateSubmeshConfigs()
        {
            if (submeshTilingConfigs.Count > 0) return;

            MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null && mr.sharedMaterials != null)
            {
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string matName = (mats[i] != null) ? mats[i].name : $"Submesh {i}";
                    bool isSides = matName.ToLower().Contains("side") || matName.ToLower().Contains("frame");

                    var config = new SubmeshTilingConfig
                    {
                        label = matName,
                        materialSlotIndex = i,
                        autoScaleTiling = isSides,
                        scaleTilingX = false,
                        scaleTilingY = isSides,
                        referenceSizeCm = new Vector2(50f, 50f),
                        baseTiling = Vector2.one,
                        baseOffset = Vector2.zero
                    };
                    submeshTilingConfigs.Add(config);
                }
            }
        }

        private void CleanupInstancedMeshes()
        {
            foreach (var item in meshDataList)
            {
                if (item.instancedMesh != null)
                {
                    if (item.filter != null && item.sourceMesh != null)
                    {
                        item.filter.sharedMesh = item.sourceMesh;
                    }

                    if (Application.isPlaying)
                        Destroy(item.instancedMesh);
                    else
                        DestroyImmediate(item.instancedMesh);
                }
            }
        }

        #endregion

        #region Slicing Deformation & Tiling

        public void SetDirty()
        {
            isDirty = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyDeformation();
            }
#endif
        }

        public void ApplyDeformation()
        {
            if (!isInitialized || meshDataList.Count == 0 || NeedsRebind())
            {
                InitializeMeshes(forceRebind: true);
                if (!isInitialized || meshDataList.Count == 0) return;
            }

            Vector3 sizeInMeters = Mesh3DSlicer.ConvertUnitsToMeters(targetSize, measurementUnit);

            Mesh3DSlicer.CalculateTargetBounds(
                originalCombinedBounds,
                sizeInMeters,
                pivotAnchor,
                customPivot,
                borders,
                out Vector3 targetMin,
                out Vector3 targetMax
            );

            foreach (var data in meshDataList)
            {
                if (data.filter == null || data.instancedMesh == null || data.originalVertices == null) continue;

                data.localToRoot = transform.worldToLocalMatrix * data.filter.transform.localToWorldMatrix;
                data.rootToLocal = data.localToRoot.inverse;

                Mesh3DSlicer.DeformVertices(
                    data.originalVertices,
                    data.deformedVertices,
                    originalCombinedBounds,
                    borders,
                    targetMin,
                    targetMax,
                    data.localToRoot,
                    data.rootToLocal
                );

                data.instancedMesh.vertices = data.deformedVertices;
                data.instancedMesh.RecalculateBounds();

                if (recalculateNormals)
                {
                    data.instancedMesh.RecalculateNormals();
                }

                if (recalculateTangents)
                {
                    data.instancedMesh.RecalculateTangents();
                }
            }

            UpdateTextureTilings(sizeInMeters);

            if (updateColliders)
            {
                UpdateAttachedColliders(targetMin, targetMax);
            }
        }

        /// <summary>
        /// Updates texture tiling on configured submeshes to preserve constant texel density.
        /// </summary>
        public void UpdateTextureTilings(Vector3 sizeInMeters)
        {
            if (submeshTilingConfigs == null || submeshTilingConfigs.Count == 0) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            Vector2 currentSizeCm = new Vector2(sizeInMeters.x * 100f, sizeInMeters.y * 100f);

            foreach (var config in submeshTilingConfigs)
            {
                if (!config.autoScaleTiling) continue;

                float refX = Mathf.Max(0.001f, config.referenceSizeCm.x);
                float refY = Mathf.Max(0.001f, config.referenceSizeCm.y);

                float scaleRatioX = config.scaleTilingX ? (currentSizeCm.x / refX) : 1.0f;
                float scaleRatioY = config.scaleTilingY ? (currentSizeCm.y / refY) : 1.0f;

                Vector2 finalTiling = new Vector2(
                    config.baseTiling.x * scaleRatioX,
                    config.baseTiling.y * scaleRatioY
                );

                Vector4 stVector = new Vector4(finalTiling.x, finalTiling.y, config.baseOffset.x, config.baseOffset.y);

                foreach (var data in meshDataList)
                {
                    if (data.renderer == null) continue;

                    data.renderer.GetPropertyBlock(propertyBlock, config.materialSlotIndex);

                    if (config.texturePropertyNames != null && config.texturePropertyNames.Length > 0)
                    {
                        foreach (var propName in config.texturePropertyNames)
                        {
                            if (string.IsNullOrEmpty(propName)) continue;
                            string stProp = propName.EndsWith("_ST") ? propName : $"{propName}_ST";
                            propertyBlock.SetVector(stProp, stVector);
                        }
                    }
                    else
                    {
                        propertyBlock.SetVector("_MainTex_ST", stVector);
                        propertyBlock.SetVector("_BaseMap_ST", stVector);
                        propertyBlock.SetVector("_BumpMap_ST", stVector);
                    }

                    data.renderer.SetPropertyBlock(propertyBlock, config.materialSlotIndex);
                }
            }
        }

        private void UpdateAttachedColliders(Vector3 targetMin, Vector3 targetMax)
        {
            var boxCol = GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.center = (targetMin + targetMax) * 0.5f;
                boxCol.size = targetMax - targetMin;
            }

            var meshCol = GetComponent<MeshCollider>();
            if (meshCol != null && meshDataList.Count > 0 && meshDataList[0].instancedMesh != null)
            {
                meshCol.sharedMesh = null;
                meshCol.sharedMesh = meshDataList[0].instancedMesh;
            }
        }

        #endregion

        #region Runtime Material & Submesh API

        /// <summary>
        /// Replaces the material on a specific submesh/material slot index at runtime.
        /// </summary>
        public void SetMaterial(int slotIndex, Material newMaterial, Renderer targetRenderer = null)
        {
            Renderer rend = targetRenderer != null ? targetRenderer : GetComponentInChildren<MeshRenderer>();
            if (rend == null) return;

            Material[] mats = rend.materials; // Creates local instances
            if (slotIndex >= 0 && slotIndex < mats.Length)
            {
                mats[slotIndex] = newMaterial;
                rend.materials = mats;
                SetDirty();
            }
            else
            {
                Debug.LogWarning($"[NineSliceMesh3D] Material slot index {slotIndex} out of range (count: {mats.Length}).", this);
            }
        }

        /// <summary>
        /// Swaps the material by finding the slot name or current material name.
        /// </summary>
        public void SetMaterial(string slotOrMaterialName, Material newMaterial, Renderer targetRenderer = null)
        {
            Renderer rend = targetRenderer != null ? targetRenderer : GetComponentInChildren<MeshRenderer>();
            if (rend == null) return;

            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.StartsWith(slotOrMaterialName, StringComparison.OrdinalIgnoreCase))
                {
                    mats[i] = newMaterial;
                    rend.materials = mats;
                    SetDirty();
                    return;
                }
            }

            for (int i = 0; i < submeshTilingConfigs.Count; i++)
            {
                if (submeshTilingConfigs[i].label.Equals(slotOrMaterialName, StringComparison.OrdinalIgnoreCase))
                {
                    SetMaterial(submeshTilingConfigs[i].materialSlotIndex, newMaterial, targetRenderer);
                    return;
                }
            }
        }

        /// <summary>
        /// Returns the material at the specified slot.
        /// </summary>
        public Material GetMaterial(int slotIndex, Renderer targetRenderer = null)
        {
            Renderer rend = targetRenderer != null ? targetRenderer : GetComponentInChildren<MeshRenderer>();
            if (rend == null) return null;

            Material[] mats = Application.isPlaying ? rend.materials : rend.sharedMaterials;
            if (slotIndex >= 0 && slotIndex < mats.Length)
            {
                return mats[slotIndex];
            }
            return null;
        }

        /// <summary>
        /// Replaces the main texture on a specific submesh slot at runtime.
        /// </summary>
        public void SetSubmeshTexture(int slotIndex, Texture newTexture, string propertyName = "_MainTex", Renderer targetRenderer = null)
        {
            Material mat = GetMaterial(slotIndex, targetRenderer);
            if (mat != null)
            {
                if (mat.HasProperty(propertyName))
                {
                    mat.SetTexture(propertyName, newTexture);
                }
                else if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", newTexture);
                }
            }
        }

        /// <summary>
        /// Configures constant texel density / tiling calibration for a specific submesh slot.
        /// </summary>
        public void SetSubmeshTilingCalibration(int slotIndex, Vector2 referenceSizeCm, bool scaleX = false, bool scaleY = true)
        {
            var config = submeshTilingConfigs.Find(c => c.materialSlotIndex == slotIndex);
            if (config == null)
            {
                config = new SubmeshTilingConfig
                {
                    materialSlotIndex = slotIndex,
                    label = $"Submesh {slotIndex}"
                };
                submeshTilingConfigs.Add(config);
            }

            config.autoScaleTiling = true;
            config.referenceSizeCm = referenceSizeCm;
            config.scaleTilingX = scaleX;
            config.scaleTilingY = scaleY;

            SetDirty();
        }

        #endregion

        #region Public Runtime Sizing & Config API

        /// <summary>
        /// Applies a PaintingData config asset at runtime or in editor.
        /// </summary>
        public void ApplyPaintingConfig(PaintingData config)
        {
            if (config == null) return;
            activePaintingConfig = config;
            config.ApplyTo(this);
            SetDirty();
        }

        /// <summary>
        /// Sets the pivot anchor preset.
        /// </summary>
        public void SetPivot(PivotAnchor anchor)
        {
            Pivot = anchor;
        }

        /// <summary>
        /// Sets custom normalized pivot coordinates (0..1 across X, Y, Z).
        /// </summary>
        public void SetCustomPivot(Vector3 normalizedPivot)
        {
            pivotAnchor = PivotAnchor.Custom;
            CustomPivot = normalizedPivot;
        }

        /// <summary>
        /// Sets the size of the mesh at runtime in specified units.
        /// </summary>
        public void SetSize(Vector3 size, MeasurementUnit unit = MeasurementUnit.Centimeters)
        {
            measurementUnit = unit;
            TargetSize = size;
        }

        /// <summary>
        /// Sets width, height, and depth in specified units.
        /// </summary>
        public void SetSize(float width, float height, float depth, MeasurementUnit unit = MeasurementUnit.Centimeters)
        {
            SetSize(new Vector3(width, height, depth), unit);
        }

        /// <summary>
        /// Resets target size to the original mesh dimensions.
        /// </summary>
        public void ResetToOriginalSize()
        {
            if (isInitialized)
            {
                targetSize = Mesh3DSlicer.ConvertMetersToUnits(originalCombinedBounds.size, measurementUnit);
                SetDirty();
            }
        }

        /// <summary>
        /// Automatically sets 9-slice borders based on a percentage of the original mesh size.
        /// </summary>
        public void AutoDetectBorders(float marginRatio = 0.15f)
        {
            if (!isInitialized) InitializeMeshes(forceRebind: true);
            if (!isInitialized) return;

            Vector3 size = originalCombinedBounds.size;
            borders.x = new SliceAxisSettings(true, size.x * marginRatio, size.x * marginRatio);
            borders.y = new SliceAxisSettings(true, size.y * marginRatio, size.y * marginRatio);
            borders.z = new SliceAxisSettings(false, 0f, 0f);
            SetDirty();
        }

        #endregion
    }
}
