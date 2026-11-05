#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Integration.Editor
{
    [InitializeOnLoad]
    public static class AssignmentPlayCheck
    {
        static AssignmentPlayCheck()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool("Assignment.Check",false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    var probe=new GameObject("Assignment checks").AddComponent<CampaignValidation>();
                    probe.folder=Output;probe.capture=SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null;
                    probe.completed=code=>{SessionState.SetInt("Assignment.Code",code);EditorApplication.isPlaying=false;};
                }
                if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("Assignment.Check",false);EditorApplication.Exit(SessionState.GetInt("Assignment.Code",1));}
            };
        }
        static string Output
        {
            get{var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--assignment-output");return i>=0?args[i+1]:Path.GetFullPath("../assignment-checks");}
        }
        public static void Run(){EditorSceneManager.OpenScene(IntegrationBuilder.ScenePath);SessionState.SetBool("Assignment.Check",true);EditorApplication.isPlaying=true;}
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Output);
            var report=BuildPipeline.BuildPlayer(new[]{IntegrationBuilder.MenuPath,IntegrationBuilder.ScenePath},Path.Combine(Output,"Windows/WarOfTheWorlds.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(Path.Combine(Output,"build-result.txt"),report.summary.result+" errors="+report.summary.totalErrors+" bytes="+report.summary.totalSize);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed");
        }
    }
}
#endif
