using System;
using System.Collections.Generic;
using System.Text;
using Jain;

namespace Integration
{
    // Pure rules are shared with the offline checks; no Unity runtime is needed.
    public static class CampaignRules
    {
        public const int MaxRanks = 5;
        public static string NormalizePlayerId(string value)
        {
            var clean = new StringBuilder();
            foreach (char c in (value ?? "").Trim())
            {
                // Prevent multiline/rich-text names from changing the team's UI.
                if (char.IsControl(c) || c == '<' || c == '>') continue;
                if (clean.Length >= 16) break;
                clean.Append(c);
            }
            return clean.Length == 0 ? "Player" : clean.ToString();
        }
        public static void SortRanks(List<GameDataManager.RankData> ranks)
        {
            ranks.Sort((a, b) =>
            {
                int score = b.stageScore.CompareTo(a.stageScore);
                return score != 0 ? score : StringComparer.Ordinal.Compare(a.playerName, b.playerName);
            });
            if (ranks.Count > MaxRanks) ranks.RemoveRange(MaxRanks, ranks.Count - MaxRanks);
        }
        public static bool IsFunctionKey(int key, int f1, int f12) => key >= f1 && key <= f12;
    }
}
