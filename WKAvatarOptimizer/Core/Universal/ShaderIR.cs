using System;
using System.Collections.Generic;
using UnityEngine;

namespace WKAvatarOptimizer.Core.Universal
{
    [Serializable]
    public class ShaderIR
    {
        public string Name;
        public string MaterialName;

        public enum ShadingModel { Unlit, Toon, PBR, Hybrid }
        public enum BlendMode { Opaque, Cutout, Alpha, Additive, Premultiplied }
        public enum CullMode { Back, Front, Off }

        public ShadingModel shadingModel = ShadingModel.Toon;
        public BlendMode blendMode = BlendMode.Opaque;
        public CullMode cullMode = CullMode.Back;
        public int renderQueue = 2000;
        public bool ignoreProjector = false;

        public TextureProperty baseColor = new TextureProperty();
        public TextureProperty normalMap = new TextureProperty();
        public float normalScale = 1f;
        
        public TextureProperty metallicGlossMap = new TextureProperty();
        public float metallicStrength = 0f;
        public float smoothness = 0.5f;

        public TextureProperty shadeMap = new TextureProperty();
        public Color shadeColor = Color.gray;
        public TextureProperty rampTexture = new TextureProperty();
        public float shadowThreshold = 0.5f;
        public float shadowSmooth = 0.1f;

        public TextureProperty matcapTexture = new TextureProperty();
        public Color matcapColor = Color.white;
        public bool useMatcapSecond = false;
        public TextureProperty matcapTexture2 = new TextureProperty();

        public Color rimColor = Color.white;
        public float rimPower = 1f;
        public float rimIntensity = 1f;
        public bool rimAffectsAlbedo = false;

        public bool useOutline = false;
        public Color outlineColor = Color.black;
        public float outlineWidth = 0.01f;
        public TextureProperty outlineMask = new TextureProperty();
        public bool outlineScreenSpace = true;

        public Color emissionColor = Color.black;
        public TextureProperty emissionMap = new TextureProperty();
        public float emissionIntensity = 1f;

        public TextureProperty dissolveMask = new TextureProperty();
        public float dissolveAmount = 0f;

        public TextureProperty detailMap = new TextureProperty();
        public float detailScale = 1f;

        public List<CustomNode> customNodes = new List<CustomNode>();

        public void ApplyToMaterial(Material targetMaterial, Shader universalShader)
        {
            if (targetMaterial == null) throw new ArgumentNullException(nameof(targetMaterial));
            if (universalShader == null) throw new ArgumentNullException(nameof(universalShader));

            targetMaterial.shader = universalShader;

            SetKeywords(targetMaterial);

            SetRenderState(targetMaterial);

            AssignProperties(targetMaterial);
        }

        private void SetKeywords(Material mat)
        {
            mat.DisableKeyword("_SHADING_MODE_UNLIT");
            mat.DisableKeyword("_SHADING_MODE_TOON");
            mat.DisableKeyword("_SHADING_MODE_PBR");
            switch (shadingModel)
            {
                case ShadingModel.Unlit: mat.EnableKeyword("_SHADING_MODE_UNLIT"); break;
                case ShadingModel.Toon: mat.EnableKeyword("_SHADING_MODE_TOON"); break;
                case ShadingModel.PBR: mat.EnableKeyword("_SHADING_MODE_PBR"); break;
                case ShadingModel.Hybrid:
                    mat.EnableKeyword("_SHADING_MODE_TOON"); 
                    mat.EnableKeyword("_SHADING_MODE_PBR");
                    break;
            }

            SetKeyword(mat, "_USE_NORMAL_MAP", normalMap.texture != null);
            SetKeyword(mat, "_USE_METALLIC_GLOSS_MAP", metallicGlossMap.texture != null);
            SetKeyword(mat, "_USE_SHADE_MAP", shadeMap.texture != null);
            SetKeyword(mat, "_USE_RAMP_TEXTURE", rampTexture.texture != null);
            SetKeyword(mat, "_USE_MATCAP_TEXTURE", matcapTexture.texture != null);
            SetKeyword(mat, "_USE_MATCAP_SECOND", useMatcapSecond);
            SetKeyword(mat, "_USE_RIM_LIGHTING", rimIntensity > 0 || rimColor != Color.white);
            SetKeyword(mat, "_USE_OUTLINE", useOutline);
            SetKeyword(mat, "_USE_EMISSION", emissionMap.texture != null || emissionColor != Color.black);
            SetKeyword(mat, "_USE_DISSOLVE", dissolveMask.texture != null);
            SetKeyword(mat, "_USE_DETAIL_MAP", detailMap.texture != null);
            SetKeyword(mat, "_DOUBLE_SIDED", cullMode == CullMode.Off);


            mat.DisableKeyword("_TRANSPARENCY_OFF");
            mat.DisableKeyword("_TRANSPARENCY_CUTOUT");
            mat.DisableKeyword("_TRANSPARENCY_ALPHA");
            mat.DisableKeyword("_TRANSPARENCY_ADDITIVE");
            mat.DisableKeyword("_TRANSPARENCY_PREMUL");
            switch (blendMode)
            {
                case BlendMode.Opaque: mat.EnableKeyword("_TRANSPARENCY_OFF"); break;
                case BlendMode.Cutout: mat.EnableKeyword("_TRANSPARENCY_CUTOUT"); break;
                case BlendMode.Alpha: mat.EnableKeyword("_TRANSPARENCY_ALPHA"); break;
                case BlendMode.Additive: mat.EnableKeyword("_TRANSPARENCY_ADDITIVE"); break;
                case BlendMode.Premultiplied: mat.EnableKeyword("_TRANSPARENCY_PREMUL"); break;
            }
        }

        private void SetKeyword(Material mat, string keyword, bool enable)
        {
            if (enable) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        private void SetRenderState(Material mat)
        {
            switch (cullMode)
            {
                case CullMode.Back: mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back); break;
                case CullMode.Front: mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front); break;
                case CullMode.Off: mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); break;
            }

            switch (blendMode)
            {
                case BlendMode.Opaque:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    break;
                case BlendMode.Cutout:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    break;
                case BlendMode.Alpha:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
                case BlendMode.Additive:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
                case BlendMode.Premultiplied:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }

            if (renderQueue != 2000)
            {
                mat.renderQueue = renderQueue;
            }
            mat.SetInt("_IgnoreProjector", ignoreProjector ? 1 : 0);
        }

        private void AssignProperties(Material mat)
        {
            mat.SetColor("_BaseColor", baseColor.color);
            mat.SetColor("_ShadeColor", shadeColor);
            mat.SetColor("_RimColor", rimColor);
            mat.SetColor("_OutlineColor", outlineColor);
            mat.SetColor("_EmissionColor", emissionColor);
            mat.SetColor("_MatcapColor", matcapColor);

            SetTexture(mat, "_BaseMap", baseColor.texture);
            SetTexture(mat, "_NormalMap", normalMap.texture);
            SetTexture(mat, "_MetallicGlossMap", metallicGlossMap.texture);
            SetTexture(mat, "_ShadeMap", shadeMap.texture);
            SetTexture(mat, "_RampTexture", rampTexture.texture);
            SetTexture(mat, "_MatcapTexture", matcapTexture.texture);
            SetTexture(mat, "_MatcapTexture2", matcapTexture2.texture);
            SetTexture(mat, "_OutlineMask", outlineMask.texture);
            SetTexture(mat, "_EmissionMap", emissionMap.texture);
            SetTexture(mat, "_DissolveMask", dissolveMask.texture);
            SetTexture(mat, "_DetailMap", detailMap.texture);

            mat.SetFloat("_NormalScale", normalScale);
            mat.SetFloat("_MetallicStrength", metallicStrength);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_RimPower", rimPower);
            mat.SetFloat("_RimIntensity", rimIntensity);
            mat.SetFloat("_OutlineWidth", outlineWidth);
            mat.SetFloat("_EmissionIntensity", emissionIntensity);
            mat.SetFloat("_DissolveAmount", dissolveAmount);
            mat.SetInt("_OutlineScreenSpace", outlineScreenSpace ? 1 : 0);
            mat.SetFloat("_ShadowThreshold", shadowThreshold);
            mat.SetFloat("_ShadowSmooth", shadowSmooth);
            mat.SetFloat("_DetailScale", detailScale);

        }

        private void SetTexture(Material mat, string propertyName, Texture2D texture)
        {
            if (texture != null)
            {
                mat.SetTexture(propertyName, texture);
            }
            else
            {
                mat.SetTexture(propertyName, null);
            }
        }
    }

    [Serializable]
    public class TextureProperty
    {
        public Texture2D texture;
        public Vector2 scale = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public Color color = Color.white;
    }

    [Serializable]
    public class CustomNode
    {
        public string name;
        public string category;
        public string description;
        public string suggestion;
        public Dictionary<string, string> parameters = new Dictionary<string, string>();
    }
}
