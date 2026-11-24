using UnityEngine;
using UnityEngine.UI;
using NeonSpite.Generation;
using K = KenneyTheme;

// Vista pasiva del mapa. El controlador abre M como una pantalla de pausa.
public sealed class DistrictMapUI : MonoBehaviour
{
    private DistrictRuntime district;
    private NeonSpiteGame demo;
    private RectTransform panel, drawing;
    private Text title;
    private CanvasGroup opacity;
    private int revision = -1, current = -1;
    public void Initialize(DistrictRuntime runtime, NeonSpiteGame owner)
    {
        district = runtime;
        demo = owner;
        RectTransform root = K.Rect(transform, "Canvas_Minimapa", 0, 0, 1600, 900);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 119;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        panel = K.Panel(root, "Minimapa_Kenney", -193, -230, 318, 210, null, new Vector2(1, 1));
        opacity = panel.gameObject.AddComponent<CanvasGroup>();
        title = K.Label(panel, "Ubicacion", "", 0, 80, 276, 28, 15, K.Gold);
        drawing = K.Rect(panel, "Salas_Descubiertas", 0, -2, 280, 130);
        K.Label(panel, "Atajo", "M   MAPA DEL DISTRITO", 0, -85, 276, 24, 13, K.Muted);
        Redraw();
    }
    private void Update()
    {
        if (district == null || demo == null)
            return;
        bool visible = !demo.IsModalOpen && !demo.InMainMenu && GameSettings.Current.minimap;
        panel.gameObject.SetActive(visible);
        opacity.alpha = GameSettings.Current.hudOpacity;
        if (visible && (revision != district.Revision || current != district.CurrentRoomId))
            Redraw();
    }
    private void Redraw()
    {
        revision = district.Revision;
        current = district.CurrentRoomId;
        title.text = "DISTRITO " + district.Plan.District.ToString("00") + "   /   " + district.VisitedCount + " DE " + district.Plan.Rooms.Count;
        Draw(drawing, district, 280, 130, false);
    }
    public static void Draw(RectTransform parent, DistrictRuntime runtime, float width, float height, bool detailed)
    {
        K.Clear(parent);
        if (runtime == null)
        {
            K.Image(parent, "Zona", 0, 35, 60, 60, K.Muted, "Icons/locked").preserveAspect = true;
            K.Label(parent, "SinMapa", "El mapa se descubre al entrar al distrito.\nCompleta el entrenamiento y visita el refugio.", 0, -45, width - 50, 85, 22, K.Muted, TextAnchor.MiddleCenter);
            return;
        }
        DistrictPlan plan = runtime.Plan;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (DistrictRoom room in plan.Rooms)
        {
            minX = Mathf.Min(minX, room.Cell.X);
            maxX = Mathf.Max(maxX, room.Cell.X);
            minY = Mathf.Min(minY, room.Cell.Y);
            maxY = Mathf.Max(maxY, room.Cell.Y);
        }
        float step = Mathf.Min(width / (maxX - minX + 1), height / (maxY - minY + 1));
        foreach (DistrictRoom room in plan.Rooms)
        {
            if (!runtime.IsRevealed(room.Id))
                continue;
            Vector2 p = Point(room, step, minX, maxX, minY, maxY);
            foreach (int id in room.Neighbors)
            {
                if (id <= room.Id || !runtime.IsRevealed(id))
                    continue;
                Vector2 q = Point(plan.Rooms[id], step, minX, maxX, minY, maxY);
                Color color = runtime.IsVisited(room.Id) && runtime.IsVisited(id) ? K.Gold * .6f : K.Line;
                K.Image(parent, "Conexion_" + room.Id + "_" + id, (p.x + q.x) / 2, (p.y + q.y) / 2, Mathf.Abs(q.x - p.x) + 2, Mathf.Abs(q.y - p.y) + 2, color);
            }
        }
        foreach (DistrictRoom room in plan.Rooms)
        {
            if (!runtime.IsRevealed(room.Id))
                continue;
            bool visited = runtime.IsVisited(room.Id), active = room.Id == runtime.CurrentRoomId;
            Vector2 p = Point(room, step, minX, maxX, minY, maxY);
            float w = step * .73f, h = step * .63f;
            bool inverted = runtime.IsInverted(room.Id);
            Color accent = active ? K.Rose : !visited ? K.Line : inverted ? InverseColor : (room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Exit) ? K.Mint : room.Purpose == RoomPurpose.Treasure ? K.Gold : K.Muted;
            RectTransform node = K.Panel(parent, "Sala_" + room.Id, p.x, p.y, w, h, accent);
            // Símbolos del Minimap Pack: refugio, peligro y salida.
            // Salas inversas: estrella mientras su mochila siga sin romper.
            string icon = !visited ? "Icons/question" : inverted ? (runtime.IsInverseLooted(room.Id) ? "Icons/checkmark" : "Icons/star") : room.Purpose == RoomPurpose.Refuge || room.Purpose == RoomPurpose.Entrance ? "Map/tile_0019" : room.Purpose == RoomPurpose.Exit ? "Map/tile_0023" : room.Purpose == RoomPurpose.Treasure ? (runtime.IsLooted(room.Id) ? "Icons/checkmark" : "Icons/star") : runtime.IsCleared(room.Id) ? "Icons/checkmark" : "Map/tile_0018";
            float iconSize = Mathf.Clamp(step * .3f, 8, 30);
            K.Image(node, "Tipo", 0, 0, iconSize, iconSize, visited ? Color.white : K.Muted, icon).preserveAspect = true;
            if (active)
            {
                K.Image(node, "Tu_Posicion", 0, -h * .5f + 3, Mathf.Max(5, w * .4f), 3, K.Rose);
                if (detailed)
                    K.Label(node, "Numero", (room.Id + 1).ToString("00"), w * .5f - 13, h * .5f - 12, 22, 20, 12, K.Rose, TextAnchor.MiddleCenter);
            }
        }
    }
    private static readonly Color InverseColor = new Color(.86f, .42f, 1f);
    private static Vector2 Point(DistrictRoom room, float step, int minX, int maxX, int minY, int maxY)
    {
        return new Vector2((room.Cell.X - (minX + maxX) * .5f) * step, (room.Cell.Y - (minY + maxY) * .5f) * step);
    }
}
