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
        public Vector2 FacingDirection { get; private set; } = Vector2.up;
        Vector2 integratedInput;
        Vector2 desiredFacing = Vector2.up, driveVelocity, knockbackVelocity;
        Integration.IntegratedCombat integratedCombat;
        const float BoundaryMargin = 1.6f;
        private void Awake()
        {
            thisRigi = GetComponent<Rigidbody2D>();
            integratedCombat = GetComponent<Integration.IntegratedCombat>();
        }
        // Keyboard movement and verification share the same eight-direction controller.
        public void ApplyMovementInput(Vector2 input)
        {
            if (thisRigi == null) thisRigi = GetComponent<Rigidbody2D>();
            if (thisRigi == null) return;
            if (Time.timeScale == 0) { integratedInput = Vector2.zero; thisRigi.linearVelocity = Vector2.zero; return; }
            integratedInput = new Vector2(Mathf.Abs(input.x) > .01f ? Mathf.Sign(input.x) : 0,
                Mathf.Abs(input.y) > .01f ? Mathf.Sign(input.y) : 0).normalized;
            if (integratedInput.sqrMagnitude > .01f)
            {
                desiredFacing = integratedInput;
            }
            var bounds = Integration.BattleArena.PlayerBounds(Camera.main, BoundaryMargin);
            thisRigi.position = new Vector2(Mathf.Clamp(thisRigi.position.x,bounds.xMin,bounds.xMax),
                Mathf.Clamp(thisRigi.position.y,bounds.yMin,bounds.yMax));
        }
        public void AddKnockback(Vector2 impulse) { knockbackVelocity = Vector2.ClampMagnitude(knockbackVelocity + impulse,8); }
        void FixedUpdate()
        {
            if (integratedCombat == null || thisRigi == null) return;
            var game = Integration.IntegratedGame.Active;
            if (Time.timeScale == 0 || game == null || !game.Running) { thisRigi.linearVelocity = Vector2.zero; return; }
            var bounds = Integration.BattleArena.PlayerBounds(Camera.main, BoundaryMargin);
            driveVelocity = Vector2.MoveTowards(driveVelocity,integratedInput * speed,
                (integratedInput.sqrMagnitude > 0 ? 35 : 45) * Time.fixedDeltaTime);
            float desiredAngle = Mathf.Atan2(desiredFacing.y,desiredFacing.x)*Mathf.Rad2Deg-90;
            thisRigi.rotation = Mathf.MoveTowardsAngle(thisRigi.rotation,desiredAngle,720*Time.fixedDeltaTime);
            FacingDirection = Quaternion.Euler(0,0,thisRigi.rotation) * Vector2.up;
            Vector2 destination = thisRigi.position + (driveVelocity + knockbackVelocity) * Time.fixedDeltaTime;
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity,Vector2.zero,22*Time.fixedDeltaTime);
            destination.x = Mathf.Clamp(destination.x,bounds.xMin,bounds.xMax);
            destination.y = Mathf.Clamp(destination.y,bounds.yMin,bounds.yMax);
            thisRigi.linearVelocity = (destination - thisRigi.position) / Time.fixedDeltaTime;
        }
        void Move()
        {
            Vector3 move = new Vector3(moveX, moveY, 0.0f);
            thisRigi.linearVelocity = move * speed;

            Vector3 poslnWorld = Camera.main.WorldToScreenPoint(this.transform.position);

            float posX = Mathf.Clamp(poslnWorld.x, 0, Screen.width);
            float posY = Mathf.Clamp(poslnWorld.y, 0, Screen.height);

            Vector3 poslnScreen = Camera.main.ScreenToWorldPoint(new Vector3(posX, posY, poslnWorld.z));

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
            if (Time.timeScale == 0) { integratedInput = Vector2.zero; if (thisRigi != null) thisRigi.linearVelocity = Vector2.zero; return; }
            if (integratedCombat != null)
            {
                var game = Integration.IntegratedGame.Active;
                if (game != null && game.Running) ApplyMovementInput(new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")));
                else { integratedInput = Vector2.zero; thisRigi.linearVelocity = Vector2.zero; }
                return;
            }
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
