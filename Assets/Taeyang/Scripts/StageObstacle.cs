using UnityEngine;
namespace Taeyang
{
    public class StageObstacle : MonoBehaviour
    {
        private Vector2 velocity;
        private float age, originX, spin;
        private int stage;
        private Rigidbody2D body;
        public void Initialize(int value)
        {
            stage=value; originX=transform.position.x;
            var visual=gameObject.AddComponent<SpriteRenderer>();
            visual.sprite=StageBackdrop.MakeSprite(value == 2 ? 4 : value == 3 ? 6 : 9);
            visual.color=value == 1 ? new Color(.55f,.35f,.19f) : value == 2 ? new Color(.45f,.4f,.6f) : new Color(.4f,.85f,1);
            visual.sortingOrder=2;
            transform.localScale=Vector3.one*(value == 3 ? .9f : 1.2f);
            var collider=gameObject.AddComponent<CircleCollider2D>(); collider.radius=.45f; collider.isTrigger=true;
            body=gameObject.AddComponent<Rigidbody2D>(); body.bodyType=RigidbodyType2D.Kinematic; body.useFullKinematicContacts=true;
            velocity=new Vector2(value == 2 ? Random.Range(-.5f,.5f) : 0,-(1.2f+value*.3f)); spin=Random.Range(-50,50);
        }
        private void FixedUpdate()
        {
            if (Integration.IntegratedGame.Active != null && Integration.IntegratedGame.Active.EnemiesFrozen) return;
            age+=Time.fixedDeltaTime;
            float x=stage == 3 ? originX+Mathf.Sin(age*1.8f)*1.2f : body.position.x+velocity.x*Time.fixedDeltaTime;
            body.MovePosition(new Vector2(x,body.position.y+velocity.y*Time.fixedDeltaTime));
            transform.Rotate(0,0,spin*Time.fixedDeltaTime);
            if(age>12 || body.position.y < -6.5f) Destroy(gameObject);
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            var combat = other.GetComponentInParent<Integration.IntegratedCombat>();
            if (combat != null) { combat.Hit(other, 1); Destroy(gameObject); return; }
            var player=other.GetComponent<DemoPlayer>();
            if(player != null) { player.TakeDamage(1); SimpleImpact.Spawn(transform.position,Color.white,.6f); Destroy(gameObject); }
            else if(other.CompareTag("Player")) { other.SendMessage("TakeDamage",1,SendMessageOptions.DontRequireReceiver); Destroy(gameObject); }
            var bullet=other.GetComponent<EnemyProjectile>();
            if(bullet != null) Destroy(bullet.gameObject);
        }
    }
}
