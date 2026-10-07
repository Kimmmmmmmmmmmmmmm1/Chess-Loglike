using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ShortcutButton))]
[CanEditMultipleObjects]
public class ShortcutButtonEditor : ButtonEditor
{
    SerializedProperty actionProp;
    SerializedProperty fallbackKeyProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        actionProp = serializedObject.FindProperty("action");
        fallbackKeyProp = serializedObject.FindProperty("fallbackKey");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        if (actionProp != null) EditorGUILayout.PropertyField(actionProp);
        if (fallbackKeyProp != null) EditorGUILayout.PropertyField(fallbackKeyProp);
        serializedObject.ApplyModifiedProperties();
    }
}
