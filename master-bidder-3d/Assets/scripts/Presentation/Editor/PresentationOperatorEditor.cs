using UnityEditor;
using UnityEngine;

namespace MasterBidder.Presentation.Editor
{
    [CustomEditor(typeof(PresentationOperator))]
    public class PresentationOperatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var op = (PresentationOperator)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to use these buttons.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying || op.IsBusy))
            {
                if (GUILayout.Button(op.IsBusy ? "Presenting..." : "Reveal Random Painting", GUILayout.Height(36)))
                {
                    op.PresentRandomPainting();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Lower"))
                {
                    op.LowerCloth();
                }

                if (GUILayout.Button("Swap"))
                {
                    op.SwapToRandomPainting();
                }

                if (GUILayout.Button("Raise"))
                {
                    op.RaiseCloth();
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Next Painting"))
                {
                    op.SwapToNextPainting();
                }
            }

            using (new EditorGUI.DisabledScope(op.LightRig == null))
            {
                if (GUILayout.Button("Aim Light"))
                {
                    var lightTx = op.LightRig != null ? op.LightRig.LightTransform : null;
                    if (lightTx != null)
                    {
                        Undo.RecordObject(lightTx, "Aim Light At Painting");
                    }

                    op.AimLight();
                    if (lightTx != null)
                    {
                        EditorUtility.SetDirty(lightTx);
                    }
                }
            }

            if (Application.isPlaying && op.IsBusy)
            {
                if (GUILayout.Button("Cancel Sequence"))
                {
                    op.CancelSequence();
                }

                // Keep inspector responsive while the coroutine runs.
                Repaint();
            }
        }
    }
}
