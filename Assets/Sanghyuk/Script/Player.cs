using UnityEngine;
using UnityEngine.SceneManagement;
namespace sanghyuk
{
    public class Player : MonoBehaviour
    {
        public PlayerData Data;
        Rigidbody2D thisRigi;
        public float speed = 5;
        float moveX;
        float moveY;
        private void Start()
        {
            thisRigi = GetComponent<Rigidbody2D>();
        }
        void Move()
        {
            Vector3 move = new Vector3(moveX, moveY, 0.0f);
            thisRigi.linearVelocity = move * speed;

            Vector3 poslnWorld = Camera.main.WorldToScreenPoint(this.transform.position);

            float posX = Mathf.Clamp(poslnWorld.x, 0, Screen.width);
            float posY = Mathf.Clamp(poslnWorld.y, 0, Screen.height);

            Vector3 poslnScreen = Camera.main.ScreenToWorldPoint(new Vector3(posX, posY, 0));

            thisRigi.position = new Vector3(poslnScreen.x, poslnScreen.y, 0);
        }

        void Bomb()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");

                foreach (GameObject bullet in bullets)
                {
                    Destroy(bullet);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Bullet"))
            {
                Data = GameObject.Find("PlayerDataManager").GetComponent<PlayerData>();
                Destroy(collision.gameObject);
                Data.PlayerHp -= 1;
            }
        }
        void Update()
        {
            moveX = Input.GetAxis("Horizontal");
            moveY = Input.GetAxis("Vertical");
            Move();
            Bomb();
        }
    }
}