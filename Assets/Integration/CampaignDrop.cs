using UnityEngine;
using Taeyang;
namespace Integration
{
    // Every defeated enemy produces a collectible at its actual death position.
    // Four skill types alternate with recovery, shield, invulnerability and bombs.
    public class CampaignDrop : MonoBehaviour
    {
        public int kind;
        public float lifetime = 20;
        bool collected;
        float age;
        static readonly string[] Names = {"블랙홀", "감속장", "반사", "시간 정지", "HP +2", "방어막", "무적 3초", "폭탄", "강제 유도"};
        public static CampaignDrop Spawn(Vector3 position, int sequence)
        {
            var go = new GameObject("Enemy drop"); go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = StageBackdrop.MakeSprite(6);
            sr.color = Color.Lerp(Color.cyan, Color.yellow, (sequence % 8) / 7f); sr.sortingOrder = 20;
            go.transform.localScale = Vector3.one * .65f;
            var body = go.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true;
            var hit = go.AddComponent<CircleCollider2D>(); hit.isTrigger = true; hit.radius = .8f;
            var drop = go.AddComponent<CampaignDrop>(); drop.kind = (sequence - 1) % 9;
            return drop;
        }
        void Update()
        {
            var game = IntegratedGame.Active;
            if (game == null || !game.Running) return;
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }
            var sr = GetComponent<SpriteRenderer>();
            sr.enabled = age < lifetime - 4 || Mathf.Repeat(age, .4f) < .2f;
        }
        void OnTriggerEnter2D(Collider2D other) => Collect(other.GetComponentInParent<IntegratedCombat>());
        public bool Collect(IntegratedCombat player)
        {
            if (collected || player == null || IntegratedGame.Active == null || !IntegratedGame.Active.Running) return false;
            collected = true;
            if (kind < 4) player.GrantPatch(kind);
            else if (kind == 4) player.Heal(2);
            else if (kind == 5) player.data.HaveShield = Mathf.Min(5, player.data.HaveShield + 1);
            else if (kind == 6) player.data.InvinTimerStart();
            else if (kind == 8) player.GrantPatch(4);
            else player.bombs = Mathf.Min(9, player.bombs + 1);
            Destroy(gameObject); return true;
        }
        void OnGUI()
        {
            if (Camera.main == null) return;
            Vector3 p = Camera.main.WorldToScreenPoint(transform.position);
            if(IntegratedGame.Active!=null&&IntegratedGame.Active.director.stageNumber==3&&Vector2.Distance(p,Camera.main.WorldToScreenPoint(IntegratedGame.Active.player.transform.position))>Mathf.Min(Screen.width,Screen.height)*.22f)return;
            GUI.Label(new Rect(p.x-55,Screen.height-p.y-35,120,28),Names[Mathf.Clamp(kind,0,8)]);
        }
    }
}
