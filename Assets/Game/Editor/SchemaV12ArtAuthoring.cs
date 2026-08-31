#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FortressFrontier.Editor
{
    internal static class SchemaV12ArtAuthoring
    {
        private const string ArtRoot = "Assets/Game/Art/Formal/PNG/SchemaV12";

        [MenuItem("Fortress Frontier/Schema v12/Import And Register Formal Sprites")]
        public static void ImportAndRegister()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in Directory.GetFiles(ArtRoot, "*.png", SearchOption.TopDirectoryOnly))
                ConfigureImporter(path.Replace('\\', '/'));

            PresentationResourceAuthoring.Configure();
            VerticalSliceAuthoring.BuildWorldPrefabs();
            SchemaV12UiAuthoring.Rebuild();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Schema v12 formal sprites imported, registered, and rebound to world/UI prefabs.");
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException($"TextureImporter missing for {path}.");
            var isMap = Path.GetFileName(path).StartsWith("map_", StringComparison.Ordinal);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = !isMap;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
#endif
