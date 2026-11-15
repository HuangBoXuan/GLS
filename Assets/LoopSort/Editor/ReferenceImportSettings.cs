using UnityEditor;
using UnityEngine;

namespace LoopSort0.Editor
{
    public sealed class ReferenceImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(assetPath=="Assets/LoopSort/Resources/LoopSort/Theme/tutorialHand.png")
            {
                var texture=(TextureImporter)assetImporter;texture.textureType=TextureImporterType.Default;
                texture.npotScale=TextureImporterNPOTScale.None;texture.filterMode=FilterMode.Bilinear;texture.sRGBTexture=true;
                texture.mipmapEnabled=false;texture.alphaIsTransparency=true;texture.wrapMode=TextureWrapMode.Clamp;
                texture.textureCompression=TextureImporterCompression.Uncompressed;texture.maxTextureSize=512;return;
            }
            if(assetPath.StartsWith("Assets/LoopSort/Resources/LoopSort/Theme/"))
            {
                var theme=(TextureImporter)assetImporter;theme.textureType=TextureImporterType.Default;
                theme.npotScale=TextureImporterNPOTScale.None;theme.mipmapEnabled=false;theme.sRGBTexture=true;
                theme.alphaIsTransparency=!assetPath.EndsWith("worlds.png");theme.isReadable=false;
                theme.filterMode=FilterMode.Bilinear;theme.wrapMode=TextureWrapMode.Clamp;
                bool adIcon=assetPath.EndsWith("/advert-yellow.png")||assetPath.EndsWith("/advert-clapper.png");
                bool buttonAtlas=assetPath.EndsWith("/candy-buttons.png");
                theme.textureCompression=adIcon||buttonAtlas?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;
                theme.maxTextureSize=adIcon?256:buttonAtlas?1024:2048;return;
            }
            if(assetPath.StartsWith("Assets/LoopSort/Resources/LoopSort/Textures/"))
            {
                var texture=(TextureImporter)assetImporter;texture.textureType=TextureImporterType.Default;
                texture.wrapMode=assetPath.Contains("/feature_")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
                texture.mipmapEnabled=true;texture.textureCompression=TextureImporterCompression.Uncompressed;return;
            }
            if(!assetPath.StartsWith("Assets/LoopSort/Resources/LoopSort/UI/"))return;
            var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
        }
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/LoopSort/Resources/LoopSort/Models/"))return;
            var importer=(ModelImporter)assetImporter;importer.globalScale=1;importer.isReadable=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;
        }
        void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/LoopSort/Resources/LoopSort/Audio/"))return;
            var importer=(AudioImporter)assetImporter;var settings=importer.defaultSampleSettings;
            settings.loadType=assetPath.Contains("music")?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.65f;importer.defaultSampleSettings=settings;
        }
    }
}
