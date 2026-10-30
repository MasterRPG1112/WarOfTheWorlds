using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
namespace Integration
{
    public class DesignRuntimeSmoke : MonoBehaviour
    {
        string folder;int errors,captures;StringBuilder log=new StringBuilder();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--design-smoke");if(i<0||i+1>=args.Length)return;
            var probe=new GameObject("Design validation only").AddComponent<DesignRuntimeSmoke>();DontDestroyOnLoad(probe.gameObject);
            probe.folder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(probe.folder);Application.runInBackground=true;Application.targetFrameRate=60;
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string m,string s,LogType t){if(t==LogType.Exception||t==LogType.Error||t==LogType.Assert){errors++;log.AppendLine(m).AppendLine(s);}}
        IEnumerator Start()
        {
            var steps=Check();bool failed=false;
            while(true)
            {
                object current;
                try{if(!steps.MoveNext())break;current=steps.Current;}
                catch(Exception e){failed=true;log.AppendLine("FAIL "+e);break;}
                yield return current;
            }
            log.AppendLine("Runtime errors="+errors+"; captures="+captures);
            log.AppendLine("Human input/balance and real teammate boss not tested. Invulnerability used only for visual capture.");
            log.AppendLine(failed||errors>0?"FUNCTIONAL FAIL":"FUNCTIONAL PASS; process exit evaluated separately");
            File.WriteAllText(Path.Combine(folder,"runtime-design-validation.txt"),log.ToString());DesignValidationMode.Restore();yield return null;Application.Quit(failed||errors>0?1:0);
        }
        void A(bool yes,string name){if(!yes)throw new Exception(name);log.AppendLine("PASS "+name);}
        void Capture(string name,int width,int height){if(!FrameCapture.TryCapture(Path.Combine(folder,name),out string error,width,height))throw new Exception(error);captures++;}
        IEnumerator Check()
        {
            yield return null;yield return new WaitForSecondsRealtime(.5f);
            var menu=FindAnyObjectByType<IntegratedMenu>();A(menu!=null,"Existing menu entry remains available");menu.NewGame();yield return null;yield return null;
            var g=IntegratedGame.Active;var p=g.player;A(g.Running&&g.selectionPanel==null,"New game begins immediately without selection");
            A(p.Health==8&&p.ownedMask==0,"Shared HP8 and unowned initial skills");
            g.shop.PullBtn();A(g.shop.coin==0&&g.shop.pull==0,"Insufficient currency blocks shop purchase");
            Capture("01-unified-hud-1920.png",1920,1080);Capture("02-unified-hud-1280.png",1280,720);
            p.data.PlayerHp=4;yield return null;Capture("03-damaged-hp-1280.png",1280,720);p.Heal(99);
            p.GrantPatch(0,2);p.GrantPatch(2,2);yield return null;
            A(p.TryUsePatch(0)&&p.PatchCooldown(0)>0,"Owned equipped patch consumes stock and starts cooldown");
            Capture("04-patches-cooldown.png",1280,720);
            float end=Time.realtimeSinceStartup+8;
            while(Time.realtimeSinceStartup<end){p.data.Invin=true;p.data.PlayerHp=8;yield return null;}
            A(FindObjectsByType<PlayerShot>(FindObjectsSortMode.None).Length==0&&p.shotsFired==0,"No player basic shots during real-time combat");
            Capture("05-missile-lure-combat.png",1920,1080);
            g.TogglePause();FindAnyObjectByType<DesignHud>().OpenPatches();yield return null;Capture("06-two-slot-equipment.png",1280,720);
            FindAnyObjectByType<DesignHud>().ClosePatches();g.BackToMenu();yield return null;yield return null;
            menu=FindAnyObjectByType<IntegratedMenu>();A(menu!=null&&menu.continueButton.interactable,"Menu return and saved checkpoint continuation");
            menu.ContinueGame();yield return null;yield return null;A(IntegratedGame.Active.Running,"Continue resumes directly without skill popup");
        }
    }
}
