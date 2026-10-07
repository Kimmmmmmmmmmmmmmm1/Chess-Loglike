using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MapGenerator
{
    public static MapData GenerateMap(MapConfig config, int seed)
    {
        System.Random rng = new System.Random(seed);
        MapData mapData = new MapData();
        
        // Temporary dictionary for quick lookup during generation
        Dictionary<string, MapNodeData> nodeLookup = new Dictionary<string, MapNodeData>();

        // 1. Path Generation (Bottom-Up)
        // We generate paths first, then collect the unique nodes created by these paths.
        
        // Create Boss Node first (Top)
        int bossY = config.height - 1;
        int bossX = config.width / 2; // Center
        MapNodeData bossNode = new MapNodeData(bossX, bossY);
        bossNode.type = NodeType.Boss;
        bossNode.renderPosX = bossX * config.nodeSpacingX;
        bossNode.renderPosY = bossY * config.nodeSpacingY;
        nodeLookup.Add(bossNode.id, bossNode);
        mapData.bossNodeId = bossNode.id;

        // Generate Paths
        for (int i = 0; i < config.pathCount; i++)
        {
            // Start at random X on floor 0
            int currentX = rng.Next(0, config.width);
            int currentY = 0;

            MapNodeData currentNode = GetOrCreateNode(nodeLookup, currentX, currentY, config);

            // Walk up to the floor below boss
            while (currentY < config.height - 2)
            {
                // Determine next step (Left, Center, Right)
                int nextX = currentX;
                int direction = rng.Next(0, 3); // 0: Left, 1: Center, 2: Right
                
                if (direction == 0) nextX--;
                else if (direction == 2) nextX++;

                // Clamp to grid
                nextX = Mathf.Clamp(nextX, 0, config.width - 1);
                int nextY = currentY + 1;

                MapNodeData nextNode = GetOrCreateNode(nodeLookup, nextX, nextY, config);
                
                // Link
                if (!currentNode.nextNodeIds.Contains(nextNode.id))
                {
                    currentNode.nextNodeIds.Add(nextNode.id);
                }

                currentNode = nextNode;
                currentX = nextX;
                currentY = nextY;
            }

            // Finally, link the top of this path to the Boss
            if (!currentNode.nextNodeIds.Contains(bossNode.id))
            {
                currentNode.nextNodeIds.Add(bossNode.id);
            }
        }

        mapData.nodes = nodeLookup.Values
            .OrderBy(n => n.floor)
            .ThenBy(n => n.index)
            .ToList();

        // 2. Type Assignment
        foreach (var node in mapData.nodes)
        {
            if (node.type != NodeType.None) continue; // Skip if already assigned (like Boss)

            if (node.floor == 0)
            {
                node.type = NodeType.Battle; // Fixed Rule: Floor 1 is Battle
            }
            else if (node.floor == config.height - 2)
            {
                // Floor before boss는 기본 Shop이지만, 연속 Shop은 금지
                List<MapNodeData> parentNodes = GetParentNodes(node, nodeLookup);
                bool hasShopParent = parentNodes.Any(p => p.type == NodeType.Shop);
                node.type = hasShopParent ? NodeType.Battle : NodeType.Shop;
            }
            else
            {
                // 부모 노드들 찾기 (현재 노드를 nextNodeIds에 포함하는 노드들)
                List<MapNodeData> parentNodes = GetParentNodes(node, nodeLookup);
                bool hasShopParent = parentNodes.Any(p => p.type == NodeType.Shop);
                bool hasTreasureParent = parentNodes.Any(p => p.type == NodeType.Treasure);

                // 제외할 타입들을 고려하여 가중치 조정
                float battleWeight = config.battleWeight;
                float shopWeight = hasShopParent ? 0f : config.shopWeight;
                float treasureWeight = hasTreasureParent ? 0f : config.treasureWeight;
                float eventWeight = config.eventWeight;
                float workShopWeight = config.workShopWeight;
                float mysteryWeight = config.mysteryWeight;

                float totalWeight = battleWeight + shopWeight + treasureWeight + eventWeight + workShopWeight + mysteryWeight;
                
                // 총 가중치가 0이면 (모든 타입이 제외된 경우) Battle로 설정
                if (totalWeight <= 0)
                {
                    node.type = NodeType.Battle;
                }
                else
                {
                    double roll = rng.NextDouble();
                    double battleThreshold = battleWeight / totalWeight;
                    double shopThreshold = battleThreshold + (shopWeight / totalWeight);
                    double treasureThreshold = shopThreshold + (treasureWeight / totalWeight);
                    double eventThreshold = treasureThreshold + (eventWeight / totalWeight);
                    double workShopThreshold = eventThreshold + (workShopWeight / totalWeight);

                    if (roll < battleThreshold)
                    {
                        node.type = NodeType.Battle;
                    }
                    else if (roll < shopThreshold)
                    {
                        node.type = NodeType.Shop;
                    }
                    else if (roll < treasureThreshold)
                    {
                        node.type = NodeType.Treasure;
                    }
                    else if (roll < eventThreshold)
                    {
                        node.type = NodeType.Event;
                    }
                    else if (roll < workShopThreshold)
                    {
                        node.type = NodeType.WorkShop;
                    }
                    else
                    {
                        node.type = NodeType.Mystery;
                    }
                }
            }
        }

        // 3. Jittering
        foreach (var node in mapData.nodes)
        {
            // Don't jitter the boss to keep it centered and majestic
            if (node.type == NodeType.Boss) continue;

            float jitterX = (float)(rng.NextDouble() * 2 - 1) * config.positionJitter;
            float jitterY = (float)(rng.NextDouble() * 2 - 1) * config.positionJitter;

            node.renderPosX += jitterX;
            node.renderPosY += jitterY;
        }

        return mapData;
    }

    private static List<MapNodeData> GetParentNodes(MapNodeData node, Dictionary<string, MapNodeData> nodeLookup)
    {
        List<MapNodeData> parents = new List<MapNodeData>();
        foreach (var potentialParent in nodeLookup.Values)
        {
            if (potentialParent.nextNodeIds.Contains(node.id))
            {
                parents.Add(potentialParent);
            }
        }
        return parents;
    }

    private static MapNodeData GetOrCreateNode(Dictionary<string, MapNodeData> lookup, int x, int y, MapConfig config)
    {
        string id = $"{x}_{y}";
        if (!lookup.ContainsKey(id))
        {
            var node = new MapNodeData(x, y);
            // Set initial visual position based on grid
            node.renderPosX = x * config.nodeSpacingX;
            node.renderPosY = y * config.nodeSpacingY;
            lookup.Add(id, node);
        }
        return lookup[id];
    }
}
