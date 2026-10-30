
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    public GameObject itemPrefab;

    public bool integratedArena;
    public float spawnInterval = 5f; // 5초 간격
    public float spawnRangeX = 8f;
    public float spawnY = 6f;
    public float destroyTime = 10f; // 10초 후 삭제

    void Start()
    {
        InvokeRepeating(nameof(SpawnItem), 1f, spawnInterval);
    }

    void SpawnItem()
    {
        float randomX = Random.Range(-spawnRangeX, spawnRangeX);
        float y = spawnY;
        if (integratedArena)
        {
            Rect view = Integration.BattleArena.ViewBounds();
            randomX = Random.Range(view.xMin + 1, view.xMax - 1);
            y = view.yMax + 1;
        }

        Vector3 spawnPosition = new Vector3(
            randomX,
            y,
            0f
        );

        GameObject clone = Instantiate(
            itemPrefab,
            spawnPosition,
            Quaternion.identity
        );

        // 생성된 클론을 10초 후 삭제
        Destroy(clone, destroyTime);
    }
}
