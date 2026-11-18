// How the models' textures are imported. The models (Resources/Models/*.gltf) keep their textures
// as ordinary image files beside them (Resources/Models/<model>/), which glTFast picks up from the
// project, so each one can be set per platform:
//
//   everywhere      exactly what the models had when the textures were packed inside the .glb:
//                   full size, uncompressed, mipmapped, colour in sRGB and data (normal, metal-
//                   roughness, occlusion: files ending "_lin") linear
//   the web build   capped at 1024 and ASTC-compressed, so the phone version is small enough to
//                   host and to hold in a phone browser's memory. Nothing else is touched by it.
using UnityEditor;

class ModelTextureImport : AssetPostprocessor
{
    const string Root = "Assets/Resources/Models/";
    const int WebMax = 1024;

    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root) || assetPath.IndexOf('/', Root.Length) < 0) return;   // only the models' own folders
        var ti = (TextureImporter)assetImporter;
        bool linear = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_lin");
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = !linear;
        ti.mipmapEnabled = true;
        ti.isReadable = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
        ti.maxTextureSize = 8192;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "WebGL",
            overridden = true,
            maxTextureSize = WebMax,
            format = TextureImporterFormat.ASTC_8x8,
            compressionQuality = 50,
        });
    }
}
