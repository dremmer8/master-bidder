using UnityEditor;
using UnityEngine;

namespace MasterBidder.Presentation.Editor
{
    [CustomEditor(typeof(PresentationLightRig))]
    public class PresentationLightRigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var rig = (PresentationLightRig)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Light Rig", EditorStyles.boldLabel);

            if (GUILayout.Button("Aim Light At Painting", GUILayout.Height(32)))
            {
                Undo.RecordObject(rig.LightTransform != null ? rig.LightTransform : rig.transform, "Aim Light At Painting");
                rig.AimAtPainting();
                EditorUtility.SetDirty(rig.LightTransform != null ? rig.LightTransform : rig.transform);
            }

            if (rig.TryGetAimPoint(out Vector3 aim))
            {
                EditorGUILayout.HelpBox($"Aim point: {aim}", MessageType.None);
            }
        }
    }
}
