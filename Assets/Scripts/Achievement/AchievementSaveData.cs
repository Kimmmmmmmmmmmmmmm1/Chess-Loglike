using System;
using System.Collections.Generic;

[Serializable]
public class AchievementSaveData
{
    public List<AchievementProgressData> achievements = new List<AchievementProgressData>();
}

[Serializable]
public class AchievementProgressData
{
    public string achievementId;
    public int currentCount;
    public long clearState;
    
}