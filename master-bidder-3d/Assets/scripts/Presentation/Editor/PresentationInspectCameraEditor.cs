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
                    "Play mode: scroll-up from hall enters inspect; scroll-down at max zoom returns. RMB toggles. Mouse look / LMB reframe. Transitions blend via proxy camera.",
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
