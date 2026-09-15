namespace LQFarm.EditorTools
{
    /// <summary>A real save written before the 2026-09-15 economy: the level-12 developer farm (286.400 coins,
    /// 1.200 XP, three islands in the OLD island order, carrots of 35 s in the ground, no "econV" and no
    /// "islandsV" key). Default plot fields are dropped to keep it short; everything else is as written.
    /// Used by the save test (the rescale) and the journey test (that such a farm can play on).</summary>
    public static class LegacySaveFixture
    {
        public const string Level12 =
            "{\"v\":2,\"minV\":2,\"worldSeed\":\"1ca98b8a2a462dd1\",\"clock\":{\"lastSeenUtc\":1789293921315,"
            + "\"savedAtUtc\":1789293921315,\"savedAtMono\":155.3999786376953,\"resetOffsetMinutes\":0,"
            + "\"clockSuspect\":false},\"player\":{\"ownerId\":\"\",\"displayName\":\"Nông dân\",\"level\":12,\"xp\":1200,"
            + "\"coin\":286400,\"energy\":222},\"islands\":[{\"id\":0,\"unlocked\":true,\"unlockedAt\":0,"
            + "\"plots\":[{\"locked\":false,\"crop\":\"carrot\",\"plantedAt\":1789293747238,\"dur\":35.0,\"windowCount\":1},"
            + "{\"locked\":false,\"crop\":\"wheat\",\"plantedAt\":1789293762238,\"dur\":48.0,\"windowCount\":1},"
            + "{\"locked\":false,\"crop\":\"tomato\",\"plantedAt\":1789293769738,\"dur\":70.0,\"windowCount\":1,\"variant\":3},"
            + "{\"locked\":false,\"crop\":\"carrot\",\"plantedAt\":1789293747238,\"dur\":35.0,\"windowCount\":1},"
            + "{\"locked\":false,\"crop\":\"wheat\",\"plantedAt\":1789293762238,\"dur\":48.0,\"windowCount\":1},"
            + "{\"locked\":false,\"crop\":\"tomato\",\"plantedAt\":1789293769738,\"dur\":70.0,\"cut\":14.0,\"waterMask\":1,"
            + "\"windowCount\":1},{\"locked\":false,\"crop\":\"carrot\",\"plantedAt\":1789293747238,\"dur\":35.0,"
            + "\"windowCount\":1},{\"locked\":false},{\"locked\":false},{\"locked\":false},{\"locked\":false},"
            + "{\"locked\":false},{},{},{},{}]},{\"id\":1,\"unlocked\":true,\"unlockedAt\":1789293787227,\"plots\":[{},{},{},"
            + "{},{},{\"locked\":false},{\"locked\":false},{},{},{\"locked\":false},{\"locked\":false},{},{},{},{},{}]},"
            + "{\"id\":2,\"unlocked\":true,\"unlockedAt\":1789293787227,\"plots\":[{},{},{},{},{},{\"locked\":false},"
            + "{\"locked\":false},{},{},{\"locked\":false},{\"locked\":false},{},{},{},{},{}]},{\"id\":3,\"unlocked\":false,"
            + "\"unlockedAt\":0,\"plots\":[{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{}]},{\"id\":4,\"unlocked\":false,"
            + "\"unlockedAt\":0,\"plots\":[{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{}]},{\"id\":5,\"unlocked\":false,"
            + "\"unlockedAt\":0,\"plots\":[{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{}]}],"
            + "\"inventory\":{\"seeds\":{\"carrot\":26,\"wheat\":24,\"tomato\":22},\"produce\":{}},\"chests\":{\"owned\":[1,0,0,"
            + "0]},\"progress\":{\"chapter\":{},\"daily\":{},\"dailyBucket\":20709,\"buffMutateUntil\":0,\"forecastUntil\":0,"
            + "\"greenhouse\":0,\"contracts\":[{\"slot\":0,\"genCycle\":3727694,\"type\":\"harvest\",\"cropId\":\"carrot\","
            + "\"grade\":\"Bronze\",\"need\":6,\"p\":0,\"claimed\":false,\"expiresAt\":1789295121459,\"refillAt\":0,"
            + "\"Empty\":false,\"Done\":false},{\"slot\":1,\"genCycle\":3727694,\"type\":\"harvest\",\"grade\":\"Gold\",\"need\":24,"
            + "\"p\":0,\"claimed\":false,\"expiresAt\":1789296921459,\"refillAt\":0,\"Empty\":false,\"Done\":false},{\"slot\":2,"
            + "\"genCycle\":3727694,\"type\":\"harvest\",\"cropId\":\"wheat\",\"grade\":\"Bronze\",\"need\":6,\"p\":0,"
            + "\"claimed\":false,\"expiresAt\":1789295121459,\"refillAt\":0,\"Empty\":false,\"Done\":false}],\"streak\":0,"
            + "\"sinceDiamond\":3,\"sinceLegendary\":0,\"contractsToday\":0,\"collected\":[],\"claimedSets\":[],"
            + "\"claimedMilestones\":[]},\"social\":{\"visited\":[],\"shopBought\":[],\"equipped\":{},\"stealBudgetLeft\":20},"
            + "\"stats\":{\"harvest\":0,\"plant\":0,\"water\":0,\"sell\":0,\"chest\":0,\"visit\":0,\"mutate\":0},"
            + "\"meta\":{\"build\":\"1.0\",\"platform\":\"OSXEditor\"}}";
    }
}
