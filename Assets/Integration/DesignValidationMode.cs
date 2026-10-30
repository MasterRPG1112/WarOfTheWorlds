using System;
using UnityEngine;
namespace Integration
{
    public static class DesignValidationMode
    {
        static bool isolated;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            foreach(var arg in Environment.GetCommandLineArgs())
                if(arg=="--design-smoke"||arg=="Integration.Editor.DesignPlayCheck.Run") { Prepare();break; }
        }
        public static void Prepare(){if(isolated)return;IntegrationSaveStore.ResetForTests();isolated=true;}
        public static void Restore(){if(!isolated)return;IntegrationSaveStore.RestoreAfterTests();isolated=false;}
    }
}
