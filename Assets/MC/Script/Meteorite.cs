using Jain;
using Sanghyuk;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Meteorite : MonoBehaviour
{
    public PlayerData Data;
    void Start()
    {
        Data = FindAnyObjectByType<PlayerData>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 currentPos = transform.position;
        currentPos.y -= 3.0f * Time.deltaTime;
        transform.position = currentPos;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Data.PlayerHp -= 1;
            Destroy(this.gameObject);
        }
    }
}
