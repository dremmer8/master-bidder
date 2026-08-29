using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NineSlice3D.Editor
{
    [CustomEditor(typeof(NineSliceMesh3D))]
    public class NineSliceMesh3DEditor : UnityEditor.Editor
    {
        private enum EditMode
        {
            None,
            Borders,
            TargetBounds
        }

        private EditMode currentEditMode = EditMode.Borders;
        private bool showBordersFoldout = true;
        private bool showSizeFoldout = true;
        private bool showSubmeshesFoldout = true;
        private bool showAdvancedFoldout = false;

        private NineSliceMesh3D slicer;

        private void OnEnable()
        {
            slicer = (NineSliceMesh3D)target;
            if (!slicer.IsInitialized)
            {
                slicer.InitializeMeshes(forceRebind: true);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            slicer = (NineSliceMesh3D)target;

            DrawReadWriteWarning();

            DrawHeader();

            EditorGUILayout.Space(6);
            DrawEditModeToolbar();

            EditorGUILayout.Space(6);
            DrawSizeSection();

            EditorGUILayout.Space(6);
            DrawBordersSection();

            EditorGUILayout.Space(6);
            DrawSubmeshTilingSection();

            EditorGUILayout.Space(6);
            DrawAdvancedSection();

            EditorGUILayout.Space(10);
            DrawActionButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3D 9-Slice Mesh Deformer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scale 3D meshes while preserving rigid corners & details.", EditorStyles.miniLabel);

            if (slicer.IsInitialized)
            {
                Vector3 orig = slicer.OriginalSizeMeters;
                Vector3 origCm = orig * 100f;
                EditorGUILayout.LabelField($"Original Size: {origCm.x:F1}cm × {origCm.y:F1}cm × {origCm.z:F1}cm ({orig.x:F3}m × {orig.y:F3}m × {orig.z:F3}m)", EditorStyles.miniBoldLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawReadWriteWarning()
        {
            bool hasUnreadable = false;
            foreach (var meshData in slicer.MeshTargets)
            {
                if (meshData.sourceMesh != null && !meshData.sourceMesh.isReadable)
                {
                    hasUnreadable = true;
                    break;
                }
            }

            if (hasUnreadable)
            {
                EditorGUILayout.HelpBox("Source mesh is not Read/Write enabled. Vertices cannot be read at runtime unless Read/Write is enabled.", MessageType.Warning);
                if (GUILayout.Button("Fix: Enable Read/Write on Source Mesh(es)"))
                {
                    foreach (var meshData in slicer.MeshTargets)
                    {
                        if (meshData.sourceMesh != null)
                        {
                            string path = AssetDatabase.GetAssetPath(meshData.sourceMesh);
                            if (!string.IsNullOrEmpty(path))
                            {
                                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                                if (importer != null && !importer.isReadable)
                                {
                                    importer.isReadable = true;
                                    importer.SaveAndReimport();
                                }
                            }
                        }
                    }
                    slicer.InitializeMeshes(forceRebind: true);
                }
                EditorGUILayout.Space(4);
            }
        }

        private void DrawEditModeToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Scene View Tool");
            int selected = GUILayout.Toolbar((int)currentEditMode, new[] { "None", "Edit Borders", "Edit Target Size" });
            if (selected != (int)currentEditMode)
            {
                currentEditMode = (EditMode)selected;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSizeSection()
        {
            showSizeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showSizeFoldout, "Target Dimensions (Real World Units)");
            if (showSizeFoldout)
            {
                EditorGUI.indentLevel++;

                SerializedProperty unitProp = serializedObject.FindProperty("measurementUnit");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(unitProp, new GUIContent("Measurement Unit"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    slicer.DisplayUnit = (MeasurementUnit)unitProp.enumValueIndex;
                }

                SerializedProperty sizeProp = serializedObject.FindProperty("targetSize");
                MeasurementUnit currentUnit = (MeasurementUnit)unitProp.enumValueIndex;
                string unitLabel = GetUnitAbbreviation(currentUnit);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(sizeProp, new GUIContent($"Dimensions ({unitLabel})"));
                EditorGUILayout.EndHorizontal();

                SerializedProperty pivotProp = serializedObject.FindProperty("pivotAnchor");
                EditorGUILayout.PropertyField(pivotProp, new GUIContent("Pivot Anchor"));

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawBordersSection()
        {
            showBordersFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showBordersFoldout, "9-Slice Borders (Original Local Margins)");
            if (showBordersFoldout)
            {
                EditorGUI.indentLevel++;

                SerializedProperty bordersProp = serializedObject.FindProperty("borders");
                SerializedProperty xProp = bordersProp.FindPropertyRelative("x");
                SerializedProperty yProp = bordersProp.FindPropertyRelative("y");
                SerializedProperty zProp = bordersProp.FindPropertyRelative("z");

                Bounds origBounds = slicer.OriginalBounds;
                Vector3 origSize = origBounds.size;

                DrawAxisBorderGUI("X Axis (Width: Left / Right)", xProp, origSize.x, "Left Margin", "Right Margin");
                DrawAxisBorderGUI("Y Axis (Height: Bottom / Top)", yProp, origSize.y, "Bottom Margin", "Top Margin");
                DrawAxisBorderGUI("Z Axis (Depth: Back / Front)", zProp, origSize.z, "Back Margin", "Front Margin");

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAxisBorderGUI(string title, SerializedProperty axisProp, float maxDimension, string minLabel, string maxLabel)
        {
            SerializedProperty enabledProp = axisProp.FindPropertyRelative("enabled");
            SerializedProperty minMarginProp = axisProp.FindPropertyRelative("minMargin");
            SerializedProperty maxMarginProp = axisProp.FindPropertyRelative("maxMargin");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enabledProp, new GUIContent(title));

            if (enabledProp.boolValue)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(minMarginProp, new GUIContent(minLabel));
                EditorGUILayout.EndHorizontal();
                minMarginProp.floatValue = Mathf.Clamp(minMarginProp.floatValue, 0f, maxDimension);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(maxMarginProp, new GUIContent(maxLabel));
                EditorGUILayout.EndHorizontal();
                maxMarginProp.floatValue = Mathf.Clamp(maxMarginProp.floatValue, 0f, maxDimension - minMarginProp.floatValue);

                float centerSpan = Mathf.Max(0f, maxDimension - minMarginProp.floatValue - maxMarginProp.floatValue);
                EditorGUILayout.LabelField($"Active Center Span: {centerSpan:F3}m / {maxDimension:F3}m", EditorStyles.miniLabel);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSubmeshTilingSection()
        {
            showSubmeshesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showSubmeshesFoldout, "Submesh Materials & Texel Density Tiling");
            if (showSubmeshesFoldout)
            {
                EditorGUI.indentLevel++;

                SerializedProperty configsProp = serializedObject.FindProperty("submeshTilingConfigs");

                MeshRenderer mr = slicer.GetComponentInChildren<MeshRenderer>();
                Material[] currentMats = mr != null ? (Application.isPlaying ? mr.materials : mr.sharedMaterials) : null;

                if (GUILayout.Button("Auto-Detect Material Slots from Renderer"))
                {
                    slicer.AutoPopulateSubmeshConfigs();
                    EditorUtility.SetDirty(slicer);
                }

                EditorGUILayout.Space(2);

                Vector3 currentMeters = slicer.SizeMeters;
                Vector2 currentCm = new Vector2(currentMeters.x * 100f, currentMeters.y * 100f);

                for (int i = 0; i < configsProp.arraySize; i++)
                {
                    SerializedProperty elem = configsProp.GetArrayElementAtIndex(i);
                    SerializedProperty labelProp = elem.FindPropertyRelative("label");
                    SerializedProperty slotProp = elem.FindPropertyRelative("materialSlotIndex");
                    SerializedProperty autoScaleProp = elem.FindPropertyRelative("autoScaleTiling");
                    SerializedProperty scaleXProp = elem.FindPropertyRelative("scaleTilingX");
                    SerializedProperty scaleYProp = elem.FindPropertyRelative("scaleTilingY");
                    SerializedProperty refSizeProp = elem.FindPropertyRelative("referenceSizeCm");
                    SerializedProperty baseTilingProp = elem.FindPropertyRelative("baseTiling");
                    SerializedProperty baseOffsetProp = elem.FindPropertyRelative("baseOffset");

                    int slotIdx = slotProp.intValue;
                    string currentMatName = (currentMats != null && slotIdx >= 0 && slotIdx < currentMats.Length && currentMats[slotIdx] != null)
                        ? currentMats[slotIdx].name
                        : "None";

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Slot {slotIdx}: {labelProp.stringValue} ({currentMatName})", EditorStyles.boldLabel);
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        configsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(slotProp, new GUIContent("Material Slot Index"));
                    EditorGUILayout.PropertyField(labelProp, new GUIContent("Slot Label"));

                    // Runtime Material Swap in Inspector
                    if (currentMats != null && slotIdx >= 0 && slotIdx < currentMats.Length)
                    {
                        EditorGUI.BeginChangeCheck();
                        Material newMat = (Material)EditorGUILayout.ObjectField("Assigned Material", currentMats[slotIdx], typeof(Material), false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            slicer.SetMaterial(slotIdx, newMat);
                        }
                    }

                    EditorGUILayout.PropertyField(autoScaleProp, new GUIContent("Auto-Scale Tiling (Preserve Texel Density)"));

                    if (autoScaleProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(refSizeProp, new GUIContent("Calibration Size (cm)"));
                        if (refSizeProp.vector2Value.x <= 0f || refSizeProp.vector2Value.y <= 0f)
                        {
                            refSizeProp.vector2Value = new Vector2(50f, 50f);
                        }

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(scaleXProp, new GUIContent("Scale X (Width)"));
                        EditorGUILayout.PropertyField(scaleYProp, new GUIContent("Scale Y (Height)"));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(baseTilingProp, new GUIContent("Base Tiling (at 50x50 cm)"));
                        EditorGUILayout.PropertyField(baseOffsetProp, new GUIContent("Base Offset"));

                        // Live calculated tiling display
                        Vector2 refCm = refSizeProp.vector2Value;
                        float ratioX = scaleXProp.boolValue ? (currentCm.x / refCm.x) : 1f;
                        float ratioY = scaleYProp.boolValue ? (currentCm.y / refCm.y) : 1f;
                        Vector2 finalTiling = new Vector2(baseTilingProp.vector2Value.x * ratioX, baseTilingProp.vector2Value.y * ratioY);

                        EditorGUILayout.LabelField($"Live Tiling Multiplier: X: {ratioX:F2}x | Y: {ratioY:F2}x -> Final Tiling: ({finalTiling.x:F2}, {finalTiling.y:F2})", EditorStyles.miniBoldLabel);
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("+ Add Submesh Tiling Config"))
                {
                    configsProp.arraySize++;
                    SerializedProperty newElem = configsProp.GetArrayElementAtIndex(configsProp.arraySize - 1);
                    newElem.FindPropertyRelative("materialSlotIndex").intValue = configsProp.arraySize - 1;
                    newElem.FindPropertyRelative("label").stringValue = $"Submesh {configsProp.arraySize - 1}";
                    newElem.FindPropertyRelative("autoScaleTiling").boolValue = true;
                    newElem.FindPropertyRelative("scaleTilingX").boolValue = false;
                    newElem.FindPropertyRelative("scaleTilingY").boolValue = true;
                    newElem.FindPropertyRelative("referenceSizeCm").vector2Value = new Vector2(50f, 50f);
                    newElem.FindPropertyRelative("baseTiling").vector2Value = Vector2.one;
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAdvancedSection()
        {
            showAdvancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvancedFoldout, "Mesh Options & Hierarchy");
            if (showAdvancedFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("includeChildren"), new GUIContent("Include Children"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recalculateNormals"), new GUIContent("Recalculate Normals"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recalculateTangents"), new GUIContent("Recalculate Tangents"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("updateColliders"), new GUIContent("Update Colliders"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Detect Borders (15%)"))
            {
                Undo.RecordObject(slicer, "Auto Detect Borders");
                slicer.AutoDetectBorders(0.15f);
                EditorUtility.SetDirty(slicer);
            }

            if (GUILayout.Button("Reset to Original Size"))
            {
                Undo.RecordObject(slicer, "Reset to Original Size");
                slicer.ResetToOriginalSize();
                EditorUtility.SetDirty(slicer);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Save Resized Mesh to Asset...", GUILayout.Height(28)))
            {
                SaveMeshAssetDialog();
            }
        }

        private void SaveMeshAssetDialog()
        {
            if (slicer.MeshTargets.Count == 0)
            {
                EditorUtility.DisplayDialog("Save Mesh", "No meshes found to save.", "OK");
                return;
            }

            string defaultName = $"{slicer.gameObject.name}_Resized.asset";
            string path = EditorUtility.SaveFilePanelInProject("Save Resized Mesh", defaultName, "asset", "Choose save location for resized mesh asset.");
            if (string.IsNullOrEmpty(path)) return;

            Mesh primaryMesh = slicer.MeshTargets[0].instancedMesh;
            if (primaryMesh != null)
            {
                Mesh newMeshAsset = Instantiate(primaryMesh);
                newMeshAsset.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(newMeshAsset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = newMeshAsset;
                Debug.Log($"[NineSlice3D] Successfully saved mesh asset to {path}", newMeshAsset);
            }
        }

        private static string GetUnitAbbreviation(MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Centimeters: return "cm";
                case MeasurementUnit.Millimeters: return "mm";
                case MeasurementUnit.Inches: return "in";
                case MeasurementUnit.Meters:
                default: return "m";
            }
        }

        #region Scene View Handles

        private void OnSceneGUI()
        {
            slicer = (NineSliceMesh3D)target;
            if (!slicer.IsInitialized) return;

            Transform t = slicer.transform;
            Bounds origBounds = slicer.OriginalBounds;

            SliceBorder3D borders = slicer.Borders;

            if (currentEditMode == EditMode.Borders)
            {
                DrawBorderHandles(t, origBounds, borders);
            }
            else if (currentEditMode == EditMode.TargetBounds)
            {
                DrawTargetBoundsHandles(t, origBounds, borders);
            }

            DrawSliceGridGizmo(t, origBounds, borders);
        }

        private void DrawBorderHandles(Transform rootTransform, Bounds origBounds, SliceBorder3D borders)
        {
            Vector3 origMin = origBounds.min;
            Vector3 origMax = origBounds.max;
            Vector3 origCenter = origBounds.center;

            bool changed = false;

            // X Axis: Left / Right handles
            if (borders.x.enabled)
            {
                float leftPos = origMin.x + borders.x.minMargin;
                float rightPos = origMax.x - borders.x.maxMargin;

                Vector3 worldLeft = rootTransform.TransformPoint(new Vector3(leftPos, origCenter.y, origCenter.z));
                Vector3 worldRight = rootTransform.TransformPoint(new Vector3(rightPos, origCenter.y, origCenter.z));

                Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldLeft = Handles.Slider(worldLeft, rootTransform.right, HandleUtility.GetHandleSize(worldLeft) * 0.15f, Handles.ConeHandleCap, 0.01f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Change 9-Slice Left Border");
                    Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldLeft);
                    borders.x.minMargin = Mathf.Clamp(localPoint.x - origMin.x, 0f, origBounds.size.x - borders.x.maxMargin);
                    changed = true;
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldRight = Handles.Slider(worldRight, -rootTransform.right, HandleUtility.GetHandleSize(worldRight) * 0.15f, Handles.ConeHandleCap, 0.01f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Change 9-Slice Right Border");
                    Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldRight);
                    borders.x.maxMargin = Mathf.Clamp(origMax.x - localPoint.x, 0f, origBounds.size.x - borders.x.minMargin);
                    changed = true;
                }
            }

            // Y Axis: Bottom / Top handles
            if (borders.y.enabled)
            {
                float bottomPos = origMin.y + borders.y.minMargin;
                float topPos = origMax.y - borders.y.maxMargin;

                Vector3 worldBottom = rootTransform.TransformPoint(new Vector3(origCenter.x, bottomPos, origCenter.z));
                Vector3 worldTop = rootTransform.TransformPoint(new Vector3(origCenter.x, topPos, origCenter.z));

                Handles.color = new Color(0.2f, 0.7f, 1f, 0.9f);
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldBottom = Handles.Slider(worldBottom, rootTransform.up, HandleUtility.GetHandleSize(worldBottom) * 0.15f, Handles.ConeHandleCap, 0.01f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Change 9-Slice Bottom Border");
                    Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldBottom);
                    borders.y.minMargin = Mathf.Clamp(localPoint.y - origMin.y, 0f, origBounds.size.y - borders.y.maxMargin);
                    changed = true;
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldTop = Handles.Slider(worldTop, -rootTransform.up, HandleUtility.GetHandleSize(worldTop) * 0.15f, Handles.ConeHandleCap, 0.01f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Change 9-Slice Top Border");
                    Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldTop);
                    borders.y.maxMargin = Mathf.Clamp(origMax.y - localPoint.y, 0f, origBounds.size.y - borders.y.minMargin);
                    changed = true;
                }
            }

            // Z Axis: Back / Front handles
            if (borders.z.enabled)
            {
                float backPos = origMin.z + borders.z.minMargin;
                float frontPos = origMax.z - borders.z.maxMargin;

                Vector3 worldBack = rootTransform.TransformPoint(new Vector3(origCenter.x, origCenter.y, backPos));
                Vector3 worldFront = rootTransform.TransformPoint(new Vector3(origCenter.x, origCenter.y, frontPos));

                Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldBack = Handles.Slider(worldBack, rootTransform.forward, HandleUtility.GetHandleSize(worldBack) * 0.15f, Handles.ConeHandleCap, 0.01f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Change 9-Slice Back Border");
                    Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldBack);
                    borders.z.minMargin = Mathf.Clamp(localPoint.z - origMin.z, 0f, origBounds.size.z - borders.z.maxMargin);
                    changed = true;
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldFront = Handles.Slider(worldFront, -rootTransform.forward, HandleUtility.GetHandleSize(worldFront) * 0.15f, Handles.ConeHandleCap, 0.01f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(slicer, "Change 9-Slice Front Border");
                    Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldFront);
                    borders.z.maxMargin = Mathf.Clamp(origMax.z - localPoint.z, 0f, origBounds.size.z - borders.z.minMargin);
                    changed = true;
                }
            }

            if (changed)
            {
                slicer.Borders = borders;
                EditorUtility.SetDirty(slicer);
            }
        }

        private void DrawTargetBoundsHandles(Transform rootTransform, Bounds origBounds, SliceBorder3D borders)
        {
            Vector3 sizeMeters = slicer.SizeMeters;
            Mesh3DSlicer.CalculateTargetBounds(origBounds, sizeMeters, slicer.Pivot, borders, out Vector3 targetMin, out Vector3 targetMax);

            Vector3 targetCenter = (targetMin + targetMax) * 0.5f;

            Vector3 worldRight = rootTransform.TransformPoint(new Vector3(targetMax.x, targetCenter.y, targetCenter.z));
            Vector3 worldTop = rootTransform.TransformPoint(new Vector3(targetCenter.x, targetMax.y, targetCenter.z));
            Vector3 worldFront = rootTransform.TransformPoint(new Vector3(targetCenter.x, targetCenter.y, targetMax.z));

            Handles.color = Color.cyan;

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldRight = Handles.Slider(worldRight, rootTransform.right, HandleUtility.GetHandleSize(worldRight) * 0.15f, Handles.CubeHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(slicer, "Resize Width");
                Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldRight);
                float newWidth = Mathf.Max(0.01f, (localPoint.x - targetMin.x));
                slicer.SizeMeters = new Vector3(newWidth, sizeMeters.y, sizeMeters.z);
                EditorUtility.SetDirty(slicer);
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldTop = Handles.Slider(worldTop, rootTransform.up, HandleUtility.GetHandleSize(worldTop) * 0.15f, Handles.CubeHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(slicer, "Resize Height");
                Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldTop);
                float newHeight = Mathf.Max(0.01f, (localPoint.y - targetMin.y));
                slicer.SizeMeters = new Vector3(sizeMeters.x, newHeight, sizeMeters.z);
                EditorUtility.SetDirty(slicer);
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldFront = Handles.Slider(worldFront, rootTransform.forward, HandleUtility.GetHandleSize(worldFront) * 0.15f, Handles.CubeHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(slicer, "Resize Depth");
                Vector3 localPoint = rootTransform.InverseTransformPoint(newWorldFront);
                float newDepth = Mathf.Max(0.01f, (localPoint.z - targetMin.z));
                slicer.SizeMeters = new Vector3(sizeMeters.x, sizeMeters.y, newDepth);
                EditorUtility.SetDirty(slicer);
            }
        }

        private void DrawSliceGridGizmo(Transform rootTransform, Bounds origBounds, SliceBorder3D borders)
        {
            Vector3 sizeMeters = slicer.SizeMeters;
            Mesh3DSlicer.CalculateTargetBounds(origBounds, sizeMeters, slicer.Pivot, borders, out Vector3 targetMin, out Vector3 targetMax);

            Matrix4x4 origMatrix = Handles.matrix;
            Handles.matrix = rootTransform.localToWorldMatrix;

            Handles.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Vector3 targetCenter = (targetMin + targetMax) * 0.5f;
            Vector3 targetSize = targetMax - targetMin;
            Handles.DrawWireCube(targetCenter, targetSize);

            float xL = targetMin.x + borders.x.minMargin;
            float xR = targetMax.x - borders.x.maxMargin;
            float yB = targetMin.y + borders.y.minMargin;
            float yT = targetMax.y - borders.y.maxMargin;

            Handles.color = new Color(0.3f, 1f, 0.4f, 0.6f);

            if (borders.x.enabled)
            {
                Handles.DrawLine(new Vector3(xL, targetMin.y, targetCenter.z), new Vector3(xL, targetMax.y, targetCenter.z));
                Handles.DrawLine(new Vector3(xR, targetMin.y, targetCenter.z), new Vector3(xR, targetMax.y, targetCenter.z));
            }

            if (borders.y.enabled)
            {
                Handles.DrawLine(new Vector3(targetMin.x, yB, targetCenter.z), new Vector3(targetMax.x, yB, targetCenter.z));
                Handles.DrawLine(new Vector3(targetMin.x, yT, targetCenter.z), new Vector3(targetMax.x, yT, targetCenter.z));
            }

            Handles.matrix = origMatrix;
        }

        #endregion
    }
}
