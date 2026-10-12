using UnityEditor;

/// <summary>Картинки ингредиентов, блюд, фона и интерфейса всегда импортируются как спрайты с одинаковыми настройками.</summary>
public sealed class SpriteImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool icon = assetPath.Contains("/Resources/Ingredients/") || assetPath.Contains("/Resources/Dishes/");
        bool background = assetPath.Contains("/Resources/Kitchen/");
        bool ui = assetPath.Contains("/Resources/UI/");
        var importer = (TextureImporter)assetImporter;
        if (!icon && !background && !ui) return;

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
            // Интерфейс (лист заказа 600×680 и т. п.) — до 1024, чтобы не терять чёткость.
            importer.maxTextureSize = ui ? 1024 : 512;
        }
    }
}
