    using UnityEngine;

    public class Player : MonoBehaviour
    {
        Rigidbody2D thisRigi;
        public float speed = 5;
        float moveX;
        float moveY;
        private void Start()
        {
            thisRigi = GetComponent<Rigidbody2D>();
        }
        private void Move()
        {
            Vector3 move = new Vector3(moveX, moveY, 0.0f);
            thisRigi.linearVelocity = move * speed;

            Vector3 poslnWorld = Camera.main.WorldToScreenPoint(this.transform.position);

            float posX = Mathf.Clamp(poslnWorld.x, 0, Screen.width);
            float posY = Mathf.Clamp(poslnWorld.y, 0, Screen.height);

            Vector3 poslnScreen = Camera.main.ScreenToWorldPoint(new Vector3(posX, posY, 0));

            thisRigi.position = new Vector3(poslnScreen.x, poslnScreen.y, 0);
        }
        void Update()
        {
            moveX = Input.GetAxis("Horizontal");
            moveY = Input.GetAxis("Vertical");
            Move();
        }
    }