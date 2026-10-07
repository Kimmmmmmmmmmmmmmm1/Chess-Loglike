#if UNITY_EDITOR
using UnityEditor;
using TMPro.EditorUtilities; // TMP 에디터 기능 가져오기

// AnimatedTMPText 컴포넌트를 클릭했을 때 이 UI를 보여주라고 유니티에게 지시
[CustomEditor(typeof(AnimatedTMPText), true)]
[CanEditMultipleObjects]
public class AnimatedTMPTextEditor : TMP_EditorPanelUI // 기존 TMP UI를 그대로 상속!
{
    SerializedProperty waveSpeedProp;
    SerializedProperty waveAmountProp;
    SerializedProperty waveFrequencyProp;
    SerializedProperty jitterAmountProp;

    protected override void OnEnable()
    {
        // 부모(기존 TMP)의 OnEnable 실행
        base.OnEnable();

        // 우리가 추가한 변수들 연결
        waveSpeedProp = serializedObject.FindProperty("waveSpeed");
        waveAmountProp = serializedObject.FindProperty("waveAmount");
        waveFrequencyProp = serializedObject.FindProperty("waveFrequency");
        jitterAmountProp = serializedObject.FindProperty("jitterAmount");
    }

    public override void OnInspectorGUI()
    {
        // 1. 기존 TextMeshPro 인스펙터를 원래 모습 그대로 완벽하게 그립니다.
        base.OnInspectorGUI();

        // 2. 그 아래에 우리가 추가한 설정 창을 덧붙여서 그립니다.
        serializedObject.Update();
        
        EditorGUILayout.Space(); // 여백
        EditorGUILayout.LabelField("Text Animation Settings", EditorStyles.boldLabel); // 볼드체 제목
        
        EditorGUILayout.PropertyField(waveSpeedProp);
        EditorGUILayout.PropertyField(waveAmountProp);
        EditorGUILayout.PropertyField(waveFrequencyProp);
        EditorGUILayout.PropertyField(jitterAmountProp);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif