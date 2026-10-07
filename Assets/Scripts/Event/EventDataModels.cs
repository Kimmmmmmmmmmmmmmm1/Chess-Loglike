using System;
using System.Collections.Generic;

[Serializable]
public class EventDatabaseData
{
    public List<EventData> events = new List<EventData>();
}

[Serializable]
public class EventData
{
    public string eventId;
    public string title;
    public string startNodeId;
    public string defaultImagePath;
    public List<EventNodeData> nodes = new List<EventNodeData>();
}

[Serializable]
public class EventNodeData
{
    public string nodeId;
    public string dialogue;
    public string imagePath;
    public List<EventChoiceData> choices = new List<EventChoiceData>();
}

[Serializable]
public class EventChoiceData
{
    public string text;
    public string nextNodeId;
    public bool endEvent;
    public EventChoiceEffectData effect = new EventChoiceEffectData();
}

[Serializable]
public class EventChoiceEffectData
{
    public string effectType = "None";

    public int goldAmount;

    public string artifactId;
    public List<string> artifactIds = new List<string>();

    public string pieceType;
    public List<string> pieceTypes = new List<string>();
    public string sealName;
    public int seal; // 0: no seal, 1: random, 2: always give seal

    public string removeArtifactId;
    public string removePieceType;
}

public enum EventEffectType
{
    None,
    GainGold,
    LoseGold,
    GainSpecificArtifact,
    GainRandomArtifact,
    GainRandomSpecificArtifact,
    GainSpecificPiece,
    GainRandomPiece,
    GainRandomSpecificPiece,
    RemoveSpecificArtifact,
    RemoveRandomArtifact,
    RemoveSpecificPiece,
    RemoveRandomPiece,
    GainSeal
}
