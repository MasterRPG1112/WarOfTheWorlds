using UnityEngine;
using UnityEngine.Events;
using Taeyang;

namespace Integration
{
    // Shared ram-only damage contract for authored prefabs and campaign bosses.
    public class IntegratedBossTarget : MonoBehaviour
    {
        [Min(1)] public int maxHealth = 300;
        public StageDirector director;
        public UnityEvent defeated = new UnityEvent();
        public bool deactivateOnDefeat = true;
        protected int currentHealth;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public bool IsDefeated { get; private set; }
        float nextRam;
        bool defeatCompleted;

        protected virtual void Awake() => currentHealth = MaxHealth;
        protected virtual void OnTriggerEnter2D(Collider2D other) => Contact(other);
        protected virtual void OnTriggerStay2D(Collider2D other) => Contact(other);
        void Contact(Collider2D other)
        {
            var game = IntegratedGame.Active;
            if (IsDefeated || game == null || !game.Running || Time.time < nextRam) return;
            var player = other.GetComponentInParent<IntegratedCombat>();
            if (player == null) return;
            nextRam = Time.time + 1;
            TakeRamDamage(player.RamDamage);
            if (!IsDefeated && ContactDamageEnabled) player.Hit(GetComponentInChildren<Collider2D>(), 1);
            // A successful ram separates the hulls, including safe recovery windows.
            Vector2 away = (Vector2)(player.transform.position - transform.position);
            if (away.sqrMagnitude < .01f) away = Vector2.down;
            if (player.movement != null) player.movement.AddKnockback(away.normalized * 6);
        }

        protected virtual bool ContactDamageEnabled => true;

        public void TakeRamDamage(int amount)
        {
            if (IsDefeated || amount <= 0) return;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            OnRamHit(amount);
            if (currentHealth > 0) return;
            IsDefeated = true;
            HandleDefeat();
        }

        protected virtual void OnRamHit(int amount) { }
        protected virtual void HandleDefeat() => CompleteDefeat();
        protected void CompleteDefeat()
        {
            if (defeatCompleted) return;
            defeatCompleted = true;
            defeated.Invoke();
            if (director != null) director.NotifyBossDefeated();
            if (deactivateOnDefeat) gameObject.SetActive(false);
        }
    }
}
