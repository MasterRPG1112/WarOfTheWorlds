using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Taeyang
{
    public class StageOneDirector : MonoBehaviour
    {
        public EnemyShip[] prefabs;
        public int Wave { get; private set; }
        public bool Complete { get; private set; }
        public float timeScaleForTest = 1;
        private readonly List<EnemyShip> alive = new List<EnemyShip>();
        public static readonly int[][] Waves = {
            new[]{0,0,0}, new[]{0,1,0,1}, new[]{3,0,3}, new[]{2,1,1},
            new[]{4,0,4}, new[]{3,1,4,3}, new[]{2,4,1,3,4}
        };
        private IEnumerator Start()
        {
            if (prefabs == null || prefabs.Length != 5)
            { Debug.LogError("Stage 1 requires five enemy prefabs"); yield break; }
            for (int w = 0; w < Waves.Length; w++)
            {
                Wave = w + 1;
                foreach (int kind in Waves[w])
                {
                    EnemyShip ship = Instantiate(prefabs[kind], new Vector3(Random.Range(-6f,6f),5.8f,0), Quaternion.identity);
                    ship.stageOnePatterns = true;
                    alive.Add(ship);
                    yield return new WaitForSeconds(1.1f / timeScaleForTest);
                }
                do { yield return null; alive.RemoveAll(e => e == null); } while (alive.Count > 0);
                yield return new WaitForSeconds(1.5f / timeScaleForTest);
            }
            Complete = true;
            Debug.Log("TAEYANG_STAGE1_COMPLETE");
        }
        private void OnGUI()
        {
            GUI.Label(new Rect(16,12,900,30), Complete ? "STAGE 1 CLEAR" : "STAGE 1 | WAVE " + Wave + " / 7");
            GUI.Label(new Rect(16,40,900,30), "WASD / ARROWS: move | SPACE: clear missiles | Lure missiles into enemies");
        }
    }
}
