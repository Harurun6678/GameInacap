using UnityEditor;
using UnityEngine;

// Normaliza la importacion del arte incluido en la demo.
public class GyaruArtImport : AssetPostprocessor
{
    // Subir este numero obliga a Unity a reimportar el arte con los ajustes actuales.
    public override uint GetVersion() { return 2; }

    private static bool IsDemoArt(string path)
    {
        return path.StartsWith("Assets/Resources/UrbanArt/") ||
               path.StartsWith("Assets/Resources/DemoArt/");
    }

    private void OnPreprocessTexture()
    {
        if (!IsDemoArt(assetPath)) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        if (assetPath.StartsWith("Assets/Resources/UrbanArt/"))
        {
            importer.filterMode = FilterMode.Point;
            // 32 px por unidad: baldosa = 1 unidad, capas de 512 px = 16 unidades.
            importer.spritePixelsPerUnit = 32;
        }
        TextureImporterPlatformSettings platform = importer.GetPlatformTextureSettings("Standalone");
        platform.overridden = false;
        importer.SetPlatformTextureSettings(platform);
    }

    [MenuItem("Gyaru Rogue/Reimportar arte del tutorial")]
    public static void ReimportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UrbanArt", "Assets/Resources/DemoArt" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("Arte del tutorial reimportado. Abre TutorialRoom y pulsa Play.");
    }
}
