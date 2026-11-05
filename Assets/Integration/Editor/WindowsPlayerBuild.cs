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
        }
        public static void BuildExistingScenes()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the user-operated Windows build launcher in a separate session.");
            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, RelativeOutput);
            string receipt = Path.Combine(root, "Builds/Windows/build-success.json");
            int exit = 1;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                if (File.Exists(receipt)) File.Delete(receipt);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { IntegrationBuilder.MenuPath, IntegrationBuilder.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
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
                    result = "Succeeded"
                };
                File.WriteAllText(receipt, JsonUtility.ToJson(saved, true));
                Debug.Log("CAMPAIGN_WINDOWS_BUILD_SUCCEEDED " + output);
                exit = 0;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { EditorApplication.Exit(exit); }
        }
    }
}
#endif
