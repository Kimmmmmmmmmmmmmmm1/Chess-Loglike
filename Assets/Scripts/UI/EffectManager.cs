using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("Settings")]
    public GameObject debrisPrefab;
    public Transform debrisParent; // Canvas 혹은 BoardPanel
    public int debrisCount = 6;    // 파편 개수
    [SerializeField] private float spreadRadiusMultiplier = 2.5f; // 그리드 셀 크기 대비 파편 반경 비율
    private Image flashImage;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayExplosion(Vector2 worldPos, Color pieceColor, bool isEnemy, float scale = 1f)
    {
        int count = Mathf.RoundToInt(debrisCount * scale);
        for (int i = 0; i < count; i++)
        {
            SpawnDebris(worldPos, pieceColor, isEnemy, scale, 1f, 1f, 0f);
        }
    }

    public void PlayCollapseExplosion(Vector2 worldPos, Color pieceColor, bool isEnemy, float scale = 1f)
    {
        int count = Mathf.RoundToInt(debrisCount * scale * 1.2f);
        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / count;
            float spreadMultiplier = Mathf.Lerp(1.4f, 2.2f, t);
            float speedMultiplier = Mathf.Lerp(1.0f, 1.6f, t);
            float delay = i * 0.02f;

            SpawnDebris(worldPos, pieceColor, isEnemy, scale, spreadMultiplier, speedMultiplier, delay);
        }
    }

    public void PlaySlowMotion()
    {
        float restoreSpeed = 1f;
        if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
        {
            restoreSpeed = Mathf.Max(0.01f, SettingsManager.Instance.Settings.gameSpeed);
        }

        DOTween.timeScale = 0.1f;
        DOTween.To(() => DOTween.timeScale, x => DOTween.timeScale = x, restoreSpeed, 1f)
            .SetUpdate(true)
            .SetEase(Ease.OutQuad);
    }

    public void PlayScreenFlash(float duration = 0.15f, float maxAlpha = 0.6f)
    {
        if (flashImage == null)
        {
            CreateFlashImage();
        }

        if (flashImage != null)
        {
            flashImage.color = new Color(1f, 1f, 1f, maxAlpha);
            flashImage.gameObject.SetActive(true);
            flashImage.DOFade(0f, duration).SetEase(Ease.OutQuad).OnComplete(() => flashImage.gameObject.SetActive(false));
        }
    }

    private void CreateFlashImage()
    {
        // debrisParent가 있는 캔버스를 찾습니다.
        Canvas canvas = debrisParent != null ? debrisParent.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("FlashOverlay");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling(); // 최상단에 표시

        flashImage = go.AddComponent<Image>();
        flashImage.color = Color.white;
        flashImage.raycastTarget = false; // 클릭 방해하지 않음

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.SetActive(false);
    }

    public void PlayCameraShake(float duration = 0.2f, float strength = 0.3f, int vibrato = 20)
    {
        // UI 모드(Screen Space - Overlay)일 경우 카메라를 흔들어도 효과가 없으므로
        // debrisParent(보드 패널)가 RectTransform이라면 대신 흔듭니다.
        if (debrisParent != null && debrisParent.GetComponent<RectTransform>() != null)
        {
            RectTransform target = debrisParent.GetComponent<RectTransform>();
            target.DOComplete();
            // UI 좌표계에서는 0.x 단위가 너무 작으므로 강도를 보정합니다 (예: 0.5 -> 25)
            target.DOShakeAnchorPos(duration, strength * 50f, vibrato, 90, false, true);
        }
        else if (Camera.main != null)
        {
            Camera.main.transform.DOComplete(); // 이미 흔들리고 있다면 즉시 완료 처리
            Camera.main.transform.DOShakePosition(duration, strength, vibrato, 90, false, true);
        }
    }

    public void PlayLandingEffect(Vector2 worldPos)
    {
        for (int i = 0; i < 5; i++)
        {
            SpawnDust(worldPos);
        }
    }

    private void SpawnDust(Vector2 pos)
    {
        if (debrisPrefab == null) return;

        GameObject dust = Instantiate(debrisPrefab, debrisParent);
        RectTransform rt = dust.GetComponent<RectTransform>();
        Image img = dust.GetComponent<Image>();

        rt.position = pos;
        
        // 먼지 느낌: 회색, 반투명
        img.color = new Color(0.9f, 0.9f, 0.9f, 0.6f);
        rt.localScale = Vector3.one * Random.Range(0.3f, 0.6f);

        // 퍼지는 애니메이션
        Vector2 dir = Random.insideUnitCircle * Random.Range(30f, 60f);
        float duration = Random.Range(0.3f, 0.5f);

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOAnchorPos(rt.anchoredPosition + dir, duration).SetEase(Ease.OutQuad));
        seq.Join(img.DOFade(0f, duration));
        seq.Join(rt.DOScale(0f, duration));
        seq.OnComplete(() => Destroy(dust));
    }

    private void SpawnDebris(Vector2 startWorldPos, Color color, bool isEnemy, float scale, float spreadMultiplier, float speedMultiplier, float startDelay)
    {
        // 1. 생성
        if (debrisPrefab == null) return;
        if (debrisParent == null) return;

        GameObject debris = Instantiate(debrisPrefab, debrisParent);
        RectTransform rt = debris.GetComponent<RectTransform>();
        Image img = debris.GetComponent<Image>();

        // 2. 초기 위치 잡기
        rt.position = startWorldPos; 
        
        // 색상 선택 (섞지 않고 랜덤 선택)
        Color tintColor = isEnemy ? Color.blue : Color.red;
        // 하얀색(color):기물색(tintColor) 의 비를 random(3~5):1정도로 설정
        float whiteRatio = Random.Range(3f, 5f);
        Color selectedColor = Random.value < (1f / (whiteRatio + 1f)) ? tintColor : color;
        
        float brightness = Random.Range(0.8f, 1.0f);
        
        img.color = new Color(selectedColor.r * brightness, selectedColor.g * brightness, selectedColor.b * brightness, selectedColor.a);
                
        // 크기 랜덤 (2배 키움)
        float randomScale = Random.Range(1.2f, 1.8f) * scale;
        rt.localScale = Vector3.one * randomScale;

        // ---------------------------------------------------------
        // ★★★ 보드 위에 흩뿌리기 로직 (Fake 3D) ★★★
        // ---------------------------------------------------------

        // 3. 목표 지점 계산 (죽은 위치 주변 랜덤)
        // gridManager가 없을 경우 fallback 셀 크기 사용
        float cellX = (PieceManager.Instance != null && PieceManager.Instance.gridManager != null)
            ? PieceManager.Instance.gridManager.cellSize.x
            : 42f;
        float spreadRadius = cellX * spreadRadiusMultiplier * spreadMultiplier;

        Vector2 randomDir = Random.insideUnitCircle * spreadRadius * scale;
        
        // 현재 위치(AnchoredPosition)를 기준으로 목표지점 설정
        Vector2 startAnchoredPos = rt.anchoredPosition;
        Vector2 targetAnchoredPos = startAnchoredPos + randomDir;

        // 4. 점프 높이 및 시간 설정
        float jumpHeight = Random.Range(50f, 100f) * scale; 
        float horizontalDuration = Random.Range(0.5f, 0.7f) / speedMultiplier;

        // 5. DOTween 시퀀스
        Sequence seq = DOTween.Sequence();

        // ---------------------------------------------------------
        // ★★★ 낙하 로직 분기 (보드 안/밖) ★★★
        // ---------------------------------------------------------
        if (IsPositionOnBoard(startWorldPos))
        {
            // [보드 안] 기존처럼 점프 후 착지
            seq.Append(rt.DOJumpAnchorPos(targetAnchoredPos, jumpHeight, 1, horizontalDuration).SetEase(Ease.Linear));
            seq.Join(rt.DORotate(new Vector3(0, 0, Random.Range(-180f, 180f)), horizontalDuration));
            
            // 착지 후 바닥에 머물다 사라짐
            seq.AppendInterval(0.3f); 
            seq.Append(img.DOFade(0f, 0.5f)); 
        }
        else
        {
            // [보드 밖] 끊김 없는 포물선 낙하 (X, Y 분리 애니메이션)
            float fallDistance = 600f;
            
            // Y축: 위로 솟았다가(OutQuad) 아래로 가속하며 떨어짐(InQuad) -> 자연스러운 중력 효과
            float peakY = startAnchoredPos.y + jumpHeight;
            float abyssY = targetAnchoredPos.y - fallDistance;
            
            // 시간 계산 (올라가는 시간 vs 떨어지는 시간)
            float riseTime = horizontalDuration * 0.4f;
            // 떨어지는 거리가 훨씬 길므로 시간도 비례해서 계산 (루트 근사치)
            float fallTime = riseTime * Mathf.Sqrt((jumpHeight + fallDistance) / jumpHeight);
            float totalDuration = riseTime + fallTime;

            // X축: 등속 운동으로 멀리 날아감
            Vector2 momentum = randomDir.normalized * 60f;
            float finalX = targetAnchoredPos.x + momentum.x;
            seq.Insert(0, rt.DOAnchorPosX(finalX, totalDuration).SetEase(Ease.Linear));

            // Y축 상승 (감속)
            seq.Insert(0, rt.DOAnchorPosY(peakY, riseTime).SetEase(Ease.OutQuad));
            // Y축 하강 (가속, 바닥 통과)
            seq.Insert(riseTime, rt.DOAnchorPosY(abyssY, fallTime).SetEase(Ease.InQuad));
            
            // 회전 및 크기 조절
            seq.Insert(0, rt.DORotate(new Vector3(0, 0, Random.Range(-360f, 360f)), totalDuration, RotateMode.FastBeyond360));
            seq.Insert(riseTime, rt.DOScale(0.2f, fallTime)); // 떨어질 때 작아짐
            seq.Insert(totalDuration - 0.2f, img.DOFade(0f, 0.2f)); // 끝부분에서 페이드 아웃
        }
        
        if (startDelay > 0f)
        {
            seq.SetDelay(startDelay);
        }

        // 6. 삭제
        seq.OnComplete(() => Destroy(debris));
    }

    private bool IsPositionOnBoard(Vector2 worldPos)
    {
        if (PieceManager.Instance == null) return true;

        GridManager gridManager = PieceManager.Instance.gridManager;
        if (gridManager == null) return true;

        // boardContainer가 있으면 world → boardContainer 로컬 좌표로 변환
        RectTransform boardRT = gridManager.boardContainer;
        if (boardRT != null)
        {
            // world position → boardContainer의 anchoredPosition 기준 로컬 좌표
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardRT,
                worldPos,
                null,
                out localPos
            );
            Vector2Int? gridPos = gridManager.GetNearestGridPosition(localPos);
            return gridPos.HasValue;
        }

        // fallback: 직접 anchoredPosition으로 판단 (좌표 공간이 같을 때만 정확)
        Vector2Int? fallbackPos = gridManager.GetNearestGridPosition(worldPos);
        return fallbackPos.HasValue;
    }
}