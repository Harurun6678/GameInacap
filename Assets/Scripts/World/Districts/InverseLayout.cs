using System;
using System.Collections.Generic;

namespace NeonSpite.Generation
{
    public enum InverseMotion { None, Horizontal, Vertical }
    public enum InverseReward { Fragments, Healing, MaxHealth, Damage }

    // Plataforma de una sala inversa, en coordenadas locales de la sala (24 x 16).
    // Oscila alrededor de (X, Top): Offset(t) = Amplitude * sin(2π (t / Period + Phase)).
    public struct InversePlatform
    {
        public float X, Top, Width, Amplitude, Period, Phase;
        public InverseMotion Motion;
        public bool Crumbles;

        public float Offset(float time)
        {
            if (Motion == InverseMotion.None || Period <= 0f) return 0f;
            return Amplitude * (float)Math.Sin(2.0 * Math.PI * (time / Period + Phase));
        }
        public float CenterAt(float time) { return Motion == InverseMotion.Horizontal ? X + Offset(time) : X; }
        public float TopAt(float time) { return Motion == InverseMotion.Vertical ? Top + Offset(time) : Top; }
        public float LeftAt(float time) { return CenterAt(time) - Width / 2f; }
        public float RightAt(float time) { return CenterAt(time) + Width / 2f; }
    }

    // Sala inversa: sin enemigos; el reto son plataformas angostas, móviles e inestables.
    // El suelo sigue siendo firme: caer solo obliga a volver a subir.
    public sealed class InverseRoom
    {
        // Indices virtuales del suelo junto a la puerta y sobre la escotilla (puntos de partida de los saltos).
        public const int SidePad = -1, CenterPad = -2;
        public bool Tower, HasCenterPad, Mirrored;
        public int Template;
        public readonly List<InversePlatform> Platforms = new List<InversePlatform>();
        public int ChestPlatform, ExitPlatform = -1;
        public InverseReward Reward;
        public int RewardAmount;
        // Segundos que dura la inversión con el jugador dentro; al llegar a cero la sala vuelve a la normalidad.
        public float Duration;
        // Saltos del recorrido: (desde, hasta, saltos necesarios: 1 simple, 2 doble).
        public readonly List<int[]> Links = new List<int[]>();
        public float ChestX { get { return Platforms[ChestPlatform].X; } }
        public float ChestTop { get { return Platforms[ChestPlatform].Top; } }

        public float SidePadFrom { get { return Mirrored ? 1f : 20f; } }
        public float SidePadTo { get { return Mirrored ? 4f : 23f; } }
        public const float CenterPadFrom = 9.5f, CenterPadTo = 14.5f;
    }

    // Decide qué salas se invierten y cómo es cada una. Todo sale de la semilla:
    // no modifica DistrictPlan, así que la huella del mapa no cambia.
    public static class InverseLayout
    {
        public const float Chance = .35f;   // probabilidad de que una sala de combate se invierta
        public const float MaxDuration = 120f;

        public static bool Eligible(DistrictRoom room)
        {
            // Las salas de combate tienen una sola puerta lateral y además escotilla (paso) o torre.
            // Las salas de paso siempre tienen escotilla: el recorrido parte de la zona central.
            return room.Purpose == RoomPurpose.Combat && (room.Has(DoorSide.Left) != room.Has(DoorSide.Right))
                && (room.Has(DoorSide.Up) || room.Has(DoorSide.Down));
        }

        public static bool IsInverse(DistrictPlan plan, DistrictRoom room)
        {
            if (!Eligible(room)) return false;
            uint state = Seed(plan, room, 0x51ED27u);
            return Next(ref state, 1000) < (int)(Chance * 1000);
        }

        public static InverseRoom Build(DistrictPlan plan, DistrictRoom room)
        {
            if (!IsInverse(plan, room)) return null;
            uint state = Seed(plan, room, 0xC0FFEEu);
            InverseRoom r = new InverseRoom
            {
                Tower = room.Has(DoorSide.Up), HasCenterPad = room.Has(DoorSide.Down),
                // Las plantillas están dibujadas con la puerta a la derecha; con puerta a la izquierda se reflejan.
                Mirrored = room.Has(DoorSide.Left), Template = Next(ref state, 2)
            };
            if (r.Tower) { if (r.Template == 0) TowerA(r); else TowerB(r); }
            else { if (r.Template == 0) PassA(r); else PassB(r); }
            // Sin escotilla, los saltos que salen de la zona central no existen.
            if (!r.HasCenterPad) r.Links.RemoveAll(link => link[0] == InverseRoom.CenterPad || link[1] == InverseRoom.CenterPad);

            // Las torres son más largas; cada distrito quita 2 segundos (mínimo 20).
            r.Duration = Math.Max(20f, (r.Tower ? 38f : 30f) - 2f * (plan.District - 1));

            // Cada distrito acelera las plataformas (hasta un 30%).
            float pace = Math.Max(.7f, 1f - .1f * (plan.District - 1));
            for (int i = 0; i < r.Platforms.Count; i++)
            {
                InversePlatform p = r.Platforms[i];
                p.Period *= pace;
                if (r.Mirrored)
                {
                    p.X = DistrictPlan.RoomWidth - p.X;
                    if (p.Motion == InverseMotion.Horizontal) p.Phase += .5f; // reflejo del vaivén
                }
                r.Platforms[i] = p;
            }

            // Contenido del cofre: fichas, curación o una mejora permanente.
            int roll = Next(ref state, 100);
            if (roll < 45) { r.Reward = InverseReward.Fragments; r.RewardAmount = Math.Min(5, 2 + plan.District); }
            else if (roll < 75) { r.Reward = InverseReward.Healing; r.RewardAmount = 50; }
            else if (roll < 88) { r.Reward = InverseReward.MaxHealth; r.RewardAmount = 10; }
            else { r.Reward = InverseReward.Damage; r.RewardAmount = 4; }
            return r;
        }

        // ---------- Plantillas: puerta a la derecha, anchos enteros para que el dibujo calce ----------

        private static InversePlatform Still(float x, float top, float width, bool crumbles = true)
        { return new InversePlatform { X = x, Top = top, Width = width, Crumbles = crumbles }; }
        private static InversePlatform Moving(float x, float top, float width, InverseMotion motion, float amplitude, float period, float phase)
        { return new InversePlatform { X = x, Top = top, Width = width, Motion = motion, Amplitude = amplitude, Period = period, Phase = phase, Crumbles = true }; }
        private static void Link(InverseRoom r, int from, int to, int jumps) { r.Links.Add(new[] { from, to, jumps }); }
        private const int Side = InverseRoom.SidePad, Center = InverseRoom.CenterPad;
        private const InverseMotion H = InverseMotion.Horizontal, V = InverseMotion.Vertical;

        // Sala de paso: un salto corto lleva de la escotilla a la puerta; el cofre está
        // al otro lado, tras una espiral de plataformas que vuelve sobre la escotilla.
        private static void PassA(InverseRoom r)
        {
            r.Platforms.Add(Still(17f, 3.2f, 1));                        // 0
            r.Platforms.Add(Moving(7.5f, 3.4f, 2, H, 1.2f, 3f, 0f));     // 1
            r.Platforms.Add(Still(4f, 5.6f, 1));                         // 2
            r.Platforms.Add(Moving(7.5f, 7.8f, 1, V, 1f, 3.4f, .25f));   // 3
            r.Platforms.Add(Moving(11.5f, 9.8f, 2, H, 1f, 3.6f, 0f));    // 4
            r.Platforms.Add(Still(15.5f, 11.8f, 3, false));              // 5 cofre
            r.ChestPlatform = 5;
            Link(r, Center, 0, 1); Link(r, 0, Side, 1); Link(r, Side, 0, 1);
            Link(r, Center, 1, 1); Link(r, 1, 2, 1); Link(r, 2, 3, 1); Link(r, 3, 4, 1); Link(r, 4, 5, 1);
        }

        // Sala de paso: escalones de una baldosa y ascensores hasta un cofre en lo alto.
        private static void PassB(InverseRoom r)
        {
            r.Platforms.Add(Moving(17.5f, 3.4f, 2, H, 1f, 2.6f, 0f));    // 0
            r.Platforms.Add(Still(7f, 3f, 1));                           // 1
            r.Platforms.Add(Moving(4f, 4.8f, 1, V, 1.2f, 3.2f, 0f));     // 2
            r.Platforms.Add(Still(7f, 7.4f, 1));                         // 3
            r.Platforms.Add(Moving(10.5f, 9f, 1, V, 1f, 3f, .5f));       // 4
            r.Platforms.Add(Still(5f, 11.4f, 3, false));                 // 5 cofre
            r.ChestPlatform = 5;
            Link(r, Center, 0, 1); Link(r, 0, Side, 1); Link(r, Side, 0, 1);
            Link(r, Center, 1, 1); Link(r, 1, 2, 1); Link(r, 2, 3, 1); Link(r, 3, 4, 1); Link(r, 4, 5, 1);
        }

        // Torre: se sube en zigzag hasta la salida del techo; el cofre en una cornisa lateral.
        private static void TowerA(InverseRoom r)
        {
            r.Platforms.Add(Still(17.5f, 3.2f, 1));                      // 0
            r.Platforms.Add(Moving(14.5f, 5.4f, 2, H, 1f, 3f, 0f));      // 1
            r.Platforms.Add(Moving(10f, 7.6f, 1, V, 1f, 3.2f, .5f));     // 2
            r.Platforms.Add(Moving(13.5f, 10.4f, 2, H, 1.2f, 3.6f, .25f)); // 3
            r.Platforms.Add(Still(10.5f, 12.6f, 1));                     // 4
            r.Platforms.Add(Still(12f, 15f, 3, false));                  // 5 salida del techo
            r.Platforms.Add(Still(6.5f, 6f, 1));                         // 6
            r.Platforms.Add(Still(3.5f, 9.6f, 3, false));                // 7 cofre
            r.ExitPlatform = 5; r.ChestPlatform = 7;
            Link(r, Side, 0, 1); Link(r, Center, 0, 1); Link(r, 0, 1, 1); Link(r, 1, 2, 1); Link(r, 2, 3, 1); Link(r, 3, 4, 1); Link(r, 4, 5, 1);
            Link(r, 2, 6, 1); Link(r, 6, 7, 2);
        }

        // Torre: ascensores alternados con escalones que se derrumban.
        private static void TowerB(InverseRoom r)
        {
            r.Platforms.Add(Moving(17f, 3.6f, 2, V, 1f, 3f, 0f));        // 0
            r.Platforms.Add(Still(13.5f, 6.2f, 1));                      // 1
            r.Platforms.Add(Moving(10f, 8f, 2, H, 1.3f, 3.4f, 0f));      // 2
            r.Platforms.Add(Moving(13.5f, 10.4f, 1, V, .8f, 2.8f, .5f)); // 3
            r.Platforms.Add(Still(10.5f, 12.6f, 1));                     // 4
            r.Platforms.Add(Still(12f, 15f, 3, false));                  // 5 salida del techo
            r.Platforms.Add(Still(5f, 6.4f, 1));                         // 6
            r.Platforms.Add(Still(3f, 8.8f, 3, false));                  // 7 cofre
            r.ExitPlatform = 5; r.ChestPlatform = 7;
            Link(r, Side, 0, 1); Link(r, Center, 0, 1); Link(r, 0, 1, 1); Link(r, 1, 2, 1); Link(r, 2, 3, 1); Link(r, 3, 4, 1); Link(r, 4, 5, 1);
            Link(r, 2, 6, 1); Link(r, 6, 7, 1);
        }

        // ---------- Comprobación ----------

        // Física del jugador (GyaruDemoBootstrap / PlayerController): salto 14, gravedad 9.81 x 3.3,
        // caída 1.25 veces más pesada, carrera 8 u/s. Se exige con margen.
        private const float JumpSpeed = 14f, Gravity = 9.81f * 3.3f, FallGravity = Gravity * 1.25f, RunSpeed = 8f;
        private const float Safety = .7f;      // solo se cuenta el 70% del alcance teórico
        private const float HeadRoom = 2.3f;   // espacio libre entre plataformas superpuestas
        private const float Floor = 1f, Ceiling = 15f;

        public static float MaxRise(int jumps) { return jumps * JumpSpeed * JumpSpeed / (2f * Gravity) - .45f; }

        // Distancia horizontal entre bordes alcanzable al subir (o bajar) dy unidades.
        public static float Reach(float dy, int jumps)
        {
            if (dy > MaxRise(jumps)) return -1f;
            float apex = jumps * JumpSpeed * JumpSpeed / (2f * Gravity);
            float rise = jumps * JumpSpeed / Gravity;
            float fall = (float)Math.Sqrt(2f * Math.Max(0f, apex - dy) / FallGravity);
            return RunSpeed * (rise + fall) * Safety;
        }

        // Devuelve null si la sala se puede recorrer; si no, el motivo.
        public static string Validate(InverseRoom room, float duration = 40f, float step = .05f)
        {
            List<InversePlatform> p = room.Platforms;
            if (room.ChestPlatform < 0 || room.ChestPlatform >= p.Count || p[room.ChestPlatform].Motion != InverseMotion.None) return "Cofre sin plataforma fija.";
            if (room.Tower != (room.ExitPlatform >= 0)) return "Torre sin salida superior.";
            int samples = (int)(duration / step);
            int[] reachable = new int[room.Links.Count];
            for (int s = 0; s <= samples; s++)
            {
                float t = s * step;
                for (int i = 0; i < p.Count; i++)
                {
                    float left = p[i].LeftAt(t), right = p[i].RightAt(t), top = p[i].TopAt(t);
                    bool exit = i == room.ExitPlatform;
                    if (left < 1.2f || right > 22.8f || top < 2.6f) return "Plataforma " + i + " fuera de la sala.";
                    // Solo la salida de la torre llega al techo, y siempre bajo el hueco.
                    if (exit ? (left < 10f || right > 14f) : top > Ceiling - HeadRoom - .1f) return "Plataforma " + i + " choca con el techo.";
                    if (OverPad(room, left, right) && top - .25f < Floor + 1.9f) return "Plataforma " + i + " demasiado baja sobre una zona segura.";
                    for (int j = i + 1; j < p.Count; j++)
                    {
                        float overlap = Math.Min(right, p[j].RightAt(t)) - Math.Max(left, p[j].LeftAt(t));
                        if (overlap > -.2f && Math.Abs(top - p[j].TopAt(t)) < HeadRoom) return "Plataformas " + i + " y " + j + " se estorban.";
                    }
                }
                for (int k = 0; k < room.Links.Count; k++)
                {
                    int[] link = room.Links[k];
                    float aLeft, aRight, aTop, bLeft, bRight, bTop;
                    Bounds(room, link[0], t, out aLeft, out aRight, out aTop);
                    Bounds(room, link[1], t, out bLeft, out bRight, out bTop);
                    float gap = Math.Max(bLeft - aRight, aLeft - bRight);
                    float reach = Reach(bTop - aTop, link[2]);
                    if (reach >= 0f && gap <= reach) reachable[k]++;
                }
            }
            for (int k = 0; k < room.Links.Count; k++)
                // Cada salto debe estar disponible al menos el 20% del tiempo: no exige sincronización perfecta.
                if (reachable[k] < samples * .2f) return "Salto " + room.Links[k][0] + " -> " + room.Links[k][1] + " casi imposible (" + (100 * reachable[k] / samples) + "%).";
            return null;
        }

        private static bool OverPad(InverseRoom room, float left, float right)
        {
            bool side = right > room.SidePadFrom && left < room.SidePadTo;
            bool center = room.HasCenterPad && right > InverseRoom.CenterPadFrom && left < InverseRoom.CenterPadTo;
            return side || center;
        }

        private static void Bounds(InverseRoom room, int index, float t, out float left, out float right, out float top)
        {
            if (index == InverseRoom.SidePad) { left = room.SidePadFrom; right = room.SidePadTo; top = Floor; return; }
            if (index == InverseRoom.CenterPad) { left = InverseRoom.CenterPadFrom; right = InverseRoom.CenterPadTo; top = Floor; return; }
            InversePlatform p = room.Platforms[index];
            left = p.LeftAt(t); right = p.RightAt(t); top = p.TopAt(t);
        }

        public static uint Seed(DistrictPlan plan, DistrictRoom room, uint salt)
        {
            uint state;
            unchecked { state = (uint)plan.Seed ^ ((uint)plan.District * 0x9E3779B9u) ^ ((uint)(room.Id + 1) * 0x85EBCA6Bu) ^ salt; }
            return state == 0 ? 0xA341316Cu : state;
        }
        public static int Next(ref uint state, int max)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)max);
        }
    }
}
