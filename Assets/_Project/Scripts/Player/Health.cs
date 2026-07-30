using UnityEngine;

namespace HunterVsHider.Player
{
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        public int maxHealth = 100;
        public int currentHealth;

        private void Start()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            currentHealth -= amount;

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            Destroy(gameObject);
        }
    }
}
