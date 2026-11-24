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

    // Las Twin Cutters (tipo Dagger) golpean rápido pero cada corte hace el 45% del daño base.
    // Se aplica al golpear, así las mejoras de daño también se reducen y la partida guarda el valor base.
    public const float CutterDamageScale = .45f;
    // Precio de su velocidad: con ellas equipadas, los golpes enemigos hacen un 35% más.
    public const float CutterDamageTakenScale = 1.35f;

    public float DamageScale => type == WeaponType.Dagger ? CutterDamageScale : 1f;
    public float DamageTakenScale => type == WeaponType.Dagger ? CutterDamageTakenScale : 1f;

    // Daño real de un golpe.
    public int HitDamage => Mathf.Max(1, Mathf.RoundToInt(damage * DamageScale));
}
