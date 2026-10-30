using UnityEngine;
using UnityEngine.Events;
using Taeyang;

namespace Integration
{
    // Attach to the team's eventual boss root. This adapter supplies no model or attack pattern.
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

        protected virtual void Awake() => currentHealth = MaxHealth;
        void OnTriggerEnter2D(Collider2D other) => Contact(other);
        void OnTriggerStay2D(Collider2D other) => Contact(other);
        void Contact(Collider2D other)
        {
            var game = IntegratedGame.Active;
            if (IsDefeated || game == null || !game.Running || Time.time < nextRam) return;
            var player = other.GetComponentInParent<IntegratedCombat>();
            if (player == null) return;
            nextRam = Time.time + 1;
            TakeRamDamage(player.RamDamage);
            player.Hit(GetComponentInChildren<Collider2D>(), 1);
        }

        public void TakeRamDamage(int amount)
        {
            if (IsDefeated || amount <= 0) return;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            if (currentHealth > 0) return;
            IsDefeated = true;
            defeated.Invoke();
            if (director != null) director.NotifyBossDefeated();
            if (deactivateOnDefeat) gameObject.SetActive(false);
        }
    }
}
