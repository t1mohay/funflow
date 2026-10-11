using UnityEditor;

/// <summary>Картинки ингредиентов, блюд, фона и интерфейса всегда импортируются как спрайты с одинаковыми настройками.</summary>
public sealed class SpriteImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool icon = assetPath.Contains("/Resources/Ingredients/") || assetPath.Contains("/Resources/Dishes/");
        bool background = assetPath.Contains("/Resources/Kitchen/");
        bool ui = assetPath.Contains("/Resources/UI/");
        if (!icon && !background && !ui) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (background)
        {
            // Фон во весь экран: полное разрешение, мипмапы не нужны.
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
        }
        else
        {
            // Мипмапы — чтобы крутящиеся ингредиенты не рябили при уменьшении.
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
        }
    }
}
