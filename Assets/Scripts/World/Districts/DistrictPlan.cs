using System;
using System.Collections.Generic;
using System.Globalization;

namespace NeonSpite.Generation
{
    public enum RoomPurpose
    {
        Entrance, Combat, Treasure, Refuge, Exit
    }
    [Flags]
    public enum DoorSide
    {
        None = 0, Left = 1, Right = 2, Down = 4, Up = 8
    }

    public struct RoomCell : IEquatable<RoomCell>
    {
        public int X, Y;
        public RoomCell(int x, int y)
        {
            X = x;
            Y = y;
        }
        public bool Equals(RoomCell other)
        {
            return X == other.X && Y == other.Y;
        }
        public override bool Equals(object obj)
        {
            return obj is RoomCell && Equals((RoomCell)obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                return X * 397 ^ Y;
            }
        }
        public RoomCell Offset(int x, int y)
        {
            return new RoomCell(X + x, Y + y);
        }
    }

    public struct RoomSurface
    {
        public float X, Bottom, Width, Height;
        public bool OneWay;
        public float Top
        {
            get
            {
                return Bottom + Height;
            }
        }
        public RoomSurface(float x, float bottom, float width, float height, bool oneWay = false)
        {
            X = x;
            Bottom = bottom;
            Width = width;
            Height = height;
            OneWay = oneWay;
        }
    }

    public sealed class DistrictRoom
    {
        public int Id;
        public RoomCell Cell;
        public RoomPurpose Purpose;
        public DoorSide Doors;
        public bool OnMainPath;
        public int Variant;
        public int Depth, EncounterBudget;
        internal int ShellSurfaces;
        public string ModuleId;
        public readonly List<int> Neighbors = new List<int>();
        public readonly List<RoomSurface> Surfaces = new List<RoomSurface>();
        public readonly List<float> EnemyX = new List<float>();
        public float WorldX
        {
            get
            {
                return Cell.X * DistrictPlan.RoomWidth;
            }
        }
        public float WorldY
        {
            get
            {
                return Cell.Y * DistrictPlan.RoomHeight;
            }
        }
        public bool Has(DoorSide side)
        {
            return (Doors & side) != 0;
        }
    }

    public sealed class DistrictPlan
    {
        public const float RoomWidth = 24f, RoomHeight = 16f;
        public const int GeneratorVersion = 3;
        public int Version = GeneratorVersion;
        public DistrictStyle Style;
        public int Seed, District, EntranceId, ExitId;
        public readonly List<DistrictRoom> Rooms = new List<DistrictRoom>();
        public readonly List<int> MainPath = new List<int>();
        public int EdgeCount
        {
            get
            {
                int n = 0;
                foreach (DistrictRoom room in Rooms)
                    n += room.Neighbors.Count;
                return n / 2;
            }
        }

        public DistrictRoom At(RoomCell cell)
        {
            foreach (DistrictRoom room in Rooms)
                if (room.Cell.Equals(cell))
                    return room;
            return null;
        }

        public DistrictRoom AtWorld(float x, float y)
        {
            return At(new RoomCell((int)Math.Floor(x / RoomWidth), (int)Math.Floor(y / RoomHeight)));
        }

        // El codigo de comprobacion solo usa numeros: no depende de Unity ni de su fisica.
        public string Fingerprint()
        {
            return ComputeFingerprint(true);
        }
        public string LayoutFingerprint()
        {
            return ComputeFingerprint(false);
        }
        private string ComputeFingerprint(bool includeSeed)
        {
            uint hash = 2166136261;
            if (includeSeed)
            {
                Hash(ref hash, Seed);
                Hash(ref hash, District);
            }
            Hash(ref hash, Version);
            if (Version >= 2)
                Hash(ref hash, (int)Style);
            foreach (DistrictRoom room in Rooms)
            {
                Hash(ref hash, room.Cell.X);
                Hash(ref hash, room.Cell.Y);
                Hash(ref hash, (int)room.Doors);
                Hash(ref hash, (int)room.Purpose);
                Hash(ref hash, room.Variant);
                if (Version >= 2)
                {
                    Hash(ref hash, room.Depth);
                    Hash(ref hash, room.EncounterBudget);
                }
                foreach (RoomSurface s in room.Surfaces)
                {
                    Hash(ref hash, (int)(s.X * 100));
                    Hash(ref hash, (int)(s.Bottom * 100));
                    Hash(ref hash, (int)(s.Width * 100));
                    Hash(ref hash, (int)(s.Height * 100));
                    Hash(ref hash, s.OneWay ? 1 : 0);
                }
                foreach (float x in room.EnemyX)
                    Hash(ref hash, (int)(x * 100));
            }
            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static void Hash(ref uint hash, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (byte)(value >> (8 * i));
                    hash *= 16777619;
                }
            }
        }

        public string Validate()
        {
            if (Rooms.Count < 3 || EntranceId < 0 || EntranceId >= Rooms.Count || ExitId < 0 || ExitId >= Rooms.Count)
                return "Faltan entrada o salida.";
            if (Rooms[EntranceId].Purpose != RoomPurpose.Entrance || Rooms[ExitId].Purpose != RoomPurpose.Exit)
                return "Entrada o salida sin su funcion.";
            HashSet<RoomCell> occupied = new HashSet<RoomCell>();
            foreach (DistrictRoom room in Rooms)
            {
                if (!occupied.Add(room.Cell))
                    return "Hay salas superpuestas.";
                foreach (int neighborId in room.Neighbors)
                {
                    if (neighborId < 0 || neighborId >= Rooms.Count)
                        return "Conexion inexistente.";
                    DistrictRoom neighbor = Rooms[neighborId];
                    int dx = neighbor.Cell.X - room.Cell.X, dy = neighbor.Cell.Y - room.Cell.Y;
                    if (Math.Abs(dx) + Math.Abs(dy) != 1 || !neighbor.Neighbors.Contains(room.Id))
                        return "Conexion rota.";
                    if (!room.Has(Side(dx, dy)) || !neighbor.Has(Side(-dx, -dy)))
                        return "Puertas que no coinciden.";
                }
                foreach (RoomSurface s in room.Surfaces)
                    if (s.Width <= 0 || s.Height <= 0)
                        return "Plataforma sin tamano.";
                // Los enemigos nacen sobre suelo solido, nunca sobre el hueco de una torre.
                foreach (float x in room.EnemyX)
                    if (x < 2 || x > 22 || (room.Has(DoorSide.Down) && x > 9.5f && x < 14.5f))
                        return "Enemigo sin suelo seguro.";
                if (room.Has(DoorSide.Up))
                {
                    float lastTop = 1f;
                    foreach (RoomSurface s in room.Surfaces)
                    {
                        if (!s.OneWay || s.Top <= 1f)
                            continue;
                        if (s.Top - lastTop > 2.51f)
                            return "Escalera demasiado alta.";
                        lastTop = s.Top;
                    }
                    if (lastTop < RoomHeight - 1)
                        return "Torre sin acceso superior.";
                }
            }
            HashSet<int> reached = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(EntranceId);
            reached.Add(EntranceId);
            while (queue.Count > 0)
                foreach (int id in Rooms[queue.Dequeue()].Neighbors)
                    if (reached.Add(id))
                        queue.Enqueue(id);
            if (reached.Count != Rooms.Count)
                return "Hay salas aisladas.";
            return null;
        }

        internal static DoorSide Side(int dx, int dy)
        {
            if (dx < 0)
                return DoorSide.Left;
            if (dx > 0)
                return DoorSide.Right;
            return dy < 0 ? DoorSide.Down : DoorSide.Up;
        }
    }
}
