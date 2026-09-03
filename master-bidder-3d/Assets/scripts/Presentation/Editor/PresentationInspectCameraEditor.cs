using UnityEditor;
using UnityEngine;

namespace MasterBidder.Presentation.Editor
{
    [CustomEditor(typeof(PresentationInspectCamera))]
    public class PresentationInspectCameraEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var inspect = (PresentationInspectCamera)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Inspect Camera", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play mode: RMB toggles inspect. Mouse look moves pivot. Scroll zooms. LMB reframes to pivot.",
                    MessageType.Info);
                return;
            }

            string label = inspect.IsInspecting ? "Exit Inspect (RMB)" : "Enter Inspect (RMB)";
            if (GUILayout.Button(label, GUILayout.Height(32)))
            {
                inspect.ToggleInspect();
            }

            if (inspect.IsInspecting)
            {
                using (new EditorGUI.DisabledScope(false))
                {
                    if (GUILayout.Button("Pan To Face Pivot (LMB)"))
                    {
                        inspect.PanToFacePivot();
                    }
                }

                Repaint();
            }
        }
    }
}
