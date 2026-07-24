using UnityEngine;

public class UnitStats :MonoBehaviour
{
    public int maxHealth;
    public int currentHealth;
    public int attackPower;
    public int defense;
    public float attackSpeed;
    public float movementSpeed;

    public ScriptableObject[] weaponData = new ScriptableObject[0];

    [SerializeField] private Weapon equippedWeapon;

    public UnitStats(int maxHealth, int attackPower, int defense, float attackSpeed, float movementSpeed)
    {
        this.maxHealth = maxHealth;
        this.currentHealth = maxHealth; // Initialize current health to max health
        this.attackPower = attackPower;
        this.defense = defense;
        this.attackSpeed = attackSpeed;
        this.movementSpeed = movementSpeed;
    }

    public void TakeDamage(int damage)
    {
        int effectiveDamage = Mathf.Max(damage - defense, 0);
        currentHealth -= effectiveDamage;
        currentHealth = Mathf.Max(currentHealth, 0); // Ensure health doesn't go below 0
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth); // Ensure health doesn't exceed max health
    }
}
