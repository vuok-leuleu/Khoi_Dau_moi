using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ChapterQuestController))]
public class ChapterQuestControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ChapterQuestController controller = (ChapterQuestController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quest Test", EditorStyles.boldLabel);

        bool allCompleted = controller.AreAllObjectivesCompleted();
        EditorGUI.BeginChangeCheck();
        bool updatedValue = EditorGUILayout.ToggleLeft(
            "Hoàn thành toàn bộ nhiệm vụ (Test)",
            allCompleted);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(controller, "Toggle All Chapter Quests");
            controller.SetAllObjectivesCompletedForInspector(updatedValue);
            EditorUtility.SetDirty(controller);
        }

        EditorGUILayout.HelpBox(
            "Tick để hoàn thành tất cả nhiệm vụ. Bỏ tick để reset toàn bộ tiến độ. Thao tác test này không cộng tài nguyên thưởng.",
            MessageType.Info);
        EditorGUILayout.Space();

        DrawDefaultInspector();
    }
}