using System;
using System.Collections.Generic;

[Serializable]
public enum NodeType
{
    None = 0,
    Battle = 1,
    Shop = 2,
    Treasure = 3,
    Boss = 4,
    Mystery = 5,
    Event = 6,
    WorkShop = 7
}

[Serializable]
public class MapNodeData
{
    public string id;           // Format: "x_y"
    public int floor;           // Y
    public int index;           // X
    public NodeType type;
    public float renderPosX;
    public float renderPosY;
    public List<string> nextNodeIds;

    public MapNodeData(int x, int y)
    {
        this.index = x;
        this.floor = y;
        this.id = $"{x}_{y}";
        this.type = NodeType.None;
        this.nextNodeIds = new List<string>();
    }
}

[Serializable]
public class MapData
{
    public List<MapNodeData> nodes;
    public string bossNodeId;

    public MapData()
    {
        nodes = new List<MapNodeData>();
    }
}
