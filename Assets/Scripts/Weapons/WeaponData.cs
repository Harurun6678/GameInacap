using UnityEngine;

public enum WeaponType
{
    Ruler,       // Regla Metalica
    Dagger,      // Dagas
    Hammer,      // Martillo
    Spear,       // Lanza
    Greatsword,  // Espadon
    Shortsword   // Espada corta
}

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Kogal/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName = "Regla Metalica";
    public WeaponType type = WeaponType.Ruler;
    public int damage = 25;
    public float attackRange = 1.0f;
    public float attackRate = 3.0f; // Ataques por segundo
    public float knockbackForce = 10f;
    public Sprite weaponSprite;
}
