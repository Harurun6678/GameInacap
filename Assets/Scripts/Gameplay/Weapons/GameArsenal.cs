using UnityEngine;

public sealed class GameArsenal : MonoBehaviour
{
    [SerializeField] private WeaponData rulerTemplate;
    [SerializeField] private WeaponData daggerTemplate;
    public WeaponData Ruler { get; private set; }
    public WeaponData Dagger { get; private set; }

    internal void Initialize()
    {
        Ruler = Instantiate(rulerTemplate);
        Dagger = Instantiate(daggerTemplate);
    }

    internal void ResetDamage()
    {
        Ruler.damage = rulerTemplate.damage;
        Dagger.damage = daggerTemplate.damage;
    }

    internal int IdOf(WeaponData weapon) => weapon == Ruler ? 1 : weapon == Dagger ? 2 : 0;
    internal WeaponData FromId(int id) => id == 1 ? Ruler : id == 2 ? Dagger : null;

    private void OnDestroy()
    {
        if (Ruler != null) Destroy(Ruler);
        if (Dagger != null) Destroy(Dagger);
    }
}
