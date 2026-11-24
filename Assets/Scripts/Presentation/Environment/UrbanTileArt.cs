using System.Collections.Generic;
using UnityEngine;

// Dibuja el atlas a 32 px por unidad. Los colliders los construye cada escena.
public sealed class UrbanTileArt : MonoBehaviour
{
    private const int TilePixels = 32;
    // Índice por lados expuestos: arriba=1, derecha=2, abajo=4, izquierda=8.
    private static readonly int[] EdgeTiles = { 37, 2, 11, 3, 18, 26, 19, 27, 9, 1, 12, 4, 17, 25, 20, 28 };
    private readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
    private Texture2D atlas;
    private Material material;

    private void Awake()
    {
        Sprite source = Resources.Load<Sprite>("UrbanArt/1 Tiles/Tileset");
        if (source == null)
        {
            Debug.LogError("Falta el atlas urbano en Resources/UrbanArt/1 Tiles.", this);
            return;
        }
        atlas = source.texture;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;
        material = new Material(shader);
        material.SetTexture("_MainTex", atlas);
    }

    public void DrawSolidTiles(Transform parent, RectInt bounds, HashSet<Vector2Int> solids,
        Vector2 origin, Color tint)
    {
        if (atlas == null)
            return;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            int x = bounds.xMin;
            while (x < bounds.xMax)
            {
                var cell = new Vector2Int(x, y);
                if (!solids.Contains(cell))
                {
                    x++;
                    continue;
                }
                int tile = SelectTile(solids, cell);
                int start = x++;
                while (x < bounds.xMax)
                {
                    cell.x = x;
                    if (!solids.Contains(cell) || SelectTile(solids, cell) != tile)
                        break;
                    x++;
                }
                DrawStrip(parent, "Mampostería", tile, origin + new Vector2(start, y),
                    new Vector2(x - start, 1), -5, tint);
            }
        }
    }

    private static int SelectTile(HashSet<Vector2Int> solids, Vector2Int cell)
    {
        int exposed = 0;
        if (!solids.Contains(cell + Vector2Int.up))
            exposed |= 1;
        if (!solids.Contains(cell + Vector2Int.right))
            exposed |= 2;
        if (!solids.Contains(cell + Vector2Int.down))
            exposed |= 4;
        if (!solids.Contains(cell + Vector2Int.left))
            exposed |= 8;
        if (exposed != 0)
            return EdgeTiles[exposed];

        // Esquinas cóncavas en cambios de nivel y uniones de muro con suelo.
        if (!solids.Contains(cell + new Vector2Int(1, 1)))
            return 13;
        if (!solids.Contains(cell + new Vector2Int(-1, 1)))
            return 14;
        if (!solids.Contains(cell + new Vector2Int(1, -1)))
            return 5;
        if (!solids.Contains(cell + new Vector2Int(-1, -1)))
            return 6;
        return EdgeTiles[0];
    }

    public void DrawPlatform(Transform parent, Vector2 topLeft, int columns, Color tint)
    {
        // Usamos el perfil superior y el canto inferior del atlas a tamaño nativo.
        const int topPixels = 12;
        const int bottomPixels = 4;
        float topHeight = topPixels / (float)TilePixels;
        float bottomHeight = bottomPixels / (float)TilePixels;
        for (int i = 0; i < columns;)
        {
            int tile = columns == 1 ? 28 : i == 0 ? 25 : i == columns - 1 ? 27 : 26;
            int width = tile == 26 ? columns - 2 : 1;
            Vector2 top = topLeft + new Vector2(i, -topHeight);
            DrawStrip(parent, "Pasarela_Perfil", tile, top,
                new Vector2(width, topHeight), -5, tint, TilePixels - topPixels, topPixels);
            DrawStrip(parent, "Pasarela_Canto", tile, top - Vector2.up * bottomHeight,
                new Vector2(width, bottomHeight), -5, tint, 0, bottomPixels);
            i += width;
        }
    }

    public void DrawServiceBay(Transform parent, Vector2 bottomLeft, int width, int height, int variant, Color tint)
    {
        int interior = width - 2;
        DrawStrip(parent, "Panel_Chapa", 55, bottomLeft + Vector2.right,
            new Vector2(interior, height - 1), -55, tint);
        DrawStrip(parent, "Panel_Zocalo", 61, bottomLeft + Vector2.right,
            new Vector2(interior, 1), -54, tint);
        DrawStrip(parent, "Panel_Canaleta", 54, bottomLeft, new Vector2(1, height), -54, tint);
        DrawStrip(parent, "Panel_Canaleta", 62, bottomLeft + Vector2.right * (width - 1),
            new Vector2(1, height), -54, tint);
        DrawStrip(parent, "Panel_Dintel", 51, bottomLeft + new Vector2(1, height - 1),
            new Vector2(interior, 1), -54, tint);

        for (int row = 3; row < height - 2; row += 3)
        {
            DrawStrip(parent, "Panel_Travesaño", 61, bottomLeft + new Vector2(1, row),
                new Vector2(interior, 1), -54, tint);
        }

        int[] fittings = { 52, 53, 60, 57 };
        DrawStrip(parent, "Panel_Registro", fittings[variant % fittings.Length],
            bottomLeft + new Vector2(1, 1), Vector2.one, -53, tint);
        int[] vents = { 56, 58, 59, 64 };
        DrawStrip(parent, "Panel_Ventilacion", vents[variant % vents.Length],
            bottomLeft + new Vector2(width - 2, height - 2), Vector2.one, -53, tint);
    }

    public void DrawConduit(Transform parent, Vector2 bottomLeft, int width, Color tint)
    {
        DrawStrip(parent, "Canaleta_Horizontal", 63, bottomLeft, new Vector2(width, 1), -56, tint);
    }

    private void DrawStrip(Transform parent, string title, int tile, Vector2 bottomLeft,
        Vector2 size, int order, Color tint, int sliceY = 0, int sliceHeight = TilePixels)
    {
        if (atlas == null)
            return;
        int key = tile * 1024 + sliceY * TilePixels + sliceHeight - 1;
        if (!sprites.TryGetValue(key, out Sprite sprite))
        {
            int atlasX = (tile - 1) % 8 * TilePixels;
            int atlasY = atlas.height - ((tile - 1) / 8 + 1) * TilePixels + sliceY;
            sprite = Sprite.Create(atlas, new Rect(atlasX, atlasY, TilePixels, sliceHeight),
                new Vector2(.5f, .5f), TilePixels, 0, SpriteMeshType.FullRect);
            sprites.Add(key, sprite);
        }

        GameObject strip = new GameObject(title);
        strip.transform.SetParent(parent, false);
        strip.transform.position = bottomLeft + size * .5f;
        SpriteRenderer renderer = strip.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.color = tint;
        if (material != null)
            renderer.sharedMaterial = material;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = size;
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in sprites.Values)
            Destroy(sprite);
        if (material != null)
            Destroy(material);
    }
}
