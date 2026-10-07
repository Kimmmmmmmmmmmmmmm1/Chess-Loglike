using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MapConfig", menuName = "Map/Map Config")]
public class MapConfig : ScriptableObject
{
    [Header("Grid Settings")]
    public int width = 5;
    public int height = 10; // Floors including Boss
    public int pathCount = 3; // Number of starting paths
    
    [Header("Visuals")]
    public float nodeSpacingX = 1.5f; // 노드 x 간격
    public float nodeSpacingY = 2f; // 노드 y 간격
    public float positionJitter = 0.3f; // 지터링
    public float nodeSize = 0.7f; // 노드 크기 (1.0 = 프리팹 원본 크기)
    public float lineThickness = 0.1f; // 선 두께
    public float lineGap = 0.5f; // 노드와 선 사이의 간격

    [Header("Stage Names")]
    public List<string> stageNames = new List<string> { "Stage 1", "Stage 2", "Stage 3" };

    [Header("Probabilities (Must sum to 1.0 roughly)")]
    [Range(0, 1)] public float battleWeight = 0.5f;
    [Range(0, 1)] public float shopWeight = 0.15f;
    [Range(0, 1)] public float treasureWeight = 0.15f;
    [Range(0, 1)] public float eventWeight = 0.1f;
    [Range(0, 1)] public float workShopWeight = 0.05f;
    [Range(0, 1)] public float mysteryWeight = 0.05f;
}
