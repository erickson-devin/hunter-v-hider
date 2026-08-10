using UnityEngine;

public class TargetDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"[{gameObject.name}] Took {damage} damage! Remaining Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Debug.Log($"[{gameObject.name}] Destroyed!");
            Destroy(gameObject);
        }
    }
}
