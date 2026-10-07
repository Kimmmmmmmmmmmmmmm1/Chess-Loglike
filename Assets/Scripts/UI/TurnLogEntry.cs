using UnityEngine;
using TMPro;

/// <summary>
/// 개별 턴 로그 엔트리 관리.
/// 자동 페이드 아웃, 스크롤/확장 시 복구, 닫을 때 숨김 등을 처리합니다.
/// </summary>
public class TurnLogEntry : MonoBehaviour
{
    public enum State
    {
        Visible,     // 표시 중
        Fading,      // 페이드 아웃 중
        Hidden,      // 숨겨짐
        RestoreFading // 복구 페이드 인 중
    }

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI textComp;
    private State currentState = State.Visible;
    private float elapsedTime = 0f;
    private float fadeOutStartTime = 0f;
    private float fadeOutDuration = 0.5f;
    private float autoHideDelay = 5f;

    private void OnEnable()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (textComp == null)
        {
            textComp = GetComponent<TextMeshProUGUI>();
            if (textComp == null)
            {
                textComp = GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        // 상태 초기화
        currentState = State.Visible;
        elapsedTime = 0f;
        canvasGroup.alpha = 1f;
    }

    private void Update()
    {
        if (currentState == State.Visible)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= autoHideDelay)
            {
                // 자동 페이드 아웃 시작
                StartFadeOut();
            }
        }
        else if (currentState == State.Fading)
        {
            float fadeProgress = (Time.time - fadeOutStartTime) / fadeOutDuration;
            canvasGroup.alpha = Mathf.Max(0f, 1f - fadeProgress);

            if (fadeProgress >= 1f)
            {
                // 페이드 아웃 완료
                currentState = State.Hidden;
                canvasGroup.alpha = 0f;
            }
        }
        else if (currentState == State.RestoreFading)
        {
            float fadeProgress = (Time.time - fadeOutStartTime) / fadeOutDuration;
            canvasGroup.alpha = Mathf.Min(1f, fadeProgress);

            if (fadeProgress >= 1f)
            {
                // 복구 페이드 인 완료
                currentState = State.Visible;
                canvasGroup.alpha = 1f;
                elapsedTime = 0f; // 타이머 리셋
            }
        }
    }

    /// <summary>
    /// 엔트리 텍스트 설정
    /// </summary>
    public void SetText(string text, Color color)
    {
        if (textComp == null)
        {
            textComp = GetComponent<TextMeshProUGUI>();
        }
        
        if (textComp != null)
        {
            textComp.text = text;
            textComp.color = color;
        }
    }

    /// <summary>
    /// 페이드 아웃 시작
    /// </summary>
    public void StartFadeOut()
    {
        if (currentState == State.Visible)
        {
            currentState = State.Fading;
            fadeOutStartTime = Time.time;
        }
    }

    /// <summary>
    /// 엔트리를 다시 표시 (숨겨진 상태에서 복구)
    /// </summary>
    public void Restore()
    {
        if (currentState == State.Hidden || currentState == State.Fading)
        {
            currentState = State.RestoreFading;
            fadeOutStartTime = Time.time;
        }
        else if (currentState == State.Visible)
        {
            // 이미 표시 중이면 타이머만 리셋
            elapsedTime = 0f;
        }
    }

    /// <summary>
    /// 엔트리를 즉시 숨김 (애니메이션 없음)
    /// </summary>
    public void Hide()
    {
        currentState = State.Hidden;
        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 엔트리를 즉시 표시 (애니메이션 없음)
    /// </summary>
    public void Show()
    {
        currentState = State.Visible;
        canvasGroup.alpha = 1f;
        elapsedTime = 0f;
    }

    /// <summary>
    /// 현재 상태 반환
    /// </summary>
    public State GetState()
    {
        return currentState;
    }

    /// <summary>
    /// 설정 값 변경
    /// </summary>
    public void SetTimings(float autoHideDelay, float fadeOutDuration)
    {
        this.autoHideDelay = autoHideDelay;
        this.fadeOutDuration = fadeOutDuration;
    }
}
