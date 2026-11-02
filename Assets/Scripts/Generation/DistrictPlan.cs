using System;
using System.Collections.Generic;
using System.Globalization;

namespace GyaruRogue.Generation
{
    public enum RoomPurpose { Entrance, Combat, Treasure, Refuge, Exit }
    [Flags] public enum DoorSide { None = 0, Left = 1, Right = 2, Down = 4, Up = 8 }

    public struct RoomCell : IEquatable<RoomCell>
    {
        public int X, Y;
        public RoomCell(int x, int y) { X = x; Y = y; }
        public bool Equals(RoomCell other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is RoomCell && Equals((RoomCell)obj); }
        public override int GetHashCode() { unchecked { return X * 397 ^ Y; } }
        public RoomCell Offset(int x, int y) { return new RoomCell(X + x, Y + y); }
    }

    public struct RoomSurface
    {
        public float X, Bottom, Width, Height;
        public bool OneWay;
        public float Top { get { return Bottom + Height; } }
        public RoomSurface(float x, float bottom, float width, float height, bool oneWay = false)
        { X = x; Bottom = bottom; Width = width; Height = height; OneWay = oneWay; }
    }

    public sealed class DistrictRoom
    {
        public int Id;
        public RoomCell Cell;
        public RoomPurpose Purpose;
        public DoorSide Doors;
        public bool OnMainPath;
        public int Variant;
        public readonly List<int> Neighbors = new List<int>();
        public readonly List<RoomSurface> Surfaces = new List<RoomSurface>();
        public readonly List<float> EnemyX = new List<float>();
        public float WorldX { get { return Cell.X * DistrictPlan.RoomWidth; } }
        public float WorldY { get { return Cell.Y * DistrictPlan.RoomHeight; } }
        public bool Has(DoorSide side) { return (Doors & side) != 0; }
    }

    public sealed class DistrictPlan
    {
        public const float RoomWidth = 24f, RoomHeight = 16f;
        public const int GeneratorVersion = 1;
        public int Seed, District, EntranceId, ExitId;
        public readonly List<DistrictRoom> Rooms = new List<DistrictRoom>();
        public readonly List<int> MainPath = new List<int>();
        public int EdgeCount
        {
            get { int n = 0; foreach (DistrictRoom room in Rooms) n += room.Neighbors.Count; return n / 2; }
        }

        public DistrictRoom At(RoomCell cell)
        {
            foreach (DistrictRoom room in Rooms) if (room.Cell.Equals(cell)) return room;
            return null;
        }

        public DistrictRoom AtWorld(float x, float y)
        {
            return At(new RoomCell((int)Math.Floor(x / RoomWidth), (int)Math.Floor(y / RoomHeight)));
        }

        // El codigo de comprobacion solo usa numeros: no depende de Unity ni de su fisica.
        public string Fingerprint() { return ComputeFingerprint(true); }
        public string LayoutFingerprint() { return ComputeFingerprint(false); }
        private string ComputeFingerprint(bool includeSeed)
        {
            uint hash = 2166136261;
            if (includeSeed) { Hash(ref hash, Seed); Hash(ref hash, District); }
            Hash(ref hash, GeneratorVersion);
            foreach (DistrictRoom room in Rooms)
            {
                Hash(ref hash, room.Cell.X); Hash(ref hash, room.Cell.Y);
                Hash(ref hash, (int)room.Doors); Hash(ref hash, (int)room.Purpose); Hash(ref hash, room.Variant);
                foreach (RoomSurface s in room.Surfaces)
                {
                    Hash(ref hash, (int)(s.X * 100)); Hash(ref hash, (int)(s.Bottom * 100));
                    Hash(ref hash, (int)(s.Width * 100)); Hash(ref hash, (int)(s.Height * 100));
                    Hash(ref hash, s.OneWay ? 1 : 0);
                }
                foreach (float x in room.EnemyX) Hash(ref hash, (int)(x * 100));
            }
            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static void Hash(ref uint hash, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++) { hash ^= (byte)(value >> (8 * i)); hash *= 16777619; }
            }
        }

        public string Validate()
        {
            if (Rooms.Count < 3 || EntranceId < 0 || EntranceId >= Rooms.Count || ExitId < 0 || ExitId >= Rooms.Count) return "Faltan entrada o salida.";
            if (Rooms[EntranceId].Purpose != RoomPurpose.Entrance || Rooms[ExitId].Purpose != RoomPurpose.Exit)
                return "Entrada o salida sin su funcion.";
            HashSet<RoomCell> occupied = new HashSet<RoomCell>();
            foreach (DistrictRoom room in Rooms)
            {
                if (!occupied.Add(room.Cell)) return "Hay salas superpuestas.";
                foreach (int neighborId in room.Neighbors)
                {
                    if (neighborId < 0 || neighborId >= Rooms.Count) return "Conexion inexistente.";
                    DistrictRoom neighbor = Rooms[neighborId];
                    int dx = neighbor.Cell.X - room.Cell.X, dy = neighbor.Cell.Y - room.Cell.Y;
                    if (Math.Abs(dx) + Math.Abs(dy) != 1 || !neighbor.Neighbors.Contains(room.Id)) return "Conexion rota.";
                    if (!room.Has(Side(dx, dy)) || !neighbor.Has(Side(-dx, -dy))) return "Puertas que no coinciden.";
                }
                foreach (RoomSurface s in room.Surfaces)
                    if (s.Width <= 0 || s.Height <= 0) return "Plataforma sin tamano.";
                // Los enemigos nacen sobre suelo solido, nunca sobre el hueco de una torre.
                foreach (float x in room.EnemyX)
                    if (x < 2 || x > 22 || (room.Has(DoorSide.Down) && x > 9.5f && x < 14.5f))
                        return "Enemigo sin suelo seguro.";
                if (room.Has(DoorSide.Up))
                {
                    float lastTop = 1f;
                    foreach (RoomSurface s in room.Surfaces)
                    {
                        if (!s.OneWay || s.Top <= 1f) continue;
                        if (s.Top - lastTop > 2.51f) return "Escalera demasiado alta.";
                        lastTop = s.Top;
                    }
                    if (lastTop < RoomHeight - 1) return "Torre sin acceso superior.";
                }
            }
            HashSet<int> reached = new HashSet<int>();
            Queue<int> queue = new Queue<int>(); queue.Enqueue(EntranceId); reached.Add(EntranceId);
            while (queue.Count > 0)
                foreach (int id in Rooms[queue.Dequeue()].Neighbors) if (reached.Add(id)) queue.Enqueue(id);
            if (reached.Count != Rooms.Count) return "Hay salas aisladas.";
            return null;
        }

        internal static DoorSide Side(int dx, int dy)
        {
            if (dx < 0) return DoorSide.Left;
            if (dx > 0) return DoorSide.Right;
            return dy < 0 ? DoorSide.Down : DoorSide.Up;
        }
    }

    // Primero dibuja las conexiones; despues elige el contenido de cada modulo.
    public static class DistrictGenerator
    {
        private struct SeedRandom
        {
            private uint state;
            public SeedRandom(int seed, int district)
            {
                unchecked { state = (uint)seed ^ ((uint)district * 0x9E3779B9u); }
                if (state == 0) state = 0xA341316Cu;
            }
            public int Next(int max)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (int)(state % (uint)max);
            }
        }

        public static DistrictPlan Generate(int seed, int district, int columns = 5, int branches = 3)
        {
            columns = Math.Max(4, Math.Min(8, columns));
            branches = Math.Max(1, Math.Min(6, branches));
            DistrictPlan plan = new DistrictPlan { Seed = seed, District = Math.Max(1, district) };
            SeedRandom random = new SeedRandom(seed, plan.District);
            int row = 0;
            DistrictRoom previous = null;
            for (int col = 0; col < columns; col++)
            {
                DistrictRoom room = Add(plan, new RoomCell(col, row), RoomPurpose.Combat, true);
                if (previous != null) Connect(previous, room);
                plan.MainPath.Add(room.Id); previous = room;
                if (col > 0 && col < columns - 1 && (col == 1 || col == columns - 2 || random.Next(2) == 0))
                {
                    int direction = row >= 2 ? -1 : row <= -1 ? 1 : (random.Next(2) == 0 ? -1 : 1);
                    row += direction;
                    DistrictRoom tower = Add(plan, new RoomCell(col, row), RoomPurpose.Combat, true);
                    Connect(previous, tower); plan.MainPath.Add(tower.Id); previous = tower;
                }
            }
            plan.EntranceId = plan.MainPath[0]; plan.ExitId = previous.Id;
            plan.Rooms[plan.EntranceId].Purpose = RoomPurpose.Entrance;
            previous.Purpose = RoomPurpose.Exit;
            plan.Rooms[plan.MainPath[plan.MainPath.Count / 2]].Purpose = RoomPurpose.Refuge;

            // Un circuito une dos puntos del recorrido y permite regresar por otra ruta.
            bool loopAdded = false;
            for (int i = 1; i < plan.MainPath.Count && !loopAdded; i++)
            {
                DistrictRoom left = plan.Rooms[plan.MainPath[i - 1]], right = plan.Rooms[plan.MainPath[i]];
                if (left.Cell.Y != right.Cell.Y || right.Id == plan.ExitId) continue;
                int sign = random.Next(2) == 0 ? -1 : 1;
                for (int attempt = 0; attempt < 2 && !loopAdded; attempt++, sign = -sign)
                {
                    RoomCell a = left.Cell.Offset(0, sign), b = right.Cell.Offset(0, sign);
                    if (plan.At(a) != null || plan.At(b) != null) continue;
                    DistrictRoom first = Add(plan, a, RoomPurpose.Combat, false);
                    DistrictRoom second = Add(plan, b, RoomPurpose.Treasure, false);
                    Connect(left, first); Connect(first, second); Connect(second, right); loopAdded = true;
                }
            }
            int addedBranches = 0;
            // Prueba posiciones en orden estable, sin depender del orden de un Dictionary.
            int offset = random.Next(plan.MainPath.Count);
            for (int attempt = 0; attempt < plan.MainPath.Count * 4 && addedBranches < branches; attempt++)
            {
                DistrictRoom anchor = plan.Rooms[plan.MainPath[(attempt + offset) % plan.MainPath.Count]];
                if (anchor.Id == plan.ExitId) continue;
                int direction = random.Next(2) == 0 ? -1 : 1;
                RoomCell cell = anchor.Cell.Offset(0, direction);
                if (Math.Abs(cell.Y) > 3 || plan.At(cell) != null) continue;
                DistrictRoom side = Add(plan, cell, RoomPurpose.Treasure, false);
                Connect(anchor, side);
                if (random.Next(2) == 0)
                {
                    RoomCell end = cell.Offset(random.Next(2) == 0 ? -1 : 1, 0);
                    if (end.X >= -1 && end.X <= columns && plan.At(end) == null)
                    {
                        side.Purpose = RoomPurpose.Combat;
                        DistrictRoom reward = Add(plan, end, RoomPurpose.Treasure, false); Connect(side, reward);
                    }
                }
                addedBranches++;
            }
            foreach (DistrictRoom room in plan.Rooms)
            {
                room.Variant = random.Next(3);
                BuildModule(room);
                if (room.Purpose == RoomPurpose.Exit) { room.EnemyX.Add(6f); room.EnemyX.Add(18f); }
                else if (room.Purpose == RoomPurpose.Combat || room.Purpose == RoomPurpose.Treasure)
                {
                    room.EnemyX.Add(18f);
                    if (room.Purpose == RoomPurpose.Combat && !room.Has(DoorSide.Up)) room.EnemyX.Add(6f);
                }
            }
            string error = plan.Validate();
            if (error != null) throw new InvalidOperationException(error);
            return plan;
        }

        private static DistrictRoom Add(DistrictPlan plan, RoomCell cell, RoomPurpose purpose, bool main)
        {
            DistrictRoom room = new DistrictRoom { Id = plan.Rooms.Count, Cell = cell, Purpose = purpose, OnMainPath = main };
            plan.Rooms.Add(room); return room;
        }
        private static void Connect(DistrictRoom a, DistrictRoom b)
        {
            if (a.Neighbors.Contains(b.Id)) return;
            a.Neighbors.Add(b.Id); b.Neighbors.Add(a.Id);
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
            else room.Surfaces.Add(new RoomSurface(0, 0, 24, 1));
            if (room.Has(DoorSide.Up))
            {
                room.Surfaces.Add(new RoomSurface(0, 15, 10, 1));
                room.Surfaces.Add(new RoomSurface(14, 15, 10, 1));
            }
            else room.Surfaces.Add(new RoomSurface(0, 15, 24, 1));
            room.Surfaces.Add(room.Has(DoorSide.Left) ? new RoomSurface(0, 5, 1, 10) : new RoomSurface(0, 1, 1, 14));
            room.Surfaces.Add(room.Has(DoorSide.Right) ? new RoomSurface(23, 5, 1, 10) : new RoomSurface(23, 1, 1, 14));
            if (room.Has(DoorSide.Up))
            {
                float[] centers = room.Variant % 2 == 0 ? new float[] { 8, 12, 8, 12, 12, 12 } : new float[] { 16, 12, 16, 12, 12, 12 };
                float[] heights = { 3.5f, 6f, 8.5f, 11f, 13f, 15f };
                for (int i = 0; i < centers.Length; i++)
                {
                    float width = i >= 4 ? 3f : 4f;
                    room.Surfaces.Add(new RoomSurface(centers[i] - width / 2f, heights[i] - .25f, width, .25f, true));
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
