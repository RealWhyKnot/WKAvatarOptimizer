Shader "whyknot/Optimizer/s_Poiyomi Pro Scarf f4a79896" // Hidden/Locked/.poiyomi/Poiyomi Pro/825a83ee2565ac34ebbe323a7ec22848
{
Properties
{
_Mode("_Mode", Int) = 0
_Color("_Color", Color) = (1, 1, 1, 1)
_MainTex("_MainTex", 2D) = "white" {}
_BumpMap("_BumpMap", 2D) = "bump" {}
_BumpScale("_BumpScale", Range(0, 10)) = 1
_AlphaMask("_AlphaMask", 2D) = "white" {}
_Cutoff("_Cutoff", Range(0, 1.001)) = 0.5
_LightingAOMaps("_LightingAOMaps", 2D) = "white" {}
_LightingDetailShadowMaps("_LightingDetailShadowMaps", 2D) = "white" {}
_LightingShadowMasks("_LightingShadowMasks", 2D) = "white" {}
_LightDataSDFMap("_LightDataSDFMap", 2D) = "white" {}
_LightingCap("_LightingCap", Range(0, 10)) = 1
_LightingMinLightBrightness("_LightingMinLightBrightness", Range(0, 1)) = 0
_LightingMonochromatic("_LightingMonochromatic", Range(0, 1)) = 0
_LightingEnableLightVolumes("_LightingEnableLightVolumes", Float) = 1
_LightingAdditiveLimit("_LightingAdditiveLimit", Range(0, 10)) = 1
_LightingAdditiveMonochromatic("_LightingAdditiveMonochromatic", Range(0, 1)) = 0
_Matcap("_Matcap", 2D) = "white" {}
_MatcapMask("_MatcapMask", 2D) = "white" {}
_Matcap0NormalMap("_Matcap0NormalMap", 2D) = "bump" {}
_Set_RimLightMask("_Set_RimLightMask", 2D) = "white" {}
_RimMask("_RimMask", 2D) = "white" {}
_RimTex("_RimTex", 2D) = "white" {}
_RimColorTex("_RimColorTex", 2D) = "white" {}
_MochieMetallicMaps("_MochieMetallicMaps", 2D) = "white" {}
_MochieReflCube("_MochieReflCube", Cube) = "" {}
_AnisotropyMap("_AnisotropyMap", 2D) = "bump" {}
_SSAOAnimationToggle("_SSAOAnimationToggle", Float) = 1
_SSAOIntensity("_SSAOIntensity", Range(0, 5)) = 1.0
_SSAOColorMap("_SSAOColorMap", 2D) = "white" {}
_SSAOMask("_SSAOMask", 2D) = "white" {}
_UzumoreMask("_UzumoreMask", 2D) = "white" {}
_ZTest("_ZTest", Float) = 4
_ZWrite("_ZWrite", Int) = 1
_OffsetFactor("_OffsetFactor", Float) = 0.0
_OffsetUnits("_OffsetUnits", Float) = 0.0
_BlendOp("_BlendOp", Int) = 0
_SrcBlend("_SrcBlend", Int) = 1
_DstBlend("_DstBlend", Int) = 0
_AddBlendOp("_AddBlendOp", Int) = 4
_AddSrcBlend("_AddSrcBlend", Int) = 1
_AddDstBlend("_AddDstBlend", Int) = 1
_BlendOpAlpha("_BlendOpAlpha", Int) = 0
_SrcBlendAlpha("_SrcBlendAlpha", Int) = 1
_DstBlendAlpha("_DstBlendAlpha", Int) = 10
_AddBlendOpAlpha("_AddBlendOpAlpha", Int) = 4
_AddSrcBlendAlpha("_AddSrcBlendAlpha", Int) = 0
_AddDstBlendAlpha("_AddDstBlendAlpha", Int) = 1
_StencilRef("_StencilRef", Range(0, 255)) = 0
_StencilReadMask("_StencilReadMask", Range(0, 255)) = 255
_StencilWriteMask("_StencilWriteMask", Range(0, 255)) = 255
_StencilPassOp("_StencilPassOp", Float) = 0
_StencilFailOp("_StencilFailOp", Float) = 0
_StencilZFailOp("_StencilZFailOp", Float) = 0
_StencilCompareFunction("_StencilCompareFunction", Float) = 8
}
SubShader
{
Tags
{
"RenderType" = "Opaque" "Queue" = "Geometry" "VRCFallback" = "Standard"
}
pass//0
{
Tags
{
"LightMode" = "ForwardBase"
}
Stencil
{
Ref [_StencilRef]
ReadMask [_StencilReadMask]
WriteMask [_StencilWriteMask]
Comp [_StencilCompareFunction]
Pass [_StencilPassOp]
Fail [_StencilFailOp]
ZFail [_StencilZFailOp]
}
ZWrite [_ZWrite]
Cull Off
ZTest [_ZTest]
ColorMask RGBA
Offset [_OffsetFactor], [_OffsetUnits]
BlendOp [_BlendOp], [_BlendOpAlpha]
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
CGPROGRAM
#pragma skip_variants DYNAMICLIGHTMAP_ON LIGHTMAP_ON LIGHTMAP_SHADOW_MIXING DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK
#pragma warning (disable : 3557) // loop only executes for 1 iteration(s), forcing loop to unroll
#pragma warning (disable : 4008) // A floating point division by zero occurred.
#pragma warning (disable : 3554) // The attribute is unknown or invalid for the specified statement.
#pragma target 5.0
#pragma multi_compile DIRECTIONAL
#pragma multi_compile LIGHTPROBE_SH
#pragma multi_compile _ SHADOWS_SCREEN
#pragma multi_compile_instancing
#pragma multi_compile_vertex _ FOG_EXP2
#pragma multi_compile_fragment _ VERTEXLIGHT_ON
#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#pragma skip_variants _SCREEN_SPACE_OCCLUSION
#pragma vertex vert
#pragma fragment frag
#include "s_Poiyomi Pro Scarf_6a502364.cginc"
ENDCG
}
pass//1
{
Tags
{
"LightMode" = "ForwardAdd"
}
Stencil
{
Ref [_StencilRef]
ReadMask [_StencilReadMask]
WriteMask [_StencilWriteMask]
Comp [_StencilCompareFunction]
Pass [_StencilPassOp]
Fail [_StencilFailOp]
ZFail [_StencilZFailOp]
}
ZWrite Off
Cull Off
ZTest [_ZTest]
ColorMask RGBA
Offset [_OffsetFactor], [_OffsetUnits]
BlendOp [_AddBlendOp], [_AddBlendOpAlpha]
Blend [_AddSrcBlend] [_AddDstBlend], [_AddSrcBlendAlpha] [_AddDstBlendAlpha]
CGPROGRAM
#pragma skip_variants DYNAMICLIGHTMAP_ON LIGHTMAP_ON LIGHTMAP_SHADOW_MIXING DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK
#pragma warning (disable : 3557) // loop only executes for 1 iteration(s), forcing loop to unroll
#pragma warning (disable : 4008) // A floating point division by zero occurred.
#pragma warning (disable : 3554) // The attribute is unknown or invalid for the specified statement.
#pragma target 5.0
#pragma multi_compile_fwdadd_fullshadows
#pragma multi_compile_instancing
#pragma multi_compile_vertex _ FOG_EXP2
#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#pragma skip_variants _SCREEN_SPACE_OCCLUSION
#pragma vertex vert
#pragma fragment frag
#include "s_Poiyomi Pro Scarf_56201e6a.cginc"
ENDCG
}
pass//2
{
Tags
{
"LightMode" = "ShadowCaster"
}
Stencil
{
Ref [_StencilRef]
ReadMask [_StencilReadMask]
WriteMask [_StencilWriteMask]
Comp [_StencilCompareFunction]
Pass [_StencilPassOp]
Fail [_StencilFailOp]
ZFail [_StencilZFailOp]
}
ZWrite [_ZWrite]
Cull Off
AlphaToMask Off
ZTest [_ZTest]
ColorMask RGBA
Offset [_OffsetFactor], [_OffsetUnits]
BlendOp [_BlendOp], [_BlendOpAlpha]
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
CGPROGRAM
#pragma skip_variants DYNAMICLIGHTMAP_ON LIGHTMAP_ON LIGHTMAP_SHADOW_MIXING DIRLIGHTMAP_COMBINED SHADOWS_SHADOWMASK
#pragma warning (disable : 3557) // loop only executes for 1 iteration(s), forcing loop to unroll
#pragma warning (disable : 4008) // A floating point division by zero occurred.
#pragma warning (disable : 3554) // The attribute is unknown or invalid for the specified statement.
#pragma target 5.0
#pragma multi_compile_instancing
#pragma multi_compile_shadowcaster
#pragma multi_compile_vertex _ FOG_EXP2
#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#pragma skip_variants _SCREEN_SPACE_OCCLUSION
#pragma vertex vert
#pragma fragment frag
#include "s_Poiyomi Pro Scarf_56266468.cginc"
ENDCG
}
}
}
