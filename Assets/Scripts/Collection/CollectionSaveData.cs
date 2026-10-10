using System;
using System.Collections.Generic;

public enum CollectionDiscoveryState
{
    Unseen = 0,
    Seen = 1,
    Acquired = 2
}

[Serializable]
public class CollectionSaveData
{
    // Stage 3: Acquired (preserved for backwards compatibility)
    public List<string> unlockedArtifactIDs = new List<string>();
    public List<string> unlockedSealIDs = new List<string>();
    public List<string> unlockedPieceIDs = new List<string>();

    // Stage 2: Seen
    public List<string> seenArtifactIDs = new List<string>();
    public List<string> seenSealIDs = new List<string>();
    public List<string> seenPieceIDs = new List<string>();
}
