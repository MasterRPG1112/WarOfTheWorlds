using Sanghyuk;
using UnityEngine;

public class Shield : MonoBehaviour
{
    public PlayerData Data;
    void Start()
    {
        Data = FindAnyObjectByType<PlayerData>();
    }
    void Update()
    {
        Vector3 currentPos = transform.position;
        currentPos.y -= 1.0f * Time.deltaTime;
        transform.position = currentPos;
        if (Data.HaveShield == 1 && Integration.IntegratedGame.Active == null)
        {
            Debug.Log("1");
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Data.HaveShield = 1;
            Destroy(this.gameObject);
        }
    }
}
