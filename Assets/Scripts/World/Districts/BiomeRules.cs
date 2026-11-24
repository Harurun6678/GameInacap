using System;

namespace NeonSpite.Generation
{
    public enum DistrictStyle
    {
        Industrial = 0, Foundry = 1, Rooftops = 2, Substation = 3
    }

    // Cada zona define su ritmo y su paleta; las salas siguen respetando las mismas puertas.
    public sealed class BiomeRules
    {
        public readonly DistrictStyle Style;
        public readonly string Name, Description;
        public readonly uint Accent, Wall, Mist;
        public readonly int EncounterBonus;
        private BiomeRules(DistrictStyle style, string name, string description, uint accent, uint wall, uint mist, int bonus)
        {
            Style = style;
            Name = name;
            Description = description;
            Accent = accent;
            Wall = wall;
            Mist = mist;
            EncounterBonus = bonus;
        }
        private static readonly BiomeRules[] all = {
            new BiomeRules(DistrictStyle.Industrial,"Distrito industrial","Galerías abiertas y un inicio gradual.",0xE4B566,0x101B2C,0x7185A2,0),
            new BiomeRules(DistrictStyle.Foundry,"Fundición","Encuentros más densos entre hornos y pasarelas.",0xF39362,0x21151F,0xB56F73,1),
            new BiomeRules(DistrictStyle.Rooftops,"Azoteas","Más ascensos, siluetas de ciudad y luz fría.",0x65D9DA,0x101F30,0x668BB8,0),
            new BiomeRules(DistrictStyle.Substation,"Subestación","El tramo final del recorrido.",0xC49AFF,0x1A142B,0x9A79B5,1)
        };
        public static BiomeRules For(DistrictStyle style)
        {
            int index = (int)style;
            if (index < 0 || index >= all.Length)
                throw new ArgumentOutOfRangeException("style");
            return all[index];
        }
    }

    public static class RunRoute
    {
        public static DistrictStyle[] Choices(int nextStage, int total)
        {
            if (nextStage < 1 || nextStage > total || total < 1 || total > 5)
                throw new ArgumentOutOfRangeException("nextStage");
            if (nextStage == 1)
                return new[] { DistrictStyle.Industrial };
            if (nextStage == total)
                return new[] { DistrictStyle.Substation };
            return nextStage % 2 == 0 ? new[] { DistrictStyle.Foundry, DistrictStyle.Rooftops } : new[] { DistrictStyle.Rooftops, DistrictStyle.Foundry };
        }
    }
}
