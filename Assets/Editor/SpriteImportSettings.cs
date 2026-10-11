using UnityEditor;

/// <summary>Картинки ингредиентов и блюд всегда импортируются как спрайты с одинаковыми настройками.</summary>
public sealed class SpriteImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/Ingredients/") && !assetPath.Contains("/Resources/Dishes/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // Мипмапы — чтобы крутящиеся ингредиенты не рябили при уменьшении.
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
