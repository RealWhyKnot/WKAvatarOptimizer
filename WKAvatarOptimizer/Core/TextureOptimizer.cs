using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WKAvatarOptimizer.Core
{
    public class TextureOptimizer
    {
        private readonly OptimizationContext context;
        private readonly GameObject gameObject;

        public TextureOptimizer(OptimizationContext context, GameObject gameObject)
        {
            this.context = context;
            this.gameObject = gameObject;
        }

        public void OptimizeTextures()
        {
            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct();
            var textures = new HashSet<Texture2D>();

            foreach (var mat in materials)
            {
                var shader = mat.shader;
                if (shader == null) continue;
                
                int propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < propertyCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string propName = ShaderUtil.GetPropertyName(shader, i);
                        var tex = mat.GetTexture(propName) as Texture2D;
                        if (tex != null)
                        {
                            textures.Add(tex);
                        }
                    }
                }
            }

            int count = 0;
            foreach (var tex in textures)
            {
                count++;
                OptimizeTexture(tex, count, textures.Count);
            }
        }

        private void OptimizeTexture(Texture2D tex, int index, int total)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            List<string> changes = new List<string>();
            context.Log($"[TextureOptimizer] Processing texture {index}/{total}: {tex.name}. Original mipmapEnabled: {importer.mipmapEnabled}, mipmapCount: {tex.mipmapCount}, format: {tex.format}");

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
                changes.Add("Enabled Mipmaps");
            }

            if (importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter)
            {
                importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                changed = true;
                changes.Add("Set Kaiser Filter");
            }

            var platformSettings = importer.GetPlatformTextureSettings("Standalone");
            var originalSettings = platformSettings;

            platformSettings.overridden = true;
            platformSettings.compressionQuality = 100; 

            if (importer.textureType == TextureImporterType.NormalMap)
            {
                if (platformSettings.format != TextureImporterFormat.BC5)
                {
                    platformSettings.format = TextureImporterFormat.BC5;
                    changed = true;
                    changes.Add("Format->BC5");
                }
            }
            else if (importer.DoesSourceTextureHaveAlpha())
            {
                if (platformSettings.format != TextureImporterFormat.DXT5)
                {
                    platformSettings.format = TextureImporterFormat.DXT5;
                    platformSettings.crunchedCompression = false;
                    changed = true;
                    changes.Add("Format->DXT5");
                }
            }
            else
            {
                if (platformSettings.format != TextureImporterFormat.DXT1)
                {
                    platformSettings.format = TextureImporterFormat.DXT1;
                    platformSettings.crunchedCompression = false;
                    changed = true;
                    changes.Add("Format->DXT1");
                }
            }

            if (changed)
            {
                context.Log($"Optimizing texture {index}/{total}: {tex.name} ({string.Join(", ", changes)})");
                importer.SetPlatformTextureSettings(platformSettings);
                importer.SaveAndReimport();
            }
        }
    }
}
