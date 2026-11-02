using System;
using UnityEditor;
using UnityEngine;
using GyaruRogue.Generation;

// Permite revisar conexiones y repetir semillas antes de entrar en Play.
public class DistrictPreviewWindow : EditorWindow
{
    private int seed = 12345, stage = 1, columns = 5, branches = 3;
    private DistrictPlan preview;
    private Vector2 scroll;
    [MenuItem("Gyaru Rogue/Generador de distritos")]
    public static void Open() { GetWindow<DistrictPreviewWindow>("Generador de distritos"); }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("DISTRITO INDUSTRIAL",EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("Semilla",seed);
        stage = EditorGUILayout.IntSlider("Distrito",stage,1,5);
        columns = EditorGUILayout.IntSlider("Largo principal",columns,4,8);
        branches = EditorGUILayout.IntSlider("Desvios solicitados",branches,1,6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generar vista")) preview = DistrictGenerator.Generate(seed,stage,columns,branches);
        if (GUILayout.Button("Otra semilla")) { seed = UnityEngine.Random.Range(1,int.MaxValue); preview = DistrictGenerator.Generate(seed,stage,columns,branches); }
        if (GUILayout.Button("Aplicar a escena")) ApplyToScene();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Azul: entrada. Rosa: combate. Verde: refugio. Dorado: cofre. Rojo: salida. Las lineas son puertas reales entre salas. El mapa del jugador solo descubre las salas cercanas.",MessageType.Info);
        if (preview == null) return;
        EditorGUILayout.LabelField(preview.Rooms.Count + " salas / " + preview.EdgeCount + " conexiones / codigo " + preview.Fingerprint());
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Rect area = GUILayoutUtility.GetRect(900,650,GUILayout.ExpandWidth(false),GUILayout.ExpandHeight(false));
        int minX = int.MaxValue,maxY = int.MinValue;
        foreach (DistrictRoom room in preview.Rooms) { minX = Math.Min(minX,room.Cell.X); maxY = Math.Max(maxY,room.Cell.Y); }
        foreach (DistrictRoom room in preview.Rooms)
        {
            Vector2 position = Position(room,area,minX,maxY);
            foreach (int neighbor in room.Neighbors)
            {
                if (neighbor <= room.Id) continue;
                Handles.color = new Color(.5f,.6f,.7f);
                Handles.DrawLine(position,Position(preview.Rooms[neighbor],area,minX,maxY));
            }
        }
        foreach (DistrictRoom room in preview.Rooms)
        {
            Vector2 position = Position(room,area,minX,maxY);
            Color color = room.Purpose == RoomPurpose.Entrance ? new Color(.2f,.55f,.85f) : room.Purpose == RoomPurpose.Exit ? new Color(.8f,.25f,.3f) :
                room.Purpose == RoomPurpose.Refuge ? new Color(.2f,.6f,.4f) : room.Purpose == RoomPurpose.Treasure ? new Color(.75f,.55f,.2f) : new Color(.5f,.25f,.4f);
            Rect box = new Rect(position.x-44,position.y-25,88,50); EditorGUI.DrawRect(box,color);
            GUI.Label(box,room.Id.ToString("00")+" "+DistrictRuntime.PurposeName(room.Purpose)+"\n"+(room.Has(DoorSide.Up)?"Torre":"Pasarelas"),EditorStyles.centeredGreyMiniLabel);
        }
        EditorGUILayout.EndScrollView();
    }
    private static Vector2 Position(DistrictRoom room,Rect area,int minX,int maxY)
    { return new Vector2(area.x+60+(room.Cell.X-minX)*110,area.y+40+(maxY-room.Cell.Y)*85); }
    private void ApplyToScene()
    {
        GyaruDemoBootstrap demo = UnityEngine.Object.FindObjectOfType<GyaruDemoBootstrap>();
        if (demo == null) { EditorUtility.DisplayDialog("Generador","Abre TutorialRoom o DistritoProcedural primero.","Aceptar"); return; }
        Undo.RecordObject(demo,"Aplicar semilla del distrito");
        SerializedObject settings = new SerializedObject(demo);
        settings.FindProperty("useRandomSeed").boolValue = false;
        settings.FindProperty("fixedSeed").intValue = seed;
        settings.FindProperty("mainPathColumns").intValue = columns;
        settings.FindProperty("optionalBranches").intValue = branches;
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(demo);
        Debug.Log("Semilla " + seed + " aplicada. Guarda la escena; se usara en el siguiente inicio de partida.",demo);
    }
}
