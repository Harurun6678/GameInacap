using System;
using UnityEditor;
using UnityEngine;
using NeonSpite.Generation;

// Permite revisar conexiones y repetir semillas antes de entrar en Play.
public class DistrictPreviewWindow : EditorWindow
{
    private int seed = 12345, stage = 1, columns = 5, branches = 3;
    private DistrictPlan preview;
    private DistrictStyle style;
    private Vector2 scroll;
    private int selectedRoom;
    private bool inspectRoom;
    [MenuItem("Neon Spite/Generador de distritos")]
    public static void Open()
    {
        GetWindow<DistrictPreviewWindow>("Generador de distritos").minSize = new Vector2(760, 640);
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("DISTRITO INDUSTRIAL", EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("Semilla", seed);
        style = (DistrictStyle)EditorGUILayout.EnumPopup("Zona", style);
        stage = EditorGUILayout.IntSlider("Distrito", stage, 1, 5);
        columns = EditorGUILayout.IntSlider("Largo principal", columns, 4, 8);
        branches = EditorGUILayout.IntSlider("Desvios solicitados", branches, 1, 6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generar vista"))
            preview = DistrictGenerator.Generate(seed, stage, columns, branches, DistrictPlan.GeneratorVersion, style);
        if (GUILayout.Button("Otra semilla"))
        {
            seed = UnityEngine.Random.Range(1, int.MaxValue);
            preview = DistrictGenerator.Generate(seed, stage, columns, branches, DistrictPlan.GeneratorVersion, style);
        }
        if (GUILayout.Button("Aplicar a escena"))
            ApplyToScene();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Azul: entrada. Rosa: combate. Verde: refugio. Dorado: cofre. Verde claro: salida segura. Las lineas son puertas reales entre salas. El mapa del jugador solo descubre las salas cercanas.", MessageType.Info);
        if (preview == null)
            return;
        inspectRoom = EditorGUILayout.Toggle("Ver módulo de sala", inspectRoom);
        if (inspectRoom)
        {
            string[] names = new string[preview.Rooms.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = i.ToString("00") + " / " + preview.Rooms[i].ModuleId;
            selectedRoom = EditorGUILayout.Popup("Sala", Mathf.Clamp(selectedRoom, 0, names.Length - 1), names);
            DrawRoom(preview.Rooms[selectedRoom]);
            return;
        }
        EditorGUILayout.LabelField(preview.Rooms.Count + " salas / " + preview.EdgeCount + " conexiones / codigo " + preview.Fingerprint());
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Rect area = GUILayoutUtility.GetRect(1350, 850, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        int minX = int.MaxValue, maxY = int.MinValue;
        foreach (DistrictRoom room in preview.Rooms)
        {
            minX = Math.Min(minX, room.Cell.X);
            maxY = Math.Max(maxY, room.Cell.Y);
        }
        foreach (DistrictRoom room in preview.Rooms)
        {
            Vector2 position = Position(room, area, minX, maxY);
            foreach (int neighbor in room.Neighbors)
            {
                if (neighbor <= room.Id)
                    continue;
                Handles.color = new Color(.5f, .6f, .7f);
                Handles.DrawLine(position, Position(preview.Rooms[neighbor], area, minX, maxY));
            }
        }
        foreach (DistrictRoom room in preview.Rooms)
        {
            Vector2 position = Position(room, area, minX, maxY);
            Color color = room.Purpose == RoomPurpose.Entrance ? new Color(.2f, .55f, .85f) : room.Purpose == RoomPurpose.Exit ? new Color(.35f, .85f, .65f) :
                room.Purpose == RoomPurpose.Refuge ? new Color(.2f, .6f, .4f) : room.Purpose == RoomPurpose.Treasure ? new Color(.75f, .55f, .2f) : new Color(.5f, .25f, .4f);
            Rect box = new Rect(position.x - 62, position.y - 35, 124, 70);
            EditorGUI.DrawRect(box, color);
            GUI.Label(box, room.Id.ToString("00") + " " + DistrictRuntime.PurposeName(room.Purpose) + "\n" + room.ModuleId + " / " + room.EnemyX.Count + " enemigos", EditorStyles.centeredGreyMiniLabel);
        }
        EditorGUILayout.EndScrollView();
    }
    private void DrawRoom(DistrictRoom room)
    {
        EditorGUILayout.LabelField(room.ModuleId + " · profundidad " + room.Depth + " · " + room.EncounterBudget + " enemigos", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("El módulo se elige por sus puertas y su función. Oro: pasarelas descendibles. Azul: sólidos. Rosa: aparición de enemigo. Las plataformas superiores de las torres quedan dentro del hueco del techo.", MessageType.Info);
        Rect area = GUILayoutUtility.GetRect(720, 480, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(area, new Color(.025f, .04f, .07f));
        foreach (RoomSurface surface in room.Surfaces)
        {
            Rect platform = new Rect(area.x + surface.X / 24 * area.width, area.y + (16 - surface.Top) / 16 * area.height, surface.Width / 24 * area.width, Mathf.Max(3, surface.Height / 16 * area.height));
            EditorGUI.DrawRect(platform, surface.OneWay ? new Color(.92f, .73f, .4f) : new Color(.3f, .42f, .6f));
        }
        foreach (float x in room.EnemyX)
            EditorGUI.DrawRect(new Rect(area.x + (x - .4f) / 24 * area.width, area.y + (16 - 2.55f) / 16 * area.height, .8f / 24 * area.width, 1.5f / 16 * area.height), new Color(.95f, .35f, .5f));
    }

    private static Vector2 Position(DistrictRoom room, Rect area, int minX, int maxY)
    {
        return new Vector2(area.x + 60 + (room.Cell.X - minX) * 146, area.y + 40 + (maxY - room.Cell.Y) * 106);
    }
    private void ApplyToScene()
    {
        NeonSpiteGame demo = UnityEngine.Object.FindObjectOfType<NeonSpiteGame>();
        if (demo == null)
        {
            EditorUtility.DisplayDialog("Generador", "Abre MainRoom o DistritoProcedural primero.", "Aceptar");
            return;
        }
        Undo.RecordObject(demo, "Aplicar semilla del distrito");
        SerializedObject settings = new SerializedObject(demo);
        settings.FindProperty("startingStyle").enumValueIndex = (int)style;
        settings.FindProperty("useRandomSeed").boolValue = false;
        settings.FindProperty("fixedSeed").intValue = seed;
        settings.FindProperty("mainPathColumns").intValue = columns;
        settings.FindProperty("optionalBranches").intValue = branches;
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(demo);
        Debug.Log("Semilla " + seed + " aplicada. Guarda la escena; se usara en el siguiente inicio de partida.", demo);
    }
}
