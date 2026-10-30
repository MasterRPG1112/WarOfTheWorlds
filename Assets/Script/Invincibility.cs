using Sanghyuk;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Processors;

public class Invincibility : MonoBehaviour
{
    public PlayerData Data;
    void Start()
    {
        Data = FindAnyObjectByType<PlayerData>();
    }
    private void Update()
    {
        Vector3 currentPos = transform.position;
        currentPos.y -= 1.0f * Time.deltaTime;
        transform.position = currentPos;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        var combat = collision.GetComponentInParent<Integration.IntegratedCombat>();
        if (combat != null) { combat.data.InvinTimerStart(); Destroy(gameObject); return; }
        if (collision.CompareTag("Player"))
        {
            Data.InvinTimerStart();
            Destroy(this.gameObject);
        }
    }
}
