using UnityEngine;
using UnityEditor;

public sealed class KenneyUIImport : AssetPostprocessor
{
    public override uint GetVersion()
    {
        return 2;
    }
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/KenneyUI/"))
            return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = assetPath.Contains("/Lights/") ? FilterMode.Bilinear : FilterMode.Point;
        importer.alphaSource = assetPath.Contains("/Lights/") ? TextureImporterAlphaSource.FromGrayScale : TextureImporterAlphaSource.FromInput;
        importer.spriteBorder = assetPath.Contains("/Frames/") && !assetPath.EndsWith("Divider.png") ? new Vector4(14, 14, 14, 14) : Vector4.zero;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
