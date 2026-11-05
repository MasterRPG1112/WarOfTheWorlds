#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Integration.Editor
{
    // Invoked only by the user's build launcher. Never rebuilds or saves team scenes.
    public static class WindowsPlayerBuild
    {
        public const string RelativeOutput = "Builds/Windows/WarOfTheWorlds.exe";
        [Serializable]
        class Receipt
        {
            public string request;
            public string output;
            public string completedUtc;
            public string result;
            public bool development;
        }
        public static void BuildExistingScenes() => Build(false, false);
        public static void BuildTestingScenes() => Build(true, false);
        public static void BuildBothVariants() => Build(false, true);
        static void Build(bool development, bool both)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the user-operated Windows build launcher in a separate session.");
            string root = Directory.GetParent(Application.dataPath).FullName;
            int exit = 1;
            try
            {
                BuildVariant(root, development);
                if (both) BuildVariant(root, true);
                exit = 0;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { EditorApplication.Exit(exit); }
        }
        static void BuildVariant(string root, bool development)
        {
                string output = Path.Combine(root, development ? "Builds/Windows-Test/WarOfTheWorlds.exe" : RelativeOutput);
                string receipt = Path.Combine(Path.GetDirectoryName(output), "build-success.json");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                if (File.Exists(receipt)) File.Delete(receipt);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { IntegrationBuilder.MenuPath, IntegrationBuilder.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded || !File.Exists(output))
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                var args = Environment.GetCommandLineArgs();
                int requestAt = Array.IndexOf(args, "-campaignBuildRequest");
                var saved = new Receipt
                {
                    request = requestAt >= 0 && requestAt + 1 < args.Length ? args[requestAt + 1] : "",
                    output = output,
                    completedUtc = DateTime.UtcNow.ToString("o"),
                    result = "Succeeded", development = development
                };
                File.WriteAllText(receipt, JsonUtility.ToJson(saved, true));
                Debug.Log("CAMPAIGN_WINDOWS_BUILD_SUCCEEDED " + output);
        }
    }
}
#endif
