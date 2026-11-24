using System;
using NeonSpite.Generation;

namespace NeonSpite.Persistence
{
    [Serializable]
    public sealed class RoomSave
    {
        public bool visited, looted, healed, rewarded;
        // Sala inversa: si ya se invirtió, si se rompió su mochila, si se agotó la cuenta y el tiempo restante.
        // Las partidas antiguas cargan con false / 0.
        public bool inverted, inverseLooted, inverseExpired;
        public float inverseTime;
    }
    [Serializable]
    public sealed class EnemySave
    {
        public int room, slot, health;
        public float x, y;
    }
    [Serializable]
    public sealed class RunSaveData
    {
        public int schema = 2, generator = DistrictPlan.GeneratorVersion;
        public string savedUtc;
        public int seed, district, room, columns, branches, districts;
        public int health, maxHealth, fragments, tutorialStep, dummyHealth;
        public bool upgraded, doorUnlocked;
        public int weapon0, weapon1, activeWeapon, rulerDamage, daggerDamage;
        public float x, y, speedBuff, elapsed;
        public string fingerprint;
        public int refuge = -1;
        public int biome, nextDistrict, nextBiome;
        public int[] route;
        public RoomSave[] rooms;
        // Solo los enemigos vivos. Las ausencias son bajas ya confirmadas.
        public EnemySave[] enemies;

        public string Validate()
        {
            DateTime date;
            if (schema < 1 || schema > 2 || generator < 1 || generator > DistrictPlan.GeneratorVersion || (schema == 1 && generator != 1))
                return "Esta partida pertenece a otra versión del juego.";
            if (!DateTime.TryParse(savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out date))
                return "Fecha de guardado inválida.";
            if (room < 0 || room > 2 || columns < 4 || columns > 8 || branches < 1 || branches > 6 || districts < 1 || districts > 5 || district < 0 || district > districts)
                return "Los datos del recorrido no son válidos.";
            if ((generator == 1 && biome != 0) || biome < 0 || biome > 3 || nextBiome < 0 || nextBiome > 3 || nextDistrict < 0 || nextDistrict > districts || (nextDistrict > 0 && (room != 1 || nextDistrict != district + 1)))
                return "La ruta guardada no es válida.";
            if (schema >= 2)
            {
                if (route == null || route.Length != districts)
                    return "Falta la ruta de la partida.";
                foreach (int choice in route)
                    if (choice < -1 || choice > 3)
                        return "La ruta contiene una zona desconocida.";
                if (district > 0 && route[district - 1] != biome)
                    return "La zona actual no coincide con la ruta.";
                if (nextDistrict > 0)
                {
                    bool allowed = false;
                    foreach (DistrictStyle choice in RunRoute.Choices(nextDistrict, districts))
                        if ((int)choice == nextBiome)
                            allowed = true;
                    if (!allowed)
                        return "La siguiente zona no está conectada al recorrido.";
                }
            }
            if (maxHealth < 1 || maxHealth > 1000 || health < 1 || health > maxHealth || fragments < 0 || fragments > 100000 || tutorialStep < 0 || tutorialStep > 5 || dummyHealth < 0 || dummyHealth > 55)
                return "Los datos del personaje no son válidos.";
            if (weapon0 < 1 || weapon0 > 2 || weapon1 < 0 || weapon1 > 2 || (weapon1 != 0 && weapon0 == weapon1) || activeWeapon < 0 || activeWeapon > 1 || (activeWeapon == 1 && weapon1 == 0) || rulerDamage < 1 || rulerDamage > 1000 || daggerDamage < 1 || daggerDamage > 1000)
                return "El inventario no es válido.";
            if (!Finite(x) || !Finite(y) || !Finite(speedBuff) || speedBuff < 0 || speedBuff > 600 || !Finite(elapsed) || elapsed < 0 || elapsed > 31536000)
                return "La posición o el tiempo de partida no son válidos.";
            if (room != 2)
            {
                if (x < -32 || x > 32 || y < -10 || y > 30)
                    return "Espera a volver al escenario antes de guardar.";
                if (room == 0 && (doorUnlocked != (dummyHealth == 0) || (dummyHealth == 0 && tutorialStep != 5)))
                    return "El estado del tutorial no es válido.";
                return null;
            }
            if (district < 1)
                return "Falta el distrito de la partida.";
            DistrictPlan plan = DistrictGenerator.Generate(seed, district, columns, branches, generator, (DistrictStyle)biome);
            if (fingerprint != plan.Fingerprint() || rooms == null || rooms.Length != plan.Rooms.Count || enemies == null || enemies.Length > plan.Rooms.Count * 4)
                return "El mapa guardado no coincide con esta versión.";
            if (plan.AtWorld(x, y) == null)
                return "Espera a volver a una sala antes de guardar.";
            if (refuge < -1 || refuge >= rooms.Length || (refuge >= 0 && (rooms[refuge] == null || plan.Rooms[refuge].Purpose != RoomPurpose.Refuge || !rooms[refuge].visited)))
                return "El refugio guardado no es válido.";
            bool[][] used = new bool[rooms.Length][];
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] == null)
                    return "Falta el estado de una sala.";
                used[i] = new bool[plan.Rooms[i].EnemyX.Count];
                if ((rooms[i].looted && plan.Rooms[i].Purpose != RoomPurpose.Treasure) || (rooms[i].healed && plan.Rooms[i].Purpose != RoomPurpose.Refuge && plan.Rooms[i].Purpose != RoomPurpose.Exit))
                    return "El estado de una sala no es válido.";
                RoomSave r = rooms[i];
                if ((r.inverted && !InverseLayout.IsInverse(plan, plan.Rooms[i])) || (r.inverseLooted && !r.inverted)
                    || (r.inverseExpired && (!r.inverted || r.inverseLooted))
                    || float.IsNaN(r.inverseTime) || r.inverseTime < 0f || r.inverseTime > InverseLayout.MaxDuration)
                    return "El estado de una sala inversa no es válido.";
            }
            foreach (EnemySave enemy in enemies)
            {
                if (enemy == null || enemy.room < 0 || enemy.room >= rooms.Length || enemy.slot < 0 || enemy.slot >= used[enemy.room].Length || used[enemy.room][enemy.slot] || enemy.health < 1 || enemy.health > 1000 || !Finite(enemy.x) || !Finite(enemy.y))
                    return "El estado de los enemigos no es válido.";
                DistrictRoom owner = plan.Rooms[enemy.room];
                if (enemy.x < owner.WorldX - 4 || enemy.x > owner.WorldX + 28 || enemy.y < owner.WorldY - 4 || enemy.y > owner.WorldY + 20 || rooms[enemy.room].rewarded || rooms[enemy.room].looted || (rooms[enemy.room].inverted && !rooms[enemy.room].inverseExpired))
                    return "La posición de un enemigo no es válida.";
                used[enemy.room][enemy.slot] = true;
            }
            return null;
        }
        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
