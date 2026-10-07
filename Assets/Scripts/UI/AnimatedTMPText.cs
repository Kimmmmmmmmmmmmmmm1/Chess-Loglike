using UnityEngine;
using TMPro;

// 기존 TextMeshProUGUI를 상속받아 모든 기능을 그대로 가져옵니다!
public class AnimatedTMPText : TextMeshProUGUI
{
    [Header("Wave Settings (<link=wavy>)")]
    public float waveSpeed = 10f;
    public float waveAmount = 1f;
    public float waveFrequency = 1f;

    [Header("Jitter Settings (<link=jitter>)")]
    public float jitterAmount = 0.5f;

    // Update()를 돌려 애니메이션 처리 (로직은 기존과 동일)
    void Update()
    {
        this.ForceMeshUpdate();
        TMP_TextInfo info = this.textInfo;

        if (info.linkCount == 0) return;

        for (int i = 0; i < info.linkCount; i++)
        {
            TMP_LinkInfo linkInfo = info.linkInfo[i];
            string linkId = linkInfo.GetLinkID();

            bool isWavy = linkId == "wavy";
            bool isJitter = linkId == "jitter";

            if (!isWavy && !isJitter) continue;

            for (int j = 0; j < linkInfo.linkTextLength; j++)
            {
                int charIndex = linkInfo.linkTextfirstCharacterIndex + j;
                TMP_CharacterInfo charInfo = info.characterInfo[charIndex];

                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                Vector3[] vertices = info.meshInfo[materialIndex].vertices;

                Vector3 offset = Vector3.zero;

                if (isWavy)
                {
                    float waveOffset = Mathf.Sin(Time.time * waveSpeed + charIndex * waveFrequency) * waveAmount;
                    offset += new Vector3(0, waveOffset, 0);
                }
                
                if (isJitter)
                {
                    offset += new Vector3(Random.Range(-jitterAmount, jitterAmount), Random.Range(-jitterAmount, jitterAmount), 0);
                }

                for (int v = 0; v < 4; v++)
                {
                    vertices[vertexIndex + v] += offset;
                }
            }
        }

        for (int i = 0; i < info.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = info.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            this.UpdateGeometry(meshInfo.mesh, i);
        }
    }
}