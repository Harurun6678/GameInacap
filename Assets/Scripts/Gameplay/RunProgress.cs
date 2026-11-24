using NeonSpite.Generation;
using NeonSpite.Persistence;

public enum RoomKind { MainRoom = 0, Rest = 1, Procedural = 2 }

internal sealed class RunProgress
{
    public int Seed, DistrictNumber, DistrictCount, Columns, Branches;
    public int Fragments, TutorialStep, PendingDistrict, GeneratorVersion;
    public DistrictStyle Style, PendingStyle;
    public bool DoorUnlocked, UpgradePurchased;
    public float Elapsed;
    private int[] route;

    public void Start(int seed, DistrictStyle style, RoomKind firstRoom)
    {
        Seed = seed;
        Style = style;
        GeneratorVersion = DistrictPlan.GeneratorVersion;
        DistrictNumber = firstRoom == RoomKind.Procedural ? 1 : 0;
        PendingDistrict = 0;
        PendingStyle = DistrictStyle.Industrial;
        TutorialStep = 0;
        Fragments = firstRoom == RoomKind.Rest ? 1 : 0;
        UpgradePurchased = false;
        DoorUnlocked = firstRoom == RoomKind.Rest;
        Elapsed = 0;
        ResetRoute();
    }

    public void EnterDistrict(int number, DistrictStyle style)
    {
        DistrictNumber = number;
        Style = style;
        GeneratorVersion = DistrictPlan.GeneratorVersion;
        route[number - 1] = (int)style;
        PendingDistrict = 0;
        PendingStyle = DistrictStyle.Industrial;
    }

    private void ResetRoute()
    {
        route = new int[DistrictCount];
        for (int i = 0; i < route.Length; i++) route[i] = -1;
        if (DistrictNumber > 0) route[DistrictNumber - 1] = (int)Style;
    }

    public string StageName(int index, DistrictStyle first)
    {
        if (route != null && index >= 0 && index < route.Length && route[index] >= 0)
            return BiomeRules.For((DistrictStyle)route[index]).Name;
        if (PendingDistrict == index + 1) return BiomeRules.For(PendingStyle).Name + " · próxima";
        if (index == 0) return BiomeRules.For(first).Name;
        return index + 1 == DistrictCount ? "Subestación" : "Ruta por elegir";
    }

    public void Capture(RunSaveData data)
    {
        data.seed = Seed;
        data.district = DistrictNumber;
        data.districts = DistrictCount;
        data.columns = Columns;
        data.branches = Branches;
        data.generator = GeneratorVersion;
        data.biome = (int)Style;
        data.nextDistrict = PendingDistrict;
        data.nextBiome = (int)PendingStyle;
        data.route = (int[])route.Clone();
        data.fragments = Fragments;
        data.tutorialStep = TutorialStep;
        data.doorUnlocked = DoorUnlocked;
        data.upgraded = UpgradePurchased;
        data.elapsed = Elapsed;
    }

    public void Restore(RunSaveData data)
    {
        Seed = data.seed;
        DistrictNumber = data.district;
        DistrictCount = data.districts;
        Columns = data.columns;
        Branches = data.branches;
        GeneratorVersion = data.generator;
        Style = (DistrictStyle)data.biome;
        PendingDistrict = data.nextDistrict;
        PendingStyle = (DistrictStyle)data.nextBiome;
        if (data.route == null) ResetRoute();
        else route = (int[])data.route.Clone();
        Fragments = data.fragments;
        TutorialStep = data.tutorialStep;
        DoorUnlocked = data.doorUnlocked;
        UpgradePurchased = data.upgraded;
        Elapsed = data.elapsed;
    }
}
