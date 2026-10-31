using System;
using System.Collections.Generic;
using System.IO;
using Jain;
using UnityEngine;

namespace Integration
{
    // The checkpoint is a stage-start snapshot, not a suspended enemy/wave simulation.
    [Serializable]
    public class Checkpoint
    {
        public int stage = 1;
        public int coreHp = 8;
        public int leftHp = 5;
        public int rightHp = 5;
        public int score;
        public int money;
        public int part1;
        public int part2;
        public int part3;
        public int stop;
        public int reflex;
        public int pull;
        public int slow;
        // Retained solely so v1 JSON and older Editor sources remain readable.
        public int selectedSkill = -1;
        public int ownedMask;
        public int patchSlot0 = -1;
        public int patchSlot1 = -1;
    }

    [Serializable]
    public class SaveFile
    {
        public int version = 2;
        public Checkpoint checkpoint;
        public List<GameDataManager.RankData> ranks = new List<GameDataManager.RankData>();
        public float volume = 1;
        public bool fullscreen;
    }

    public static class IntegrationSaveStore
    {
        public static bool SuppressDiskWrites;
        public static bool ContinueRequested;
        // Validation redirects every read/write to an explicit scratch file, never the user save.
        public static string ValidationPathOverride;
        public static string FilePath => string.IsNullOrEmpty(ValidationPathOverride)
            ? Path.Combine(Application.persistentDataPath, "TaeyangIntegration-v1.json")
            : ValidationPathOverride;

        static SaveFile data;
        static bool rejectedDiskFile;
        static int testDepth;
        static string originalTestData;
        static string testSavedData;
        static bool originalTestContinue;
        static bool originalTestSuppression;
        static bool originalTestRejected;
        static string originalTestPathOverride;

        public static SaveFile Data
        {
            get
            {
                if (data == null) Reload();
                return data;
            }
        }

        public static bool HasCheckpoint => Data.checkpoint != null;

        public static void Reload()
        {
            if (testDepth > 0)
            {
                // Test save/reload round trips stay entirely in memory.
                data = string.IsNullOrEmpty(testSavedData) ? new SaveFile() : JsonUtility.FromJson<SaveFile>(testSavedData);
                Validate(data);
                return;
            }

            data = new SaveFile();
            rejectedDiskFile = false;
            try
            {
                if (!File.Exists(FilePath)) return;
                string json = File.ReadAllText(FilePath).Trim();
                if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
                    throw new FormatException("The save is not a JSON object.");
                SaveFile loaded = JsonUtility.FromJson<SaveFile>(json);
                if (loaded == null || (loaded.version != 1 && loaded.version != 2))
                    throw new FormatException("The save version is unsupported.");
                rejectedDiskFile = !Validate(loaded);
                data = loaded;
                if (rejectedDiskFile)
                    Debug.LogWarning("An invalid integration checkpoint was ignored. The original save will be retained as a backup.");
            }
            catch (Exception exception)
            {
                rejectedDiskFile = true;
                data = new SaveFile();
                Debug.LogWarning("Integration save could not be read; starting with safe defaults. Original file retained. " + exception.Message);
            }
        }

        public static void Save()
        {
            SaveFile current = Data;
            Validate(current);
            string json = JsonUtility.ToJson(current, true);
            if (testDepth > 0)
            {
                testSavedData = json;
                return;
            }
            if (SuppressDiskWrites) return;

            string temporaryPath = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(FilePath))
                {
                    // A rejected original gets a unique backup, never overwritten by later saves.
                    string backupPath = rejectedDiskFile
                        ? FilePath + ".rejected-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")
                        : FilePath + ".previous";
                    File.Replace(temporaryPath, FilePath, backupPath);
                }
                else File.Move(temporaryPath, FilePath);
                rejectedDiskFile = false;
            }
            catch (Exception exception)
            {
                // Leave the original and any temporary file in place if replacement fails.
                Debug.LogWarning("Integration save could not be written. Existing save retained. " + exception.Message);
            }
        }

        public static void ClearCheckpoint()
        {
            Data.checkpoint = null;
            ContinueRequested = false;
            Save();
        }

        public static void RecordResult(int score)
        {
            Data.ranks.Add(new GameDataManager.RankData("Player", Mathf.Max(0, score)));
            Validate(Data);
            Save();
        }

        public static void ResetForTests()
        {
            if (testDepth == 0)
            {
                originalTestData = JsonUtility.ToJson(Data);
                originalTestContinue = ContinueRequested;
                originalTestSuppression = SuppressDiskWrites;
                originalTestRejected = rejectedDiskFile;
                originalTestPathOverride = ValidationPathOverride;
            }
            testDepth++;
            SuppressDiskWrites = true;
            ContinueRequested = false;
            data = new SaveFile();
            testSavedData = null;
        }

        public static void RestoreAfterTests()
        {
            if (testDepth == 0) return;
            testDepth--;
            if (testDepth > 0) return;
            data = JsonUtility.FromJson<SaveFile>(originalTestData) ?? new SaveFile();
            ContinueRequested = originalTestContinue;
            SuppressDiskWrites = originalTestSuppression;
            rejectedDiskFile = originalTestRejected;
            ValidationPathOverride = originalTestPathOverride;
            originalTestData = null;
            testSavedData = null;
        }

        // Invalid stages disable continuation. HP is one pool; v1 wing HP and the
        // former free selected skill have no gameplay meaning after migration.
        static bool Validate(SaveFile save)
        {
            bool validCheckpoint = true;
            if (save.checkpoint != null)
            {
                Checkpoint checkpoint = save.checkpoint;
                if (checkpoint.stage < 1 || checkpoint.stage > 3)
                {
                    save.checkpoint = null;
                    validCheckpoint = false;
                }
                else
                {
                    checkpoint.coreHp = Mathf.Clamp(checkpoint.coreHp, 1, 8);
                    checkpoint.score = Mathf.Max(0, checkpoint.score);
                    checkpoint.money = Mathf.Max(0, checkpoint.money);
                    checkpoint.part1 = Mathf.Max(0, checkpoint.part1);
                    checkpoint.part2 = Mathf.Max(0, checkpoint.part2);
                    checkpoint.part3 = Mathf.Max(0, checkpoint.part3);
                    checkpoint.stop = Mathf.Max(0, checkpoint.stop);
                    checkpoint.reflex = Mathf.Max(0, checkpoint.reflex);
                    checkpoint.pull = Mathf.Max(0, checkpoint.pull);
                    checkpoint.slow = Mathf.Max(0, checkpoint.slow);
                    // v1 had no equipment slots. Ignore default zero slot values
                    // from absent JSON fields rather than granting a free patch.
                    if (save.version == 1)
                    {
                        checkpoint.ownedMask = 0;
                        checkpoint.patchSlot0 = checkpoint.patchSlot1 = -1;
                    }
                    checkpoint.ownedMask &= 15;
                    if (checkpoint.pull > 0) checkpoint.ownedMask |= 1;
                    if (checkpoint.slow > 0) checkpoint.ownedMask |= 2;
                    if (checkpoint.reflex > 0) checkpoint.ownedMask |= 4;
                    if (checkpoint.stop > 0) checkpoint.ownedMask |= 8;
                    checkpoint.patchSlot0 = ValidatePatchSlot(checkpoint.patchSlot0);
                    checkpoint.patchSlot1 = ValidatePatchSlot(checkpoint.patchSlot1);
                    if (checkpoint.patchSlot1 == checkpoint.patchSlot0) checkpoint.patchSlot1 = -1;
                    // Empty stock is allowed only for a previously owned patch; malformed
                    // equipment fields must never create free ownership during load.
                    if (checkpoint.patchSlot0 >= 0 && (checkpoint.ownedMask & (1 << checkpoint.patchSlot0)) == 0) checkpoint.patchSlot0 = -1;
                    if (checkpoint.patchSlot1 >= 0 && (checkpoint.ownedMask & (1 << checkpoint.patchSlot1)) == 0) checkpoint.patchSlot1 = -1;
                }
            }
            save.version = 2;
            save.volume = float.IsNaN(save.volume) || float.IsInfinity(save.volume) ? 1 : Mathf.Clamp01(save.volume);
            if (save.ranks == null) save.ranks = new List<GameDataManager.RankData>();
            for (int index = 0; index < save.ranks.Count; index++)
            {
                GameDataManager.RankData rank = save.ranks[index];
                rank.stageScore = Mathf.Max(0, rank.stageScore);
                rank.playerName = string.IsNullOrWhiteSpace(rank.playerName) ? "Player" : rank.playerName.Trim();
                if (rank.playerName.Length > 128) rank.playerName = rank.playerName.Substring(0, 128);
                save.ranks[index] = rank;
            }
            save.ranks.Sort((left, right) => right.stageScore.CompareTo(left.stageScore));
            if (save.ranks.Count > 10) save.ranks.RemoveRange(10, save.ranks.Count - 10);
            return validCheckpoint;
        }

        static int ValidatePatchSlot(int kind) => kind >= 0 && kind < 4 ? kind : -1;
    }
}
