using UnityEngine;
using UnityEngine.UI;

public class CloudController : MonoBehaviour
{
    [Header("구름 마테리얼을 넣어주세요")]
    public Material cloudMaterial;
    
    [Header("구름 이동 속도")]
    public float speed = 1.0f;

    void Update()
    {
        if (cloudMaterial != null)
        {
            // Update 함수는 게임이 '실행' 중일 때만 돕니다.
            // 흘러가는 시간(Time.time)에 속도를 곱해서 쉐이더로 쏴줍니다!
            cloudMaterial.SetFloat("_CustomTime", Time.time * speed);
        }
    }

    /// <summary>
    /// 색을 러프하게 바꾸는 메서드
    /// </summary>
    /// <param name="fromColor">원래 색상</param>
    /// <param name="toColor">목표 색상</param>
    /// <param name="t">보간 값 (0~1 사이, 0이면 fromColor, 1이면 toColor)</param>
    /// <returns>보간된 색상</returns>
    public Color ChangeColorRoughly(Color fromColor, Color toColor, float t = 0.5f)
    {
        // Lerp를 사용해 두 색상 사이를 부드럽게 보간
        t = Mathf.Clamp01(t);
        return Color.Lerp(fromColor, toColor, t);
    }
}