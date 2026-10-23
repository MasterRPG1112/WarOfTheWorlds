using UnityEngine;

namespace Taeyang
{
    public class EnemyDemoHUD : MonoBehaviour
    {
        public EnemyWaveSpawner spawner;
        public DemoPlayer player;
        private int score;
        private GUIStyle style;

        private void OnEnable() => EnemyShip.DestroyedByPlayer += OnEnemyDestroyed;
        private void OnDisable() => EnemyShip.DestroyedByPlayer -= OnEnemyDestroyed;
        private void OnEnemyDestroyed(EnemyShip ship, int points) => score += points;

        private void Update()
        {
            if (spawner == null) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) spawner.SetStage(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) spawner.SetStage(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) spawner.SetStage(3);
        }
        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.fontSize = 19;
                style.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(16, 12, 900, 34), "WAR OF THE WORLDS | ENEMY TEST", style);
            GUI.Label(new Rect(16, 44, 900, 32), "HP: " + (player != null ? player.health : 0) + "     SCORE: " + score +
                "     STAGE: " + (spawner != null ? spawner.stage : 1), style);
            GUI.Label(new Rect(16, 76, 1000, 32), "WASD / ARROWS: move    SPACE: clear missiles    1/2/3: stage", style);
            GUI.Label(new Rect(16, 108, 1000, 32), "Lure enemy missiles into other ships to destroy them!", style);
        }
    }
}
