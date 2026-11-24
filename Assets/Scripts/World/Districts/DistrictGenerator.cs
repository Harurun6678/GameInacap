using System;
using System.Collections.Generic;

namespace NeonSpite.Generation
{
    // Primero dibuja las conexiones; despues elige el contenido de cada modulo.
    public static class DistrictGenerator
    {
        private static readonly (string name, float firstX, float secondX, float firstTop, float secondTop)[] CombatLayouts =
        {
            ("Galeria", 4f, 15f, 3.5f, 6f),
            ("Pasarela_Central", 9f, 4f, 3.5f, 6f),
            ("Doble_Nivel", 15f, 8f, 3.5f, 6f),
            ("Andenes", 3f, 14f, 3f, 5.5f),
            ("Galeria_Alta", 14f, 5f, 4f, 6.5f),
            ("Balcon", 8f, 15f, 3f, 5.5f)
        };
        private static readonly float[] LeftTowerCenters = { 8, 12, 8, 12, 12, 12 };
        private static readonly float[] RightTowerCenters = { 16, 12, 16, 12, 12, 12 };
        private static readonly float[] TowerHeights = { 3.5f, 6f, 8.5f, 11f, 13f, 15f };

        private struct SeedRandom
        {
            private uint state;
            public SeedRandom(int seed, int district)
            {
                unchecked
                {
                    state = (uint)seed ^ ((uint)district * 0x9E3779B9u);
                }
                if (state == 0)
                    state = 0xA341316Cu;
            }
            public int Next(int max)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (int)(state % (uint)max);
            }
        }

        public static DistrictPlan Generate(int seed, int district, int columns = 5, int branches = 3, int version = DistrictPlan.GeneratorVersion, DistrictStyle style = DistrictStyle.Industrial)
        {
            if (version < 1 || version > DistrictPlan.GeneratorVersion)
                throw new ArgumentOutOfRangeException("version");
            BiomeRules.For(style);
            columns = Math.Max(4, Math.Min(8, columns));
            branches = Math.Max(1, Math.Min(6, branches));
            DistrictPlan plan = new DistrictPlan { Seed = seed, District = Math.Max(1, district), Version = version, Style = version == 1 ? DistrictStyle.Industrial : style };
            SeedRandom random = new SeedRandom(version == 1 ? seed : unchecked(seed ^ ((int)style * 73471)), plan.District);
            int row = 0;
            DistrictRoom previous = null;
            for (int col = 0; col < columns; col++)
            {
                DistrictRoom room = Add(plan, new RoomCell(col, row), RoomPurpose.Combat, true);
                if (previous != null)
                    Connect(previous, room);
                plan.MainPath.Add(room.Id);
                previous = room;
                if (col > 0 && col < columns - 1 && (col == 1 || col == columns - 2 || (version >= 2 && style == DistrictStyle.Rooftops) || random.Next(2) == 0))
                {
                    int direction = row >= 2 ? -1 : row <= -1 ? 1 : (random.Next(2) == 0 ? -1 : 1);
                    row += direction;
                    DistrictRoom tower = Add(plan, new RoomCell(col, row), RoomPurpose.Combat, true);
                    Connect(previous, tower);
                    plan.MainPath.Add(tower.Id);
                    previous = tower;
                }
            }
            plan.EntranceId = plan.MainPath[0];
            plan.ExitId = previous.Id;
            plan.Rooms[plan.EntranceId].Purpose = RoomPurpose.Entrance;
            previous.Purpose = RoomPurpose.Exit;
            plan.Rooms[plan.MainPath[plan.MainPath.Count / 2]].Purpose = RoomPurpose.Refuge;

            // Un circuito une dos puntos del recorrido y permite regresar por otra ruta.
            bool loopAdded = false;
            for (int i = 1; i < plan.MainPath.Count && !loopAdded; i++)
            {
                DistrictRoom left = plan.Rooms[plan.MainPath[i - 1]], right = plan.Rooms[plan.MainPath[i]];
                if (left.Cell.Y != right.Cell.Y || right.Id == plan.ExitId)
                    continue;
                int sign = random.Next(2) == 0 ? -1 : 1;
                for (int attempt = 0; attempt < 2 && !loopAdded; attempt++, sign = -sign)
                {
                    RoomCell a = left.Cell.Offset(0, sign), b = right.Cell.Offset(0, sign);
                    if (plan.At(a) != null || plan.At(b) != null)
                        continue;
                    DistrictRoom first = Add(plan, a, RoomPurpose.Combat, false);
                    DistrictRoom second = Add(plan, b, RoomPurpose.Treasure, false);
                    Connect(left, first);
                    Connect(first, second);
                    Connect(second, right);
                    loopAdded = true;
                }
            }
            int addedBranches = 0;
            // Prueba posiciones en orden estable, sin depender del orden de un Dictionary.
            int offset = random.Next(plan.MainPath.Count);
            for (int attempt = 0; attempt < plan.MainPath.Count * 4 && addedBranches < branches; attempt++)
            {
                DistrictRoom anchor = plan.Rooms[plan.MainPath[(attempt + offset) % plan.MainPath.Count]];
                if (anchor.Id == plan.ExitId)
                    continue;
                int direction = random.Next(2) == 0 ? -1 : 1;
                RoomCell cell = anchor.Cell.Offset(0, direction);
                if (Math.Abs(cell.Y) > 3 || plan.At(cell) != null)
                    continue;
                DistrictRoom side = Add(plan, cell, RoomPurpose.Treasure, false);
                Connect(anchor, side);
                if (random.Next(2) == 0)
                {
                    RoomCell end = cell.Offset(random.Next(2) == 0 ? -1 : 1, 0);
                    if (end.X >= -1 && end.X <= columns && plan.At(end) == null)
                    {
                        side.Purpose = RoomPurpose.Combat;
                        DistrictRoom reward = Add(plan, end, RoomPurpose.Treasure, false);
                        Connect(side, reward);
                    }
                }
                addedBranches++;
            }
            SetDepths(plan);
            foreach (DistrictRoom room in plan.Rooms)
            {
                room.Variant = random.Next(version == 1 ? 3 : 6);
                BuildModule(room);
                if (version >= 2)
                {
                    CompleteModule(room);
                    PopulateEncounter(room, plan);
                    continue;
                }
                if (room.Purpose == RoomPurpose.Exit)
                {
                    room.EnemyX.Add(6f);
                    room.EnemyX.Add(18f);
                }
                else if (room.Purpose == RoomPurpose.Combat || room.Purpose == RoomPurpose.Treasure)
                {
                    room.EnemyX.Add(18f);
                    if (room.Purpose == RoomPurpose.Combat && !room.Has(DoorSide.Up))
                        room.EnemyX.Add(6f);
                }
            }
            string error = plan.Validate();
            if (error != null)
                throw new InvalidOperationException(error);
            return plan;
        }

        private static void SetDepths(DistrictPlan plan)
        {
            foreach (DistrictRoom room in plan.Rooms)
                room.Depth = -1;
            Queue<int> todo = new Queue<int>();
            todo.Enqueue(plan.EntranceId);
            plan.Rooms[plan.EntranceId].Depth = 0;
            while (todo.Count > 0)
            {
                DistrictRoom room = plan.Rooms[todo.Dequeue()];
                foreach (int id in room.Neighbors)
                    if (plan.Rooms[id].Depth < 0)
                    {
                        plan.Rooms[id].Depth = room.Depth + 1;
                        todo.Enqueue(id);
                    }
            }
        }
        private static void CompleteModule(DistrictRoom room)
        {
            // Las torres conservan sus alturas probadas; varían los encuentros y la decoración.
            if (room.Has(DoorSide.Up))
            {
                room.ModuleId = room.Variant % 2 == 0 ? "Torre_Izquierda" : "Torre_Derecha";
                return;
            }
            if (room.Purpose != RoomPurpose.Combat)
            {
                room.ModuleId = room.Purpose.ToString();
                return;
            }
            var layout = CombatLayouts[room.Variant];
            room.ModuleId = layout.name;
            room.Surfaces.RemoveRange(room.ShellSurfaces, room.Surfaces.Count - room.ShellSurfaces);
            room.Surfaces.Add(new RoomSurface(layout.firstX, layout.firstTop - .25f, 5, .25f, true));
            room.Surfaces.Add(new RoomSurface(layout.secondX, layout.secondTop - .25f, 5, .25f, true));
        }
        private static void PopulateEncounter(DistrictRoom room, DistrictPlan plan)
        {
            // Las versiones anteriores conservan su huella para abrir partidas antiguas.
            if (plan.Version >= 3 && room.Purpose == RoomPurpose.Exit)
                return;
            if (room.Purpose == RoomPurpose.Entrance || room.Purpose == RoomPurpose.Refuge)
                return;
            int count = room.Purpose == RoomPurpose.Exit ? 2 : room.Purpose == RoomPurpose.Treasure ? 1 :
                room.Depth <= 2 || room.Has(DoorSide.Up) ? 1 : Math.Min(3, 1 + plan.District / 2 + BiomeRules.For(plan.Style).EncounterBonus);
            room.EncounterBudget = count;
            if (count == 1)
                room.EnemyX.Add(18f);
            else
            {
                room.EnemyX.Add(6f);
                room.EnemyX.Add(18f);
                if (count == 3)
                    room.EnemyX.Add(21f);
            }
        }

        private static DistrictRoom Add(DistrictPlan plan, RoomCell cell, RoomPurpose purpose, bool main)
        {
            DistrictRoom room = new DistrictRoom { Id = plan.Rooms.Count, Cell = cell, Purpose = purpose, OnMainPath = main };
            plan.Rooms.Add(room);
            return room;
        }
        private static void Connect(DistrictRoom a, DistrictRoom b)
        {
            if (a.Neighbors.Contains(b.Id))
                return;
            a.Neighbors.Add(b.Id);
            b.Neighbors.Add(a.Id);
            a.Doors |= DistrictPlan.Side(b.Cell.X - a.Cell.X, b.Cell.Y - a.Cell.Y);
            b.Doors |= DistrictPlan.Side(a.Cell.X - b.Cell.X, a.Cell.Y - b.Cell.Y);
        }
        private static void BuildModule(DistrictRoom room)
        {
            // Puertas horizontales de cuatro unidades y huecos verticales siempre alineados.
            if (room.Has(DoorSide.Down))
            {
                room.Surfaces.Add(new RoomSurface(0, 0, 10, 1));
                room.Surfaces.Add(new RoomSurface(14, 0, 10, 1));
                room.Surfaces.Add(new RoomSurface(10, .75f, 4, .25f, true));
            }
            else
                room.Surfaces.Add(new RoomSurface(0, 0, 24, 1));
            if (room.Has(DoorSide.Up))
            {
                room.Surfaces.Add(new RoomSurface(0, 15, 10, 1));
                room.Surfaces.Add(new RoomSurface(14, 15, 10, 1));
            }
            else
                room.Surfaces.Add(new RoomSurface(0, 15, 24, 1));
            room.Surfaces.Add(room.Has(DoorSide.Left) ? new RoomSurface(0, 5, 1, 10) : new RoomSurface(0, 1, 1, 14));
            room.Surfaces.Add(room.Has(DoorSide.Right) ? new RoomSurface(23, 5, 1, 10) : new RoomSurface(23, 1, 1, 14));
            room.ShellSurfaces = room.Surfaces.Count;
            if (room.Has(DoorSide.Up))
            {
                float[] centers = room.Variant % 2 == 0 ? LeftTowerCenters : RightTowerCenters;
                for (int i = 0; i < centers.Length; i++)
                {
                    float width = i >= 4 ? 3f : 4f;
                    room.Surfaces.Add(new RoomSurface(centers[i] - width / 2f, TowerHeights[i] - .25f, width, .25f, true));
                }
            }
            else if (room.Purpose == RoomPurpose.Combat)
            {
                // Tres distribuciones de pasarelas para encuentros que no son torres.
                float x = room.Variant == 0 ? 4 : room.Variant == 1 ? 9 : 15;
                room.Surfaces.Add(new RoomSurface(x, 3.25f, 5, .25f, true));
                room.Surfaces.Add(new RoomSurface(9, 5.75f, 5, .25f, true));
            }
        }
    }
}
