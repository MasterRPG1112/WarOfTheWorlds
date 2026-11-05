using System;
using System.Collections.Generic;
using System.Reflection;
using Integration;
using Jain;

// Exercises the actual compiled save validator/ranking rules outside Unity.
// No scenes, MonoBehaviours, JsonUtility or native Unity runtime are executed.
public static class CampaignLogicChecks
{
    static int passed;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
        passed++;
    }
    static bool Validate(SaveFile file)
    {
        return (bool)typeof(IntegrationSaveStore).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { file });
    }
    public static int Main()
    {
        try
        {
            foreach (int stage in new[] { 2, 3 })
            {
                var snapshot = new Checkpoint { stage = stage, coreHp = 6, money = 2300, score = 4500,
                    part1 = 1, part2 = 1, part3 = 1, pull = 4, slow = 3, reflex = 2, stop = 1, shields = 2, practice = true };
                var save = new SaveFile { checkpoint = snapshot };
                Check(Validate(save) && save.checkpoint.stage == stage, "Stage " + stage + " checkpoint stays on its own stage");
                Check(snapshot.part1 == 1 && snapshot.part2 == 1 && snapshot.part3 == 1 && snapshot.money == 2300 && snapshot.score == 4500,
                    "Stage " + stage + " checkpoint retains purchased parts, money and score");
                Check(snapshot.pull == 4 && snapshot.slow == 3 && snapshot.reflex == 2 && snapshot.stop == 1 && snapshot.shields == 2 && snapshot.practice,
                    "Stage " + stage + " checkpoint retains all four inventories and practice status");
            }
            var legacy = new SaveFile { version = 1, checkpoint = new Checkpoint { stage = 2, coreHp = 20, pull = 2, patchSlot0 = 0, patchSlot1 = 0 } };
            Check(Validate(legacy) && legacy.version == 4 && legacy.checkpoint.coreHp == 8, "Version 1 checkpoint migrates with shared HP capped at 8");
            Check(legacy.checkpoint.pull == 2 && legacy.checkpoint.ownedMask == 1 && legacy.checkpoint.patchSlot0 == -1 && legacy.checkpoint.patchSlot1 == -1,
                "Legacy migration preserves real stock without granting slot ownership");
            var oldParts = new SaveFile { version = 3, checkpoint = new Checkpoint { part1 = 1, part2 = 1, part3 = 1, equippedPartsMask = 0 } };
            Check(Validate(oldParts) && oldParts.checkpoint.equippedPartsMask == 7, "Version 3 owned parts migrate as equipped to preserve existing effects");
            var unequipped = new SaveFile { version = 4, checkpoint = new Checkpoint { part2 = 1, part3 = 1, equippedPartsMask = 4 } };
            Check(Validate(unequipped) && unequipped.checkpoint.equippedPartsMask == 4, "Current save retains intentionally unequipped owned parts");
            var corrupt = new SaveFile { checkpoint = new Checkpoint { stage = 9 }, volume = float.NaN };
            Check(!Validate(corrupt) && corrupt.checkpoint == null && corrupt.volume == 1, "Invalid stage and volume cannot create a corrupt continuation");
            var negatives = new SaveFile { checkpoint = new Checkpoint { coreHp = -1, money = -1, score = -2, pull = -5, shields = 999 } };
            Check(Validate(negatives) && negatives.checkpoint.coreHp == 1 && negatives.checkpoint.money == 0 && negatives.checkpoint.pull == 0 && negatives.checkpoint.shields <= 9,
                "Malformed counters are sanitized without creating negative stock");
            var ranks = new List<GameDataManager.RankData>();
            for (int i = 0; i < 9; i++) ranks.Add(new GameDataManager.RankData("P" + i, i * 100));
            ranks.Add(new GameDataManager.RankData("Winner", 9000));
            ranks.Add(new GameDataManager.RankData("Tie", 800));
            var rankedSave = new SaveFile { ranks = ranks };
            Check(Validate(rankedSave) && ranks.Count == 5 && ranks[0].playerName == "Winner", "Only the five best scores survive ranking validation");
            bool descending = true;
            for (int i = 1; i < ranks.Count; i++) descending &= ranks[i-1].stageScore >= ranks[i].stageScore;
            Check(descending, "Leaderboard is sorted by score descending including ties");
            Check(CampaignRules.NormalizePlayerId("  <b>A\nB</b>  ") == "bAB/b", "Player IDs cannot inject rich text or new lines");
            Check(CampaignRules.NormalizePlayerId("   ") == "Player" && CampaignRules.NormalizePlayerId(new string('x', 40)).Length == 16,
                "Empty IDs get a safe default and long IDs are bounded");
            Check(CampaignRules.IsFunctionKey(282, 282, 293) && CampaignRules.IsFunctionKey(293, 282, 293) && !CampaignRules.IsFunctionKey(294, 282, 293),
                "Cheat bindings accept only the configured F1 through F12 range");
            Console.WriteLine("RESULT " + passed + " passed; no Unity runtime invoked.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
