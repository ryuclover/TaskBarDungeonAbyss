using UnityEditor;
using UnityEngine;

namespace TBDA.Editor
{
    /// <summary>
    /// Automatically configures all pixel art under Assets/_Project/Art/ with Nintendo DS-faithful
    /// Point filtering, Uncompressed RGBA32 palette fidelity, FullRect mesh, and 16 PPU.
    /// </summary>
    public sealed class PixelArtImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            if (assetPath.Contains("dungeon_strip_bg"))
            {
                importer.wrapMode = TextureWrapMode.Repeat;
            }
            else
            {
                importer.wrapMode = TextureWrapMode.Clamp;
            }
        }
    }
}
