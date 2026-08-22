#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class InspectorSelectionCleanup
{
    static InspectorSelectionCleanup()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.ExitingPlayMode)
        {
            return;
        }

        UnityEngine.Object selectedObject = Selection.activeObject;
        if (selectedObject == null || !EditorUtility.IsPersistent(selectedObject))
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
        }
    }
}
#endif