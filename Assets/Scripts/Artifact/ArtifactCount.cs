using UnityEngine;
using UnityEngine.UI;

public class ArtifactCount : MonoBehaviour
{
    [SerializeField] private Image CheckImage;
    [SerializeField] private Image CheckImage2;

    public void SetFillCount(int fillCount)
    {
        int safeFillCount = Mathf.Clamp(fillCount, 0, 2);

        if (CheckImage != null)
        {
            CheckImage.gameObject.SetActive(safeFillCount >= 1);
        }

        if (CheckImage2 != null)
        {
            CheckImage2.gameObject.SetActive(safeFillCount >= 2);
        }
    }
}
