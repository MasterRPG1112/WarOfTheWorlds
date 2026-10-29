using Jain;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;

namespace Sanghyuk
{
    public class Player : MonoBehaviour
    {
        int bomb = 1;
        public GameObject Origin;
        public GameObject Cam;
        public GameObject BHole;
        public GameObject GravityUnit;
        public GameObject ParryingUnit;
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
            if (bomb  == 1)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    Instantiate(Origin, Cam.transform.position + new Vector3(0f, -10f, +1f), Cam.transform.rotation);
                    this.bomb -= 1;
                }
            }
        }
        void BlackH()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                GameObject BH = Instantiate(BHole, transform.position, transform.rotation);
            }
        }
        void Grav()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                GameObject GV = Instantiate(GravityUnit, transform.position, transform.rotation);
            }
        }
        void Par()
        {
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                GameObject PR = Instantiate(ParryingUnit, transform.position, transform.rotation);
            }
        }
        void Update()
        {
            moveX = Input.GetAxis("Horizontal");
            moveY = Input.GetAxis("Vertical");
            Move();
            Bomb();
            BlackH();
            Grav();
            Par();
        }
    }
}