using UnityEngine;

namespace Taeyang
{
    public class EnemyWaveSpawner : MonoBehaviour
    {
        public EnemyShip[] enemyPrefabs;
        [Range(1, 3)] public int stage = 1;
        public float[] spawnIntervals = { 3f, 4f, 5f };
        public int maxAlive = 7;
        public float spawnY = 6.1f;
        public float minX = -7f;
        public float maxX = 7f;
        private float timer = 1.5f;

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Mathf.Max(0.5f, spawnIntervals[Mathf.Clamp(stage - 1, 0, spawnIntervals.Length - 1)]);
            if (FindObjectsByType<EnemyShip>(FindObjectsSortMode.None).Length >= maxAlive) return;
            if (enemyPrefabs == null || enemyPrefabs.Length < 5) return;

            int index = ChooseEnemyIndex();
            float x = Random.Range(minX, maxX);
            Instantiate(enemyPrefabs[index], new Vector3(x, spawnY, 0), Quaternion.identity);
        }

        public void SetStage(int number)
        {
            stage = Mathf.Clamp(number, 1, 3);
            timer = 1.0f;
        }

        private int ChooseEnemyIndex()
        {
            // Index order is Scout / Fighter / Heavy / Interceptor / Bomber.
            float sample = Random.value;
            if (stage == 1) return sample < 0.55f ? 0 : sample < 0.85f ? 1 : 3;
            if (stage == 2) return sample < 0.3f ? 0 : sample < 0.65f ? 1 : sample < 0.85f ? 2 : 3;
            return sample < 0.15f ? 0 : sample < 0.35f ? 1 : sample < 0.60f ? 2 : sample < 0.77f ? 3 : 4;
        }
    }
}
