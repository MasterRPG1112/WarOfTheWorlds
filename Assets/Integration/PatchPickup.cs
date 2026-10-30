using UnityEngine;
namespace Integration
{
    public class PatchPickup : MonoBehaviour
    {
        [Range(0,3)] public int kind;
        bool collected;
        void Update() { transform.position += Vector3.down * Time.deltaTime; }
        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<IntegratedCombat>();
            if (collected || player == null) return;
            collected = true; player.GrantPatch(kind,1); Destroy(gameObject);
        }
        void OnGUI()
        {
            if (Camera.main == null) return;
            Vector3 p=Camera.main.WorldToScreenPoint(transform.position);
            GUI.Label(new Rect(p.x-42,Screen.height-p.y-38,100,24),new[]{"BLACK HOLE","SLOW","MIRROR","TIME STOP"}[kind]);
        }
    }
}
