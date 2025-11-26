#pragma warning (disable : 3557) // loop only executes for 1 iteration(s), forcing loop to unroll
#pragma warning (disable : 4008) // A floating point division by zero occurred.
#pragma warning (disable : 3554) // The attribute is unknown or invalid for the specified statement.
cbuffer WKVRCOptimizerAnimatedScalars
{
float _IsActiveMesh32 : packoffset(c0);
float WKVRCOptimizer_LightingCap_ArrayIndex32 : packoffset(c1);
float WKVRCOptimizer_LightingAdditiveLimit_ArrayIndex32 : packoffset(c2);
float WKVRCOptimizer_LightingMinLightBrightness_ArrayIndex32 : packoffset(c3);
float WKVRCOptimizer_LightingMonochromatic_ArrayIndex32 : packoffset(c4);
float WKVRCOptimizer_LightingAdditiveMonochromatic_ArrayIndex32 : packoffset(c5);
float WKVRCOptimizer_SSAOIntensity_ArrayIndex32 : packoffset(c6);
float WKVRCOptimizer_LightingEnableLightVolumes_ArrayIndex32 : packoffset(c7);
float WKVRCOptimizer_SSAOAnimationToggle_ArrayIndex32 : packoffset(c8);
float WKVRCOptimizer_MainHueShift_ArrayIndex32 : packoffset(c9);
float WKVRCOptimizer_Saturation_ArrayIndex32 : packoffset(c10);
};
static float _LightingCap = 1;
static float _LightingAdditiveLimit = 1;
static float _LightingMinLightBrightness = 0;
static float _LightingMonochromatic = 0;
static float _LightingAdditiveMonochromatic = 0;
static float _SSAOIntensity = 0;
static float _LightingEnableLightVolumes = 1;
static float _SSAOAnimationToggle = 1;
static float _MainHueShift = 0;
static float _Saturation = 3.37;
uniform float WKVRCOptimizer_Zero;
static uint WKVRCOptimizer_MaterialID = 0;
static uint WKVRCOptimizer_MeshID = 0;
// Include UnityLightingCommon.cginc
#ifndef UNITY_LIGHTING_COMMON_INCLUDED
#define UNITY_LIGHTING_COMMON_INCLUDED
float4 _LightColor0;
float4 _SpecColor;
struct UnityLight
{
half3 color;
half3 dir;
half ndotl;
}
;
struct UnityIndirect
{
half3 diffuse;
half3 specular;
}
;
struct UnityGI
{
UnityLight light;
UnityIndirect indirect;
}
;
struct UnityGIInput
{
UnityLight light;
float3 worldPos;
half3 worldViewDir;
half atten;
half3 ambient;
float4 lightmapUV;
#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION) || defined(UNITY_ENABLE_REFLECTION_BUFFERS)
float4 boxMin[2];
#endif
#ifdef UNITY_SPECCUBE_BOX_PROJECTION
float4 boxMax[2];
float4 probePosition[2];
#endif
float4 probeHDR[2];
}
;
#endif
#define COLOR_GRADING_HDR
#define MOCHIE_PBR
#define POI_MATCAP0
#define POI_SSAO
#define POI_UZUMORE
#define VIGNETTE_MASKED
#define _GLOSSYREFLECTIONS_OFF
#define _LIGHTINGMODE_FLAT
#define _RIM2STYLE_POIYOMI
#define _RIMSTYLE_POIYOMI
#define _STOCHASTICMODE_DELIOT_HEITZ
#define PROP_BUMPMAP
#define PROP_MATCAP
#define OPTIMIZER_ENABLED
#define POI_PASS_SHADOW
#include "UnityCG.cginc"
#include "AutoLight.cginc"
SamplerState sampler_linear_clamp;
SamplerState sampler_linear_repeat;
SamplerState sampler_trilinear_clamp;
SamplerState sampler_trilinear_repeat;
SamplerState sampler_point_clamp;
SamplerState sampler_point_repeat;
#define DielectricSpec float4(0.04, 0.04, 0.04, 1.0 - 0.04)
#define HALF_PI float(1.5707964)
#define PI float(3.14159265359)
#define TWO_PI float(6.28318530718)
#define PI_OVER_2 1.5707963f
#define PI_OVER_4 0.785398f
#define EPSILON 0.000001f
#define POI2D_SAMPLE_TEX2D_SAMPLERGRAD(tex, samplertex, coord, dx, dy) tex.SampleGrad(sampler##samplertex, coord, dx, dy)
#define POI2D_SAMPLE_TEX2D_SAMPLERGRADD(tex, samp, uv, pan, dx, dy) tex.SampleGrad(samp, POI_PAN_UV(uv, pan), dx, dy)
#define POI_PAN_UV(uv, pan) (uv + _Time.x * pan)
#define POI2D_SAMPLER_PAN(tex, texSampler, uv, pan) (UNITY_SAMPLE_TEX2D_SAMPLER(tex, texSampler, POI_PAN_UV(uv, pan)))
#define POI2D_SAMPLER_PANGRAD(tex, texSampler, uv, pan, dx, dy) (POI2D_SAMPLE_TEX2D_SAMPLERGRAD(tex, texSampler, POI_PAN_UV(uv, pan), dx, dy))
#define POI2D_SAMPLER(tex, texSampler, uv) (UNITY_SAMPLE_TEX2D_SAMPLER(tex, texSampler, uv))
#define POI_SAMPLE_1D_X(tex, samp, uv) tex.Sample(samp, float2(uv, 0.5))
#define POI2D_SAMPLER_GRAD(tex, texSampler, uv, dx, dy) (POI2D_SAMPLE_TEX2D_SAMPLERGRAD(tex, texSampler, uv, dx, dy))
#define POI2D_SAMPLER_GRADD(tex, texSampler, uv, dx, dy) tex.SampleGrad(texSampler, uv, dx, dy)
#define POI2D_PAN(tex, uv, pan) (tex2D(tex, POI_PAN_UV(uv, pan)))
#define POI2D(tex, uv) (tex2D(tex, uv))
#define POI_SAMPLE_TEX2D(tex, uv) (UNITY_SAMPLE_TEX2D(tex, uv))
#define POI_SAMPLE_TEX2D_PAN(tex, uv, pan) (UNITY_SAMPLE_TEX2D(tex, POI_PAN_UV(uv, pan)))
#define POI_SAMPLE_CUBE_LOD(tex, sampler, coord, lod) tex.SampleLevel(sampler, coord, lod)
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#define POI_SAMPLE_SCREEN(tex, samp, uv)          tex.Sample(samp, float3(uv, unity_StereoEyeIndex))
#else
#define POI_SAMPLE_SCREEN(tex, samp, uv)          tex.Sample(samp, uv)
#endif
#define POI_SAFE_RGB0 float4(mainTexture.rgb * .0001, 0)
#define POI_SAFE_RGB1 float4(mainTexture.rgb * .0001, 1)
#define POI_SAFE_RGBA mainTexture
#if defined(UNITY_COMPILER_HLSL)
#define PoiInitStruct(type, name) name = (type)0;
#else
#define PoiInitStruct(type, name)
#endif
#define POI_ERROR(poiMesh, gridSize) lerp(float3(1, 0, 1), float3(0, 0, 0), fmod(floor((poiMesh.worldPos.x) * gridSize) + floor((poiMesh.worldPos.y) * gridSize) + floor((poiMesh.worldPos.z) * gridSize), 2) == 0)
#define POI_NAN (asfloat(-1))
#define POI_MODE_OPAQUE 0
#define POI_MODE_CUTOUT 1
#define POI_MODE_FADE 2
#define POI_MODE_TRANSPARENT 3
#define POI_MODE_ADDITIVE 4
#define POI_MODE_SOFTADDITIVE 5
#define POI_MODE_MULTIPLICATIVE 6
#define POI_MODE_2XMULTIPLICATIVE 7
#define POI_MODE_TRANSCLIPPING 9
#ifndef UNITY_SPECCUBE_LOD_STEPS
#define UNITY_SPECCUBE_LOD_STEPS (6)
#endif
#ifndef UNITY_LIGHTING_COMMON_INCLUDED
#define UNITY_LIGHTING_COMMON_INCLUDED
fixed4 _LightColor0;
fixed4 _SpecColor;
struct UnityLight
{
half3 color;
half3 dir;
half ndotl;
}
;
struct UnityIndirect
{
half3 diffuse;
half3 specular;
}
;
struct UnityGI
{
UnityLight light;
UnityIndirect indirect;
}
;
struct UnityGIInput
{
UnityLight light;
float3 worldPos;
half3 worldViewDir;
half atten;
half3 ambient;
#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION) || defined(UNITY_ENABLE_REFLECTION_BUFFERS)
float4 boxMin[2];
#endif
#ifdef UNITY_SPECCUBE_BOX_PROJECTION
float4 boxMax[2];
float4 probePosition[2];
#endif
float4 probeHDR[2];
}
;
#endif
float _GrabMode;
static float _Mode = 9;
struct Unity_GlossyEnvironmentData
{
half roughness;
half3 reflUVW;
}
;
#ifndef _STOCHASTICMODE_NONE
static float _StochasticDeliotHeitzDensity = 1;
#endif
static float4 _Color = float4(1, 1, 1, 1);
static float _ColorThemeIndex = 0;
#define DUMMY_USE_TEXTURE_TO_PRESERVE_SAMPLER__MainTex
UNITY_DECLARE_TEX2D(_MainTex);
#ifdef UNITY_STEREO_INSTANCING_ENABLED
#define STEREO_UV(uv) float3(uv, unity_StereoEyeIndex)
Texture2DArray<float> _CameraDepthTexture;
#else
#define STEREO_UV(uv) uv
Texture2D<float> _CameraDepthTexture;
#endif
float SampleScreenDepth(float2 uv)
{
uv.y = _ProjectionParams.x * 0.5 + 0.5 - uv.y * _ProjectionParams.x;
return _CameraDepthTexture.SampleLevel(sampler_point_clamp, STEREO_UV(uv), 0);
}
bool DepthTextureExists()
{
#ifdef UNITY_STEREO_INSTANCING_ENABLED
float3 dTexDim;
_CameraDepthTexture.GetDimensions(dTexDim.x, dTexDim.y, dTexDim.z);
#else
float2 dTexDim;
_CameraDepthTexture.GetDimensions(dTexDim.x, dTexDim.y);
#endif
return dTexDim.x > 16;
}
static float _MainPixelMode = 0;
static float4 _MainTex_ST = float4(1, 1, 0, 0);
static float2 _MainTexPan = float4(0, 0, 0, 0);
static float _MainTexUV = 0;
static float4 _MainTex_TexelSize = float4(1.0 / 1024, 1.0 / 1024, 1024, 1024);
static float _MainTexStochastic = 0;
static float _MainIgnoreTexAlpha = 0;
Texture2D _BumpMap;
static float4 _BumpMap_ST = float4(1, 1, 0, 0);
static float2 _BumpMapPan = float4(0, 0, 0, 0);
static float _BumpMapUV = 0;
static float _BumpScale = 1;
static float _BumpMapStochastic = 0;
// Skipped 1 lines | #if defined(PROP_ALPHAMASK) || !defined(OPTIMIZER_ENABLED)
static float4 _AlphaMask_ST = float4(1, 1, 0, 0);
static float2 _AlphaMaskPan = float4(0, 0, 0, 0);
static float _AlphaMaskUV = 0;
static float _AlphaMaskInvert = 0;
static float _MainAlphaMaskMode = 2;
static float _AlphaMaskBlendStrength = 1;
static float _AlphaMaskValue = 0;
static float _Cutoff = 0.01;
static float _MainColorAdjustToggle = 1;
// Skipped 1 lines | #if defined(PROP_MAINCOLORADJUSTTEXTURE) || !defined(OPTIMIZER_ENABLED)
static float4 _MainColorAdjustTexture_ST = float4(1, 1, 0, 0);
static float2 _MainColorAdjustTexturePan = float4(0, 0, 0, 0);
static float _MainColorAdjustTextureUV = 0;
static float _MainHueShiftColorSpace = 0;
static float _MainHueShiftSelectOrShift = 1;
static float _MainHueShiftToggle = 1;
static float _MainHueShiftReplace = 1;
static float _MainHueShiftSpeed = 0;
static float _MainBrightness = 0;
static float _MainGamma = 1;
static float _MainHueALCTEnabled = 0;
static float _MainALHueShiftBand = 0;
static float _MainALHueShiftCTIndex = 0;
static float _MainHueALMotionSpeed = 1;
static float _MainHueGlobalMask = 0;
static float _MainHueGlobalMaskBlendType = 2;
static float _MainSaturationGlobalMask = 0;
static float _MainSaturationGlobalMaskBlendType = 2;
static float _MainBrightnessGlobalMask = 0;
static float _MainBrightnessGlobalMaskBlendType = 2;
static float _MainGammaGlobalMask = 0;
static float _MainGammaGlobalMaskBlendType = 2;
#if defined(PROP_MAINGRADATIONTEX)
Texture2D _MainGradationTex;
#endif
static float _ColorGradingToggle = 0;
static float _MainGradationStrength = 0;
static float _AlphaForceOpaque = 0;
static float _AlphaMod = 0;
float _AlphaPremultiply;
float _AlphaBoostFA;
static float _AlphaGlobalMask = 0;
static float _AlphaGlobalMaskBlendType = 2;
static float _IgnoreFog = 0;
static float _RenderingReduceClipDistance = 0;
static int _FlipBackfaceNormals = 1;
static float _AddBlendOp = 4;
static float _Cull = 0;
int _GlobalMaskVertexColorLinearSpace;
static float _StereoEnabled = 0;
static float _PolarUV = 0;
static float2 _PolarCenter = float4(0.5, 0.5, 0, 0);
static float _PolarRadialScale = 1;
static float _PolarLengthScale = 1;
static float _PolarSpiralPower = 0;
static float _PanoUseBothEyes = 1;
static float _UVModWorldPos0 = 0;
static float _UVModWorldPos1 = 2;
static float _UVModLocalPos0 = 0;
static float _UVModLocalPos1 = 1;
static float _UzumoreEnabled = 1;
static float _UzumoreAmount = 0.1;
static float _UzumoreBias = 0.001;
// Skipped 1 lines | #if defined(PROP_UZUMOREMASK) || !defined(OPTIMIZER_ENABLED)
static float _UzumoreMaskUV = 3;
struct appdata
{
float4 vertex : POSITION;
float3 normal : NORMAL;
float4 tangent : TANGENT;
float4 color : COLOR;
//float4 uv0 : TEXCOORD0;
float2 uv1 : TEXCOORD1;
float2 uv2 : TEXCOORD2;
float2 uv3 : TEXCOORD3;
uint vertexId : SV_VertexID;
UNITY_VERTEX_INPUT_INSTANCE_ID
float4 uv0 : TEXCOORD0;
}
;
struct tessellatedAppData
{
float4 vertex : POSITION;
float3 normal : NORMAL;
float4 tangent : TANGENT;
float4 color : COLOR;
float2 uv0 : TEXCOORD0;
float2 uv1 : TEXCOORD1;
float2 uv2 : TEXCOORD2;
float2 uv3 : TEXCOORD3;
uint vertexId : TEXCOORD4;
UNITY_VERTEX_INPUT_INSTANCE_ID
}
;
struct VertexOut
{
float4 pos : SV_POSITION;
float4 uv[2] : TEXCOORD0;
float3 normal : TEXCOORD2;
float4 tangent : TEXCOORD3;
float4 worldPos : TEXCOORD4;
float4 localPos : TEXCOORD5;
float4 vertexColor : TEXCOORD6;
float4 lightmapUV : TEXCOORD7;
float worldDir : TEXCOORD8;
float2 fogData: TEXCOORD10;
UNITY_SHADOW_COORDS(12) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
}
;
struct PoiMesh
{
float3 normals[2];
float3 objNormal;
float3 tangentSpaceNormal;
float3 binormal[2];
float3 tangent[2];
float3 worldPos;
float3 localPos;
float3 objectPosition;
float isFrontFace;
float4 vertexColor;
float4 lightmapUV;
float2 uv[10];
float2 parallaxUV;
float2 dx;
float2 dy;
uint isRightHand;
}
;
struct PoiCam
{
float3 viewDir;
float3 forwardDir;
float3 worldPos;
float distanceToVert;
float4 clipPos;
float4 screenSpacePosition;
float3 reflectionDir;
float3 vertexReflectionDir;
float3 tangentViewDir;
float4 posScreenSpace;
float2 posScreenPixels;
float2 screenUV;
float vDotN;
float4 worldDirection;
}
;
struct PoiMods
{
float4 Mask;
float audioLink[5];
float audioLinkAvailable;
float audioLinkVersion;
float4 audioLinkTexture;
float2 detailMask;
float2 backFaceDetailIntensity;
float globalEmission;
float4 globalColorTheme[12];
float globalMask[16];
float ALTime[8];
}
;
struct PoiLight
{
float3 direction;
float nDotVCentered;
float attenuation;
float attenuationStrength;
float3 directColor;
float3 indirectColor;
float occlusion;
float shadowMask;
float detailShadow;
float3 halfDir;
float lightMap;
float lightMapNoAttenuation;
float3 rampedLightMap;
float vertexNDotL;
float nDotL;
float nDotV;
float vertexNDotV;
float nDotH;
float vertexNDotH;
float lDotv;
float lDotH;
float nDotLSaturated;
float nDotLNormalized;
#ifdef POI_PASS_ADD
float additiveShadow;
#endif
float3 finalLighting;
float3 finalLightAdd;
float3 LTCGISpecular;
float3 LTCGIDiffuse;
float directLuminance;
float indirectLuminance;
float finalLuminance;
#if defined(VERTEXLIGHT_ON)
float4 vDotNL;
float4 vertexVDotNL;
float3 vColor[4];
float4 vCorrectedDotNL;
float4 vAttenuation;
float4 vSaturatedDotNL;
float3 vPosition[4];
float3 vDirection[4];
float3 vFinalLighting;
float3 vHalfDir[4];
half4 vDotNH;
half4 vertexVDotNH;
half4 vDotLH;
#endif
}
;
struct PoiVertexLights
{
float3 direction;
float3 color;
float attenuation;
}
;
struct PoiFragData
{
float smoothness;
float smoothness2;
float metallic;
float specularMask;
float reflectionMask;
float3 baseColor;
float3 finalColor;
float alpha;
float3 emission;
float toggleVertexLights;
}
;
float4 poiTransformClipSpacetoScreenSpaceFrag(float4 clipPos)
{
float4 positionSS = float4(clipPos.xyz * clipPos.w, clipPos.w);
positionSS.xy = positionSS.xy / _ScreenParams.xy;
return positionSS;
}
static float4 PoiSHAr = 0;
static float4 PoiSHAg = 0;
static float4 PoiSHAb = 0;
static float4 PoiSHBr = 0;
static float4 PoiSHBg = 0;
static float4 PoiSHBb = 0;
static float4 PoiSHC  = 0;
half3 PoiSHEval_L0L1(half4 normal)
{
half3 x;
x.r = dot(PoiSHAr, normal);
x.g = dot(PoiSHAg, normal);
x.b = dot(PoiSHAb, normal);
return x;
}
half3 PoiSHEval_L2(half4 normal)
{
half3 x1;
half3 x2;
half4 vB = normal.xyzz * normal.yzzx;
x1.r = dot(PoiSHBr, vB);
x1.g = dot(PoiSHBg, vB);
x1.b = dot(PoiSHBb, vB);
half  vC = normal.x*normal.x - normal.y*normal.y;
x2    = PoiSHC.rgb * vC;
return x1 + x2;
}
half3 PoiShadeSH9 (half4 normal)
{
half3 res = PoiSHEval_L0L1(normal);
res += PoiSHEval_L2(normal);
// Skipped 1 lines | #ifdef UNITY_COLORSPACE_GAMMA
return res;
}
inline half4 Pow5(half4 x)
{
return x * x * x * x * x;
}
inline half3 FresnelLerp(half3 F0, half3 F90, half cosA)
{
half t = Pow5(1 - cosA);
return lerp(F0, F90, t);
}
inline half3 FresnelTerm(half3 F0, half cosA)
{
half t = Pow5(1 - cosA);
return F0 + (1 - F0) * t;
}
half perceptualRoughnessToMipmapLevel(half perceptualRoughness)
{
return perceptualRoughness * UNITY_SPECCUBE_LOD_STEPS;
}
half3 Unity_GlossyEnvironment(UNITY_ARGS_TEXCUBE(tex), half4 hdr, Unity_GlossyEnvironmentData glossIn)
{
half perceptualRoughness = glossIn.roughness  ;
// Expected defined at 0, got 0
#if 0
float m = PerceptualRoughnessToRoughness(perceptualRoughness);
const float fEps = 1.192092896e-07F;
float n = (2.0 / max(fEps, m * m)) - 2.0;
n /= 4;
perceptualRoughness = pow(2 / (n + 2), 0.25);
#else
perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
#endif
half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
half3 R = glossIn.reflUVW;
half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(tex, R, mip);
return DecodeHDR(rgbm, hdr);
}
half3 UnpackScaleNormalDXT5nm(half4 packednormal, half bumpScale)
{
half3 normal;
normal.xy = (packednormal.wy * 2 - 1);
// Expected defined at 1, got SHADER_TAR
// Expected defined at 1, got SHADER_TAR
#if (SHADER_TARGET >= 30)
normal.xy *= bumpScale;
#endif
normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
return normal;
}
half3 LerpWhiteTo(half3 b, half t)
{
half oneMinusT = 1 - t;
return half3(oneMinusT, oneMinusT, oneMinusT) + b * t;
}
inline float GGXTerm(float NdotH, float roughness)
{
float a2 = roughness * roughness;
float d = (NdotH * a2 - NdotH) * NdotH + 1.0f;
return UNITY_INV_PI * a2 / (d * d + 1e-7f);
}
Unity_GlossyEnvironmentData UnityGlossyEnvironmentSetup(half Smoothness, half3 worldViewDir, half3 Normal, half3 fresnel0)
{
Unity_GlossyEnvironmentData g;
g.roughness  = 1 - Smoothness;
g.reflUVW = reflect(-worldViewDir, Normal);
return g;
}
half3 UnpackScaleNormalRGorAG(half4 packednormal, half bumpScale)
{
#if defined(UNITY_NO_DXT5nm)
half3 normal = packednormal.xyz * 2 - 1;
// Expected defined at 1, got SHADER_TAR
// Expected defined at 1, got SHADER_TAR
#if (SHADER_TARGET >= 30)
normal.xy *= bumpScale;
#endif
return normal;
#elif defined(UNITY_ASTC_NORMALMAP_ENCODING)
half3 normal;
normal.xy = (packednormal.wy * 2 - 1);
normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
normal.xy *= bumpScale;
return normal;
#else
packednormal.x *= packednormal.w;
half3 normal;
normal.xy = (packednormal.xy * 2 - 1);
// Expected defined at 1, got SHADER_TAR
// Expected defined at 1, got SHADER_TAR
#if (SHADER_TARGET >= 30)
normal.xy *= bumpScale;
#endif
normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
return normal;
#endif
}
half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
{
return UnpackScaleNormalRGorAG(packednormal, bumpScale);
}
half3 BlendNormals(half3 n1, half3 n2)
{
return normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
}
inline float2 Pow4(float2 x)
{
return x * x * x * x;
}
inline float3 Unity_SafeNormalize(float3 inVec)
{
float dp3 = max(0.001f, dot(inVec, inVec));
return inVec * rsqrt(dp3);
}
inline float3 BoxProjectedCubemapDirection(float3 worldRefl, float3 worldPos, float4 cubemapCenter, float4 boxMin, float4 boxMax)
{
if (cubemapCenter.w > 0.0)
{
float3 nrdir = normalize(worldRefl);
// Expected defined at 0, got 1
#if 1
float3 rbmax = (boxMax.xyz - worldPos) / nrdir;
float3 rbmin = (boxMin.xyz - worldPos) / nrdir;
float3 rbminmax = (nrdir > 0.0f) ? rbmax : rbmin;
#else
float3 rbmax = (boxMax.xyz - worldPos);
float3 rbmin = (boxMin.xyz - worldPos);
float3 select = step(float3(0, 0, 0), nrdir);
float3 rbminmax = lerp(rbmax, rbmin, select);
rbminmax /= nrdir;
#endif
float fa = min(min(rbminmax.x, rbminmax.y), rbminmax.z);
worldPos -= cubemapCenter.xyz;
worldRefl = worldPos + nrdir * fa;
}
return worldRefl;
}
inline half3 UnityGI_IndirectSpecular(UnityGIInput data, half occlusion, Unity_GlossyEnvironmentData glossIn)
{
half3 specular;
#ifdef UNITY_SPECCUBE_BOX_PROJECTION
half3 originalReflUVW = glossIn.reflUVW;
glossIn.reflUVW = BoxProjectedCubemapDirection(originalReflUVW, data.worldPos, data.probePosition[0], data.boxMin[0], data.boxMax[0]);
#endif
specular = unity_IndirectSpecColor.rgb;
// Skipped 19 lines
return specular * occlusion;
}
inline half3 UnityGI_IndirectSpecular(UnityGIInput data, half occlusion, half3 normalWorld, Unity_GlossyEnvironmentData glossIn)
{
return UnityGI_IndirectSpecular(data, occlusion, glossIn);
}
#ifndef glsl_mod
#define glsl_mod(x, y) (((x) - (y) * floor((x) / (y))))
#endif
uniform float random_uniform_float_only_used_to_stop_compiler_warnings = 0.0f;
float2 poiUV(float2 uv, float4 tex_st)
{
return uv * tex_st.xy + tex_st.zw;
}
float2 vertexUV(in VertexOut o, int index)
{
switch(index)
{
case 0:
return o.uv[0].xy;
case 1:
return o.uv[0].zw;
case 2:
return o.uv[1].xy;
case 3:
return o.uv[1].zw;
default:
return o.uv[0].xy;
}
}
float2 vertexUV(in appdata v, int index)
{
switch(index)
{
case 0:
return v.uv0.xy;
case 1:
return v.uv1.xy;
case 2:
return v.uv2.xy;
case 3:
return v.uv3.xy;
default:
return v.uv0.xy;
}
}
float calculateluminance(float3 color)
{
return color.r * 0.299 + color.g * 0.587 + color.b * 0.114;
}
float dotToDegrees(float dot)
{
dot = clamp(dot, -1.0, 1.0);
return degrees(acos(dot));
}
float dotToDegrees(float3 a, float3 b)
{
return dotToDegrees(dot(normalize(a), normalize(b)));
}
float _VRChatCameraMode;
float _VRChatMirrorMode;
float VRCCameraMode()
{
return _VRChatCameraMode;
}
float VRCMirrorMode()
{
return _VRChatMirrorMode;
}
bool IsInMirror()
{
return unity_CameraProjection[2][0] != 0.f || unity_CameraProjection[2][1] != 0.f;
}
bool IsOrthographicCamera()
{
return unity_OrthoParams.w == 1 || UNITY_MATRIX_P[3][3] == 1;
}
float shEvaluateDiffuseL1Geomerics_local(float L0, float3 L1, float3 n)
{
float R0 = max(0, L0);
float3 R1 = 0.5f * L1;
float lenR1 = length(R1);
float q = dot(normalize(R1), n) * 0.5 + 0.5;
q = saturate(q);
float p = 1.0f + 2.0f * lenR1 / R0;
float a = (1.0f - lenR1 / R0) / (1.0f + lenR1 / R0);
return R0 * (a + (1.0f - a) * (p + 1.0f) * pow(q, p));
}
half3 BetterSH9(half4 normal)
{
float3 indirect;
float3 L0 = float3(PoiSHAr.w, PoiSHAg.w, PoiSHAb.w) + float3(PoiSHBr.z, PoiSHBg.z, PoiSHBb.z) / 3.0;
indirect.r = shEvaluateDiffuseL1Geomerics_local(L0.r, PoiSHAr.xyz, normal.xyz);
indirect.g = shEvaluateDiffuseL1Geomerics_local(L0.g, PoiSHAg.xyz, normal.xyz);
indirect.b = shEvaluateDiffuseL1Geomerics_local(L0.b, PoiSHAb.xyz, normal.xyz);
indirect = max(0, indirect);
indirect += SHEvalLinearL2(normal);
return indirect;
}
float3 getCameraForward()
{
// Expected defined at 0, got UNITY_SING
#if UNITY_SINGLE_PASS_STEREO
float3 p1 = mul(unity_StereoCameraToWorld[0], float4(0, 0, 1, 1));
float3 p2 = mul(unity_StereoCameraToWorld[0], float4(0, 0, 0, 1));
#else
float3 p1 = mul(unity_CameraToWorld, float4(0, 0, 1, 1)).xyz;
float3 p2 = mul(unity_CameraToWorld, float4(0, 0, 0, 1)).xyz;
#endif
return normalize(p2 - p1);
}
half3 GetSHLength()
{
half3 x;
half3 x1;
x.r = length(PoiSHAr);
x.g = length(PoiSHAg);
x.b = length(PoiSHAb);
x1.r = length(PoiSHBr);
x1.g = length(PoiSHBg);
x1.b = length(PoiSHBb);
return x + x1;
}
float3 BoxProjection(float3 direction, float3 position, float4 cubemapPosition, float3 boxMin, float3 boxMax)
{
// Expected defined at 0, got UNITY_SPEC
#if UNITY_SPECCUBE_BOX_PROJECTION
if (cubemapPosition.w > 0)
{
float3 factors = ((direction > 0 ? boxMax : boxMin) - position) / direction;
float scalar = min(min(factors.x, factors.y), factors.z);
direction = direction * scalar + (position - cubemapPosition.xyz);
}
#endif
return direction;
}
float poiMax(float2 i)
{
return max(i.x, i.y);
}
float poiMax(float3 i)
{
return max(max(i.x, i.y), i.z);
}
float poiMax(float4 i)
{
return max(max(max(i.x, i.y), i.z), i.w);
}
float3 calculateNormal(in float3 baseNormal, in PoiMesh poiMesh, in Texture2D normalTexture, in float4 normal_ST, in float2 normalPan, in float normalUV, in float normalIntensity)
{
float3 normal = UnpackScaleNormal(POI2D_SAMPLER_PAN(normalTexture, _MainTex, poiUV(poiMesh.uv[normalUV], normal_ST), normalPan), normalIntensity);
return normalize( normal.x * poiMesh.tangent[0] + normal.y * poiMesh.binormal[0] + normal.z * baseNormal );
}
float remap(float x, float minOld, float maxOld, float minNew = 0, float maxNew = 1)
{
return minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld);
}
float2 remap(float2 x, float2 minOld, float2 maxOld, float2 minNew = 0, float2 maxNew = 1)
{
return minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld);
}
float3 remap(float3 x, float3 minOld, float3 maxOld, float3 minNew = 0, float3 maxNew = 1)
{
return minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld);
}
float4 remap(float4 x, float4 minOld, float4 maxOld, float4 minNew = 0, float4 maxNew = 1)
{
return minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld);
}
float remapClamped(float minOld, float maxOld, float x, float minNew = 0, float maxNew = 1)
{
return clamp(minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld), minNew, maxNew);
}
float2 remapClamped(float2 minOld, float2 maxOld, float2 x, float2 minNew, float2 maxNew)
{
return clamp(minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld), minNew, maxNew);
}
float3 remapClamped(float3 minOld, float3 maxOld, float3 x, float3 minNew, float3 maxNew)
{
return clamp(minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld), minNew, maxNew);
}
float4 remapClamped(float4 minOld, float4 maxOld, float4 x, float4 minNew, float4 maxNew)
{
return clamp(minNew + (x - minOld) * (maxNew - minNew) / (maxOld - minOld), minNew, maxNew);
}
float2 calcParallax(in float height, in PoiCam poiCam)
{
return ((height * - 1) + 1) * (poiCam.tangentViewDir.xy / poiCam.tangentViewDir.z);
}
float4 poiBlend(const float sourceFactor, const  float4 sourceColor, const  float destinationFactor, const  float4 destinationColor, const float4 blendFactor)
{
float4 sA = 1 - blendFactor;
const float4 blendData[11] =
{
float4(0.0, 0.0, 0.0, 0.0), float4(1.0, 1.0, 1.0, 1.0), destinationColor, sourceColor, float4(1.0, 1.0, 1.0, 1.0) - destinationColor, sA, float4(1.0, 1.0, 1.0, 1.0) - sourceColor, sA, float4(1.0, 1.0, 1.0, 1.0) - sA, saturate(sourceColor.aaaa), 1 - sA,
}
;
return lerp(blendData[sourceFactor] * sourceColor + blendData[destinationFactor] * destinationColor, sourceColor, sA);
}
float blendColorBurn(float base, float blend)
{
return (blend == 0.0) ? blend : max((1.0 - ((1.0 - base) * rcp(random_uniform_float_only_used_to_stop_compiler_warnings + blend))), 0.0);
}
float3 blendColorBurn(float3 base, float3 blend)
{
return float3(blendColorBurn(base.r, blend.r), blendColorBurn(base.g, blend.g), blendColorBurn(base.b, blend.b));
}
float blendColorDodge(float base, float blend)
{
return (blend == 1.0) ? blend : min(base / (1.0 - blend), 1.0);
}
float3 blendColorDodge(float3 base, float3 blend)
{
return float3(blendColorDodge(base.r, blend.r), blendColorDodge(base.g, blend.g), blendColorDodge(base.b, blend.b));
}
float blendDarken(float base, float blend)
{
return min(blend, base);
}
float3 blendDarken(float3 base, float3 blend)
{
return float3(blendDarken(base.r, blend.r), blendDarken(base.g, blend.g), blendDarken(base.b, blend.b));
}
float blendOverlay(float base, float blend)
{
return base < 0.5 ? (2.0 * base * blend) : (1.0 - 2.0 * (1.0 - base) * (1.0 - blend));
}
float3 blendOverlay(float3 base, float3 blend)
{
return float3(blendOverlay(base.r, blend.r), blendOverlay(base.g, blend.g), blendOverlay(base.b, blend.b));
}
float blendLighten(float base, float blend)
{
return max(blend, base);
}
float3 blendLighten(float3 base, float3 blend)
{
return float3(blendLighten(base.r, blend.r), blendLighten(base.g, blend.g), blendLighten(base.b, blend.b));
}
float blendLinearDodge(float base, float blend)
{
return min(base + blend, 1.0);
}
float3 blendLinearDodge(float3 base, float3 blend)
{
return base + blend;
}
float blendMultiply(float base, float blend)
{
return base * blend;
}
float3 blendMultiply(float3 base, float3 blend)
{
return base * blend;
}
float blendNormal(float base, float blend)
{
return blend;
}
float3 blendNormal(float3 base, float3 blend)
{
return blend;
}
float blendScreen(float base, float blend)
{
return 1.0 - ((1.0 - base) * (1.0 - blend));
}
float3 blendScreen(float3 base, float3 blend)
{
return float3(blendScreen(base.r, blend.r), blendScreen(base.g, blend.g), blendScreen(base.b, blend.b));
}
float blendSubtract(float base, float blend)
{
return max(base - blend, 0.0);
}
float3 blendSubtract(float3 base, float3 blend)
{
return max(base - blend, 0.0);
}
float blendMixed(float base, float blend)
{
return base + base * blend;
}
float3 blendMixed(float3 base, float3 blend)
{
return base + base * blend;
}
float3 customBlend(float3 base, float3 blend, float blendType, float alpha = 1)
{
float3 output = base;
switch(blendType)
{
case 0: output = lerp(base, blend, alpha);
break;
case 1: output = lerp(base, blendDarken(base, blend), alpha);
break;
case 2: output = base * lerp(1, blend, alpha);
break;
case 5: output = lerp(base, blendLighten(base, blend), alpha);
break;
case 6: output = lerp(base, blendScreen(base, blend), alpha);
break;
case 7: output = blendSubtract(base, blend * alpha);
break;
case 8: output = lerp(base, blendLinearDodge(base, blend), alpha);
break;
case 9: output = lerp(base, blendOverlay(base, blend), alpha);
break;
case 20: output = lerp(base, blendMixed(base, blend), alpha);
break;
default: output = 0;
break;
}
return output;
}
float3 customBlend(float base, float blend, float blendType, float alpha = 1)
{
float3 output = base;
switch(blendType)
{
case 0: output = lerp(base, blend, alpha);
break;
case 2: output = base * lerp(1, blend, alpha);
break;
case 5: output = lerp(base, blendLighten(base, blend), alpha);
break;
case 6: output = lerp(base, blendScreen(base, blend), alpha);
break;
case 7: output = blendSubtract(base, blend * alpha);
break;
case 8: output = lerp(base, blendLinearDodge(base, blend), alpha);
break;
case 9: output = lerp(base, blendOverlay(base, blend), alpha);
break;
case 20: output = lerp(base, blendMixed(base, blend), alpha);
break;
default: output = 0;
break;
}
return output;
}
#define REPLACE 0
#define SUBSTRACT 1
#define MULTIPLY 2
#define DIVIDE 3
#define MIN 4
#define MAX 5
#define AVERAGE 6
#define ADD 7
float maskBlend(float baseMask, float blendMask, float blendType)
{
float output = 0;
switch(blendType)
{
case REPLACE: output = blendMask;
break;
case SUBSTRACT: output = baseMask - blendMask;
break;
case MULTIPLY: output = baseMask * blendMask;
break;
case DIVIDE: output = baseMask / blendMask;
break;
case MIN: output = min(baseMask, blendMask);
break;
case MAX: output = max(baseMask, blendMask);
break;
case AVERAGE: output = (baseMask + blendMask) * 0.5;
break;
case ADD: output = baseMask + blendMask;
break;
}
return saturate(output);
}
float globalMaskBlend(float baseMask, float globalMaskIndex, float blendType, PoiMods poiMods)
{
if (globalMaskIndex == 0)
{
return baseMask;
}
else
{
return maskBlend(baseMask, poiMods.globalMask[globalMaskIndex - 1], blendType);
}
}
inline float poiRand(float2 co)
{
float3 p3 = frac(float3(co.xyx) * 0.1031);
p3 += dot(p3, p3.yzx + 33.33);
return frac((p3.x + p3.y) * p3.z);
}
inline float4 poiRand4(float2 seed)
{
float3 p3 = frac(float3(seed.xyx) * 0.1031);
p3 += dot(p3, p3.yzx + 33.33);
float2 a = frac((p3.xx + p3.yz) * p3.zy);
float2 s2 = seed + 37.0;
float3 q3 = frac(float3(s2.xyx) * 0.1031);
q3 += dot(q3, q3.yzx + 33.33);
float2 b = frac((q3.xx + q3.yz) * q3.zy);
return float4(a, b);
}
inline float2 poiRand2(float seed)
{
float2 x = float2(seed, seed * 1.3);
float3 p3 = frac(float3(x.xyx) * 0.1031);
p3 += dot(p3, p3.yzx + 33.33);
return frac((p3.xx + p3.yz) * p3.zy);
}
inline float2 poiRand2(float2 seed)
{
float3 p3 = frac(float3(seed.xyx) * 0.1031);
p3 += dot(p3, p3.yzx + 33.33);
return frac((p3.xx + p3.yz) * p3.zy);
}
inline float poiRand3(float seed)
{
float p = frac(seed * 0.1031);
p *= p + 33.33;
p *= p + p;
return frac(p);
}
inline float3 poiRand3(float2 seed)
{
float3 p3 = frac(float3(seed.xyx) * 0.1031);
p3 += dot(p3, p3.yzx + 33.33);
return frac((p3.xxy + p3.yzz) * p3.zyx);
}
inline float3 poiRand3(float3 seed)
{
float3 p3 = frac(seed * 0.1031);
p3 += dot(p3, p3.zyx + 31.32);
return frac((p3.xxy + p3.yzz) * p3.zyx);
}
inline float3 poiRand3Range(float2 Seed, float Range)
{
float3 r = poiRand3(Seed);
return (r * 2.0 - 1.0) * Range;
}
float3 randomFloat3WiggleRange(float2 Seed, float Range, float wiggleSpeed, float timeOffset)
{
float3 rando = (float3( frac(sin(dot(Seed.xy, float2(12.9898, 78.233))) * 43758.5453), frac(sin(dot(Seed.yx, float2(12.9898, 78.233))) * 43758.5453), frac(sin(dot(float2(Seed.x * Seed.y, Seed.y + Seed.x), float2(12.9898, 78.233))) * 43758.5453) ) * 2 - 1);
float speed = 1 + wiggleSpeed;
return float3(sin(((_Time.x + timeOffset) + rando.x * PI) * speed), sin(((_Time.x + timeOffset) + rando.y * PI) * speed), sin(((_Time.x + timeOffset) + rando.z * PI) * speed)) * Range;
}
static const float3 HCYwts = float3(0.299, 0.587, 0.114);
static const float HCLgamma = 3;
static const float HCLy0 = 100;
static const float HCLmaxL = 0.530454533953517;
static const float3 wref = float3(1.0, 1.0, 1.0);
#define TAU 6.28318531
float3 HUEtoRGB(in float H)
{
float R = abs(H * 6 - 3) - 1;
float G = 2 - abs(H * 6 - 2);
float B = 2 - abs(H * 6 - 4);
return saturate(float3(R, G, B));
}
float3 RGBtoHCV(in float3 RGB)
{
float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0 / 3.0) : float4(RGB.gb, 0.0, -1.0 / 3.0);
float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
float C = Q.x - min(Q.w, Q.y);
float H = abs((Q.w - Q.y) / (6 * C + EPSILON) + Q.z);
return float3(H, C, Q.x);
}
float3 RGBtoHSV(float3 c)
{
float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
float d = q.x - min(q.w, q.y);
float e = 1.0e-10;
return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}
float3 HSVtoRGB(float3 c)
{
float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}
void DecomposeHDRColor(in float3 linearColorHDR, out float3 baseLinearColor, out float exposure)
{
float maxColorComponent = max(linearColorHDR.r, max(linearColorHDR.g, linearColorHDR.b));
bool isSDR = maxColorComponent <= 1.0;
float scaleFactor = isSDR ? 1.0 : (1.0 / maxColorComponent);
exposure = isSDR ? 0.0 : log(maxColorComponent) * 1.44269504089;
baseLinearColor = scaleFactor * linearColorHDR;
}
float3 ApplyHDRExposure(float3 linearColor, float exposure)
{
return linearColor * pow(2, exposure);
}
float3 ModifyViaHSV(float3 color, float h, float s, float v)
{
float3 colorHSV = RGBtoHSV(color);
colorHSV.x = frac(colorHSV.x + h);
colorHSV.y = saturate(colorHSV.y + s);
colorHSV.z = saturate(colorHSV.z + v);
return HSVtoRGB(colorHSV);
}
float3 ModifyViaHSV(float3 color, float3 HSVMod)
{
return ModifyViaHSV(color, HSVMod.x, HSVMod.y, HSVMod.z);
}
float4x4 brightnessMatrix(float brightness)
{
return float4x4( 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, brightness, brightness, brightness, 1 );
}
float4x4 contrastMatrix(float contrast)
{
float t = (1.0 - contrast) / 2.0;
return float4x4( contrast, 0, 0, 0, 0, contrast, 0, 0, 0, 0, contrast, 0, t, t, t, 1 );
}
float4x4 saturationMatrix(float saturation)
{
float3 luminance = float3(0.3086, 0.6094, 0.0820);
float oneMinusSat = 1.0 - saturation;
float3 red = luminance.x * oneMinusSat;
red += float3(saturation, 0, 0);
float3 green = luminance.y * oneMinusSat;
green += float3(0, saturation, 0);
float3 blue = luminance.z * oneMinusSat;
blue += float3(0, 0, saturation);
return float4x4( red, 0, green, 0, blue, 0, 0, 0, 0, 1 );
}
float4 PoiColorBCS(float4 color, float brightness, float contrast, float saturation)
{
return mul(color, mul(brightnessMatrix(brightness), mul(contrastMatrix(contrast), saturationMatrix(saturation))));
}
float3 PoiColorBCS(float3 color, float brightness, float contrast, float saturation)
{
return mul(float4(color, 1), mul(brightnessMatrix(brightness), mul(contrastMatrix(contrast), saturationMatrix(saturation)))).rgb;
}
float3 linear_srgb_to_oklab(float3 c)
{
float l = 0.4122214708 * c.x + 0.5363325363 * c.y + 0.0514459929 * c.z;
float m = 0.2119034982 * c.x + 0.6806995451 * c.y + 0.1073969566 * c.z;
float s = 0.0883024619 * c.x + 0.2817188376 * c.y + 0.6299787005 * c.z;
float l_ = pow(l, 1.0 / 3.0);
float m_ = pow(m, 1.0 / 3.0);
float s_ = pow(s, 1.0 / 3.0);
return float3( 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_, 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_, 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_ );
}
float3 oklab_to_linear_srgb(float3 c)
{
float l_ = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
float m_ = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
float s_ = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;
float l = l_ * l_ * l_;
float m = m_ * m_ * m_;
float s = s_ * s_ * s_;
return float3( + 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, - 1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, - 0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s );
}
float3 hueShiftOKLab(float3 color, float shift, float selectOrShift)
{
float3 oklab = linear_srgb_to_oklab(color);
float chroma = length(oklab.yz);
if (chroma < 1e-5)
{
return color;
}
float hue = atan2(oklab.z, oklab.y);
hue = shift * TWO_PI + hue * selectOrShift;
oklab.y = cos(hue) * chroma;
oklab.z = sin(hue) * chroma;
return oklab_to_linear_srgb(oklab);
}
float3 hueShiftHSV(float3 color, float hueOffset, float selectOrShift)
{
float3 hsvCol = RGBtoHSV(color);
hsvCol.x = hsvCol.x * selectOrShift + hueOffset;
return HSVtoRGB(hsvCol);
}
float3 hueShift(float3 color, float shift, float ColorSpace, float selectOrShift)
{
switch(ColorSpace)
{
case 0.0:
return hueShiftOKLab(color, shift, selectOrShift);
case 1.0:
return hueShiftHSV(color, shift, selectOrShift);
default:
return float3(1.0, 0.0, 0.0);
}
}
float4 hueShift(float4 color, float shift, float ColorSpace, float selectOrShift)
{
return float4(hueShift(color.rgb, shift, ColorSpace, selectOrShift), color.a);
}
float4x4 poiRotationMatrixFromAngles(float x, float y, float z)
{
float angleX = radians(x);
float c = cos(angleX);
float s = sin(angleX);
float4x4 rotateXMatrix = float4x4(1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0, 0, 0, 0, 1);
float angleY = radians(y);
c = cos(angleY);
s = sin(angleY);
float4x4 rotateYMatrix = float4x4(c, 0, s, 0, 0, 1, 0, 0, - s, 0, c, 0, 0, 0, 0, 1);
float angleZ = radians(z);
c = cos(angleZ);
s = sin(angleZ);
float4x4 rotateZMatrix = float4x4(c, -s, 0, 0, s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
return mul(mul(rotateXMatrix, rotateYMatrix), rotateZMatrix);
}
float4x4 poiRotationMatrixFromAngles(float3 angles)
{
float angleX = radians(angles.x);
float c = cos(angleX);
float s = sin(angleX);
float4x4 rotateXMatrix = float4x4(1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0, 0, 0, 0, 1);
float angleY = radians(angles.y);
c = cos(angleY);
s = sin(angleY);
float4x4 rotateYMatrix = float4x4(c, 0, s, 0, 0, 1, 0, 0, - s, 0, c, 0, 0, 0, 0, 1);
float angleZ = radians(angles.z);
c = cos(angleZ);
s = sin(angleZ);
float4x4 rotateZMatrix = float4x4(c, -s, 0, 0, s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
return mul(mul(rotateXMatrix, rotateYMatrix), rotateZMatrix);
}
float3 _VRChatMirrorCameraPos;
float3 getCameraPosition()
{
#ifdef USING_STEREO_MATRICES
return unity_StereoWorldSpaceCameraPos[0] * .5 + unity_StereoWorldSpaceCameraPos[1] * .5;
#endif
return _VRChatMirrorMode == 1 ? _VRChatMirrorCameraPos : _WorldSpaceCameraPos;
}
#ifdef POI_AUDIOLINK
inline int poiALBandPass(int bandIdx)
{
bandIdx = clamp(bandIdx, 0, 3);
return bandIdx == 0 ? ALPASS_AUDIOBASS : bandIdx == 1 ? ALPASS_AUDIOLOWMIDS : bandIdx == 2 ? ALPASS_AUDIOHIGHMIDS : ALPASS_AUDIOTREBLE;
}
#endif
float2 calcPixelScreenUVs(half4 grabPos)
{
half2 uv = grabPos.xy / (grabPos.w + 0.0000000001);
// Expected defined at 0, got UNITY_SING
#if UNITY_SINGLE_PASS_STEREO
uv.xy *= half2(_ScreenParams.x * 2, _ScreenParams.y);
#else
uv.xy *= _ScreenParams.xy;
#endif
return uv;
}
float CalcMipLevel(float2 texture_coord)
{
float2 dx = ddx(texture_coord);
float2 dy = ddy(texture_coord);
float delta_max_sqr = max(dot(dx, dx), dot(dy, dy));
return 0.5 * log2(delta_max_sqr);
}
float inverseLerp(float A, float B, float T)
{
return (T - A) / (B - A);
}
float inverseLerp2(float2 a, float2 b, float2 value)
{
float2 AB = b - a;
float2 AV = value - a;
return dot(AV, AB) / dot(AB, AB);
}
float inverseLerp3(float3 a, float3 b, float3 value)
{
float3 AB = b - a;
float3 AV = value - a;
return dot(AV, AB) / dot(AB, AB);
}
float inverseLerp4(float4 a, float4 b, float4 value)
{
float4 AB = b - a;
float4 AV = value - a;
return dot(AV, AB) / dot(AB, AB);
}
float4 QuaternionFromMatrix( float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
{
float4 q;
float trace = m00 + m11 + m22;
if (trace > 0)
{
float s = sqrt(trace + 1) * 2;
q.w = 0.25 * s;
q.x = (m21 - m12) / s;
q.y = (m02 - m20) / s;
q.z = (m10 - m01) / s;
}
else if (m00 > m11 && m00 > m22)
{
float s = sqrt(1 + m00 - m11 - m22) * 2;
q.w = (m21 - m12) / s;
q.x = 0.25 * s;
q.y = (m01 + m10) / s;
q.z = (m02 + m20) / s;
}
else if (m11 > m22)
{
float s = sqrt(1 + m11 - m00 - m22) * 2;
q.w = (m02 - m20) / s;
q.x = (m01 + m10) / s;
q.y = 0.25 * s;
q.z = (m12 + m21) / s;
}
else
{
float s = sqrt(1 + m22 - m00 - m11) * 2;
q.w = (m10 - m01) / s;
q.x = (m02 + m20) / s;
q.y = (m12 + m21) / s;
q.z = 0.25 * s;
}
return q;
}
float4 MulQuat(float4 a, float4 b)
{
return float4( a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x, a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z );
}
float4 QuaternionFromBasis(float3 sx, float3 sy, float3 sz)
{
return QuaternionFromMatrix( sx.x, sy.x, sz.x, sx.y, sy.y, sz.y, sx.z, sy.z, sz.z );
}
float4 BuildQuatFromForwardUp(float3 forward, float3 up)
{
float3 f = normalize(forward);
float3 u = normalize(up);
float3 x = normalize(cross(u, f));
float3 y = cross(f, x);
return QuaternionFromBasis(x, y, f);
}
float3 QuaternionToEuler(float4 q)
{
float3 euler;
float sinr_cosp = 2 * (q.w * q.z + q.x * q.y);
float cosr_cosp = 1 - 2 * (q.z * q.z + q.x * q.x);
euler.z = atan2(sinr_cosp, cosr_cosp) * 57.2958;
float sinp = 2 * (q.w * q.x - q.y * q.z);
if (abs(sinp) >= 1) euler.x = (sinp >= 0 ? 1 : - 1) * 90;
else euler.x = asin(sinp) * 57.2958;
float siny_cosp = 2 * (q.w * q.y + q.z * q.x);
float cosy_cosp = 1 - 2 * (q.x * q.x + q.y * q.y);
euler.y = atan2(siny_cosp, cosy_cosp) * 57.2958;
return euler;
}
float4 EulerToQuaternion(float3 euler)
{
float3 eulerRad = euler * 0.0174533;
float cx = cos(eulerRad.x * 0.5);
float sx = sin(eulerRad.x * 0.5);
float cy = cos(eulerRad.y * 0.5);
float sy = sin(eulerRad.y * 0.5);
float cz = cos(eulerRad.z * 0.5);
float sz = sin(eulerRad.z * 0.5);
float4 q;
q.w = cx * cy * cz + sx * sy * sz;
q.x = sx * cy * cz - cx * sy * sz;
q.y = cx * sy * cz + sx * cy * sz;
q.z = cx * cy * sz - sx * sy * cz;
return q;
}
float4 quaternion_conjugate(float4 v)
{
return float4( v.x, -v.yzw );
}
float4 quaternion_mul(float4 v1, float4 v2)
{
float4 result1 = (v1.x * v2 + v1 * v2.x);
float4 result2 = float4( - dot(v1.yzw, v2.yzw), cross(v1.yzw, v2.yzw) );
return float4(result1 + result2);
}
float4 get_quaternion_from_angle(float3 axis, float angle)
{
float sn = sin(angle * 0.5);
float cs = cos(angle * 0.5);
return float4(axis * sn, cs);
}
float4 quaternion_from_vector(float3 inVec)
{
return float4(0.0, inVec);
}
float degree_to_radius(float degree)
{
return ( degree / 180.0 * PI );
}
float3 rotate_with_quaternion(float3 inVec, float3 rotation)
{
float4 qx = get_quaternion_from_angle(float3(1, 0, 0), radians(rotation.x));
float4 qy = get_quaternion_from_angle(float3(0, 1, 0), radians(rotation.y));
float4 qz = get_quaternion_from_angle(float3(0, 0, 1), radians(rotation.z));
#define MUL3(A, B, C) quaternion_mul(quaternion_mul((A), (B)), (C))
float4 quaternion = normalize(MUL3(qx, qy, qz));
float4 conjugate = quaternion_conjugate(quaternion);
float4 inVecQ = quaternion_from_vector(inVec);
float3 rotated = ( MUL3(quaternion, inVecQ, conjugate) ).yzw;
return rotated;
}
float3 RotateByQuaternion(float4 q, float3 v)
{
float3 u = q.xyz;
float s = q.w;
return 2.0 * dot(u, v) * u + (s * s - dot(u, u)) * v + 2.0 * s * cross(u, v);
}
float4 SlerpQuaternion(float4 qa, float4 qb, float t)
{
float cosHalfTheta = dot(qa, qb);
if (cosHalfTheta < 0.0)
{
qb = -qb;
cosHalfTheta = -cosHalfTheta;
}
if (cosHalfTheta > 0.9995)
{
float4 qr = normalize(qa * (1 - t) + qb * t);
return qr;
}
float halfTheta = acos(cosHalfTheta);
float sinHalfTheta = sqrt(1.0 - cosHalfTheta * cosHalfTheta);
float a = sin((1 - t) * halfTheta) / sinHalfTheta;
float b = sin(t * halfTheta) / sinHalfTheta;
return qa * a + qb * b;
}
float4 transform(float4 input, float4 pos, float4 rotation, float4 scale)
{
input.rgb *= (scale.xyz * scale.w);
input = float4(rotate_with_quaternion(input.xyz, rotation.xyz * rotation.w) + (pos.xyz * pos.w), input.w);
return input;
}
float2 RotateUV(float2 _uv, float _radian, float2 _piv, float _time)
{
float RotateUV_ang = _radian;
float RotateUV_cos = cos(_time * RotateUV_ang);
float RotateUV_sin = sin(_time * RotateUV_ang);
return (mul(_uv - _piv, float2x2(RotateUV_cos, -RotateUV_sin, RotateUV_sin, RotateUV_cos)) + _piv);
}
float3 RotateAroundAxis(float3 original, float3 axis, float radian)
{
float s = sin(radian);
float c = cos(radian);
float one_minus_c = 1.0 - c;
axis = normalize(axis);
float3x3 rot_mat =
{
one_minus_c * axis.x * axis.x + c, one_minus_c * axis.x * axis.y - axis.z * s, one_minus_c * axis.z * axis.x + axis.y * s, one_minus_c * axis.x * axis.y + axis.z * s, one_minus_c * axis.y * axis.y + c, one_minus_c * axis.y * axis.z - axis.x * s, one_minus_c * axis.z * axis.x - axis.y * s, one_minus_c * axis.y * axis.z + axis.x * s, one_minus_c * axis.z * axis.z + c
}
;
return mul(rot_mat, original);
}
float3 poiThemeColor(in PoiMods poiMods, in float3 srcColor, in float themeIndex)
{
float3 outputColor = srcColor;
if (themeIndex != 0)
{
themeIndex = max(themeIndex - 1, 0);
if (themeIndex <= 3)
{
outputColor = poiMods.globalColorTheme[themeIndex];
}
else
{
#ifdef POI_AUDIOLINK
if (poiMods.audioLinkAvailable)
{
outputColor = poiMods.globalColorTheme[themeIndex];
}
#endif
}
}
return outputColor;
}
float3 lilToneCorrection(float3 c, float4 hsvg)
{
c = pow(abs(c), hsvg.w);
float4 p = (c.b > c.g) ? float4(c.bg, -1.0, 2.0 / 3.0) : float4(c.gb, 0.0, -1.0 / 3.0);
float4 q = (p.x > c.r) ? float4(p.xyw, c.r) : float4(c.r, p.yzx);
float d = q.x - min(q.w, q.y);
float e = 1.0e-10;
float3 hsv = float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
hsv = float3(hsv.x + hsvg.x, saturate(hsv.y * hsvg.y), saturate(hsv.z * hsvg.z));
return hsv.z - hsv.z * hsv.y + hsv.z * hsv.y * saturate(abs(frac(hsv.x + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
}
float3 lilBlendColor(float3 dstCol, float3 srcCol, float3 srcA, int blendMode)
{
float3 ad = dstCol + srcCol;
float3 mu = dstCol * srcCol;
float3 outCol = float3(0, 0, 0);
if (blendMode == 0) outCol = srcCol;
if (blendMode == 1) outCol = ad;
if (blendMode == 2) outCol = max(ad - mu, dstCol);
if (blendMode == 3) outCol = mu;
return lerp(dstCol, outCol, srcA);
}
float lilIsIn0to1(float f)
{
float value = 0.5 - abs(f - 0.5);
return saturate(value / clamp(fwidth(value), 0.0001, 1.0));
}
float lilIsIn0to1(float f, float nv)
{
float value = 0.5 - abs(f - 0.5);
return saturate(value / clamp(fwidth(value), 0.0001, nv));
}
float poiEdgeLinearNoSaturate(float value, float border)
{
return (value - border) / clamp(fwidth(value), 0.0001, 1.0);
}
float3 poiEdgeLinearNoSaturate(float value, float3 border)
{
return float3( (value - border.x) / clamp(fwidth(value), 0.0001, 1.0), (value - border.y) / clamp(fwidth(value), 0.0001, 1.0), (value - border.z) / clamp(fwidth(value), 0.0001, 1.0) );
}
float poiEdgeLinearNoSaturate(float value, float border, float blur)
{
float borderMin = saturate(border - blur * 0.5);
float borderMax = saturate(border + blur * 0.5);
return (value - borderMin) / max(saturate(borderMax - borderMin + fwidth(value)), .0001);
}
float poiEdgeLinearNoSaturate(float value, float border, float blur, float borderRange)
{
float borderMin = saturate(border - blur * 0.5 - borderRange);
float borderMax = saturate(border + blur * 0.5);
return (value - borderMin) / max(saturate(borderMax - borderMin + fwidth(value)), .0001);
}
float poiEdgeNonLinearNoSaturate(float value, float border)
{
float fwidthValue = fwidth(value);
return smoothstep(border - fwidthValue, border + fwidthValue, value);
}
float poiEdgeNonLinearNoSaturate(float value, float border, float blur)
{
float fwidthValue = fwidth(value);
float borderMin = saturate(border - blur * 0.5);
float borderMax = saturate(border + blur * 0.5);
return smoothstep(borderMin - fwidthValue, borderMax + fwidthValue, value);
}
float poiEdgeNonLinearNoSaturate(float value, float border, float blur, float borderRange)
{
float fwidthValue = fwidth(value);
float borderMin = saturate(border - blur * 0.5 - borderRange);
float borderMax = saturate(border + blur * 0.5);
return smoothstep(borderMin - fwidthValue, borderMax + fwidthValue, value);
}
float poiEdgeNonLinear(float value, float border)
{
return saturate(poiEdgeNonLinearNoSaturate(value, border));
}
float poiEdgeNonLinear(float value, float border, float blur)
{
return saturate(poiEdgeNonLinearNoSaturate(value, border, blur));
}
float poiEdgeNonLinear(float value, float border, float blur, float borderRange)
{
return saturate(poiEdgeNonLinearNoSaturate(value, border, blur, borderRange));
}
float poiEdgeLinear(float value, float border)
{
return saturate(poiEdgeLinearNoSaturate(value, border));
}
float poiEdgeLinear(float value, float border, float blur)
{
return saturate(poiEdgeLinearNoSaturate(value, border, blur));
}
float poiEdgeLinear(float value, float border, float blur, float borderRange)
{
return saturate(poiEdgeLinearNoSaturate(value, border, blur, borderRange));
}
float3 OpenLitLinearToSRGB(float3 col)
{
return LinearToGammaSpace(col);
}
float3 OpenLitSRGBToLinear(float3 col)
{
return GammaToLinearSpace(col);
}
float OpenLitLuminance(float3 rgb)
{
// Skipped 1 lines | #if defined(UNITY_COLORSPACE_GAMMA)
return dot(rgb, float3(0.0396819152, 0.458021790, 0.00609653955));
}
float3 AdjustLitLuminance(float3 rgb, float targetLuminance)
{
float currentLuminance;
// Skipped 1 lines | #if defined(UNITY_COLORSPACE_GAMMA)
currentLuminance = dot(rgb, float3(0.0396819152, 0.458021790, 0.00609653955));
float luminanceRatio = targetLuminance / currentLuminance;
return rgb * luminanceRatio;
}
float3 ClampLuminance(float3 rgb, float minLuminance, float maxLuminance)
{
float currentLuminance = dot(rgb, float3(0.299, 0.587, 0.114));
float minRatio = (currentLuminance != 0) ? minLuminance / currentLuminance : 1.0;
float maxRatio = (currentLuminance != 0) ? maxLuminance / currentLuminance : 1.0;
float luminanceRatio = clamp(min(maxRatio, max(minRatio, 1.0)), 0.0, 1.0);
return lerp(rgb, rgb * luminanceRatio, luminanceRatio < 1.0);
}
float3 MaxLuminance(float3 rgb, float maxLuminance)
{
float currentLuminance = dot(rgb, float3(0.299, 0.587, 0.114));
float luminanceRatio = (currentLuminance != 0) ? maxLuminance / max(currentLuminance, 0.00001) : 1.0;
return lerp(rgb, rgb * luminanceRatio, currentLuminance > maxLuminance);
}
float OpenLitGray(float3 rgb)
{
return dot(rgb, float3(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0));
}
void OpenLitShadeSH9ToonDouble(float3 lightDirection, out float3 shMax, out float3 shMin)
{
float3 N = lightDirection * 0.666666;
float4 vB = N.xyzz * N.yzzx;
float3 res = float3(PoiSHAr.w, PoiSHAg.w, PoiSHAb.w);
res.r += dot(PoiSHBr, vB);
res.g += dot(PoiSHBg, vB);
res.b += dot(PoiSHBb, vB);
res += PoiSHC.rgb * (N.x * N.x - N.y * N.y);
float3 l1;
l1.r = dot(PoiSHAr.rgb, N);
l1.g = dot(PoiSHAg.rgb, N);
l1.b = dot(PoiSHAb.rgb, N);
shMax = res + l1;
shMin = res - l1;
// Skipped 2 lines | #if defined(UNITY_COLORSPACE_GAMMA)
// Skipped 2 lines
}
float3 OpenLitComputeCustomLightDirection(float4 lightDirectionOverride)
{
float3 customDir = length(lightDirectionOverride.xyz) * normalize(mul((float3x3)unity_ObjectToWorld, lightDirectionOverride.xyz));
return lightDirectionOverride.w ? customDir : lightDirectionOverride.xyz;
}
float3 OpenLitLightingDirectionForSH9()
{
float3 mainDir = _WorldSpaceLightPos0.xyz * OpenLitLuminance(_LightColor0.rgb);
float3 sh9Dir = PoiSHAr.xyz * 0.333333 + PoiSHAg.xyz * 0.333333 + PoiSHAb.xyz * 0.333333;
float3 sh9DirAbs = float3(sh9Dir.x, abs(sh9Dir.y), sh9Dir.z);
// Skipped 2 lines
float3 lightDirectionForSH9 = sh9Dir + mainDir;
lightDirectionForSH9 = dot(lightDirectionForSH9, lightDirectionForSH9) < 0.000001 ? 0 : normalize(lightDirectionForSH9);
return lightDirectionForSH9;
}
float3 OpenLitLightingDirection(float4 lightDirectionOverride)
{
float3 mainDir = _WorldSpaceLightPos0.xyz * OpenLitLuminance(_LightColor0.rgb);
// Expected defined at 25, got UNITY_SHOU
#if !defined(LIGHTMAP_ON) && UNITY_SHOULD_SAMPLE_SH
float3 sh9Dir = PoiSHAr.xyz * 0.333333 + PoiSHAg.xyz * 0.333333 + PoiSHAb.xyz * 0.333333;
float3 sh9DirAbs = float3(sh9Dir.x, abs(sh9Dir.y), sh9Dir.z);
#else
float3 sh9Dir = 0;
float3 sh9DirAbs = 0;
#endif
float3 customDir = OpenLitComputeCustomLightDirection(lightDirectionOverride);
return normalize(sh9DirAbs + mainDir + customDir);
}
float3 OpenLitLightingDirection()
{
float4 customDir = float4(0.001, 0.002, 0.001, 0.0);
return OpenLitLightingDirection(customDir);
}
inline float4 CalculateFrustumCorrection()
{
float x1 = -UNITY_MATRIX_P._31 / (UNITY_MATRIX_P._11 * UNITY_MATRIX_P._34);
float x2 = -UNITY_MATRIX_P._32 / (UNITY_MATRIX_P._22 * UNITY_MATRIX_P._34);
return float4(x1, x2, 0, UNITY_MATRIX_P._33 / UNITY_MATRIX_P._34 + x1 * UNITY_MATRIX_P._13 + x2 * UNITY_MATRIX_P._23);
}
inline float CorrectedLinearEyeDepth(float z, float correctionFactor)
{
return 1.f / (z / UNITY_MATRIX_P._34 + correctionFactor);
}
float evalRamp4(float time, float4 ramp)
{
return lerp(ramp.x, ramp.y, smoothstep(ramp.z, ramp.w, time));
}
float2 sharpSample(float4 texelSize, float2 p)
{
p = p * texelSize.zw;
float2 c = max(0.0, fwidth(p));
p = floor(p) + saturate(frac(p) / c);
p = (p - 0.5) * texelSize.xy;
return p;
}
void applyToGlobalMask(inout PoiMods poiMods, int index, int blendType, float val)
{
float valBlended = saturate(maskBlend(poiMods.globalMask[index], val, blendType));
switch(index)
{
case 0: poiMods.globalMask[0] = valBlended;
break;
case 1: poiMods.globalMask[1] = valBlended;
break;
case 2: poiMods.globalMask[2] = valBlended;
break;
case 3: poiMods.globalMask[3] = valBlended;
break;
case 4: poiMods.globalMask[4] = valBlended;
break;
case 5: poiMods.globalMask[5] = valBlended;
break;
case 6: poiMods.globalMask[6] = valBlended;
break;
case 7: poiMods.globalMask[7] = valBlended;
break;
case 8: poiMods.globalMask[8] = valBlended;
break;
case 9: poiMods.globalMask[9] = valBlended;
break;
case 10: poiMods.globalMask[10] = valBlended;
break;
case 11: poiMods.globalMask[11] = valBlended;
break;
case 12: poiMods.globalMask[12] = valBlended;
break;
case 13: poiMods.globalMask[13] = valBlended;
break;
case 14: poiMods.globalMask[14] = valBlended;
break;
case 15: poiMods.globalMask[15] = valBlended;
break;
}
}
void assignValueToVectorFromIndex(inout float4 vec, int index, float value)
{
switch(index)
{
case 0: vec[0] = value;
break;
case 1: vec[1] = value;
break;
case 2: vec[2] = value;
break;
case 3: vec[3] = value;
break;
}
}
float3 mod289(float3 x)
{
return x - floor(x * (1.0 / 289.0)) * 289.0;
}
float2 mod289(float2 x)
{
return x - floor(x * (1.0 / 289.0)) * 289.0;
}
float3 permute(float3 x)
{
return mod289(((x * 34.0) + 1.0) * x);
}
float snoise(float2 v)
{
const float4 C = float4(0.211324865405187, 0.366025403784439, - 0.577350269189626, 0.024390243902439);
float2 i = floor(v + dot(v, C.yy));
float2 x0 = v - i + dot(i, C.xx);
float2 i1;
i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
float4 x12 = x0.xyxy + C.xxzz;
x12.xy -= i1;
i = mod289(i);
float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));
float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
m = m * m ;
m = m * m ;
float3 x = 2.0 * frac(p * C.www) - 1.0;
float3 h = abs(x) - 0.5;
float3 ox = floor(x + 0.5);
float3 a0 = x - ox;
m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
float3 g;
g.x = a0.x * x0.x + h.x * x0.y;
g.yz = a0.yz * x12.xz + h.yz * x12.yw;
return 130.0 * dot(m, g);
}
float poiInvertToggle(in float value, in float toggle)
{
return (toggle == 0 ? value : 1 - value);
}
float3 PoiBlendNormal(float3 dstNormal, float3 srcNormal)
{
return float3(dstNormal.xy + srcNormal.xy, dstNormal.z * srcNormal.z);
}
float3 lilTransformDirOStoWS(float3 directionOS, bool doNormalize)
{
if (doNormalize)
return normalize(mul((float3x3)unity_ObjectToWorld, directionOS));
else
return mul((float3x3)unity_ObjectToWorld, directionOS);
}
float2 poiGetWidthAndHeight(Texture2D tex)
{
uint width;
uint height;
tex.GetDimensions(width, height);
return float2(width, height);
}
float2 poiGetWidthAndHeight(Texture2DArray tex)
{
uint width;
uint height;
uint element;
tex.GetDimensions(width, height, element);
return float2(width, height);
}
bool SceneHasReflections()
{
float width;
float height;
unity_SpecCube0.GetDimensions(width, height);
return !(width * height < 2);
}
void applyUnityFog(inout float3 col, float2 fogData)
{
float fogFactor = 1.0;
float depth = UNITY_Z_0_FAR_FROM_CLIPSPACE(fogData.x);
if (unity_FogParams.z != unity_FogParams.w)
{
fogFactor = depth * unity_FogParams.z + unity_FogParams.w;
}
else if (fogData.y)
{
float exponent_val = unity_FogParams.x * depth;
fogFactor = exp2(-exponent_val * exponent_val);
}
else if (unity_FogParams.y != 0.0f)
{
float exponent = unity_FogParams.y * depth;
fogFactor = exp2(-exponent);
}
fixed3 appliedFogColor = unity_FogColor.rgb;
// Skipped 1 lines | #if defined(UNITY_PASS_FORWARDADD)
col.rgb = lerp(appliedFogColor, col.rgb, saturate(fogFactor));
}
void applyReducedRenderClipDistance(inout VertexOut o)
{
if (o.pos.w < _ProjectionParams.y * 1.01 && o.pos.w > 0)
{
#if defined(UNITY_REVERSED_Z)
o.pos.z = o.pos.z * 0.0001 + o.pos.w * 0.999;
#else
o.pos.z = o.pos.z * 0.0001 - o.pos.w * 0.999;
#endif
}
}
inline float sdPlane(float3 p, float3 n, float h)
{
return dot(p, normalize(n)) + h;
}
float3 calcIntrudePos(float3 pos, float3 normalOS, float2 uv)
{
float3 wnormal = UnityObjectToWorldNormal(normalOS);
float3 wpos = mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz;
float3 camDir = -UNITY_MATRIX_V._m20_m21_m22;
float3 camPos = _WorldSpaceCameraPos;
float near = _ProjectionParams.y;
// Skipped 1 lines | #if defined(PROP_UZUMOREMASK) || !defined(OPTIMIZER_ENABLED)
float uzumoreMask = 1;
float maxAmount = 0.1 * uzumoreMask;
float maxBias = 0.001;
float d = sdPlane(wpos - camPos, -camDir, (near + maxBias));
float intrudeAmount = clamp(d, 0, maxAmount);
if (intrudeAmount > 0.0f && dot(camDir, wnormal) < - 0.2)
{
float biasRate = min(1.0f, intrudeAmount / max(maxAmount, 0.00001));
float bias = maxBias * biasRate;
float3 extrude = (intrudeAmount - bias) * camDir;
return mul(unity_WorldToObject, float4(wpos + extrude, 1.0)).xyz;
}
return pos;
}
struct vertexOutputWrapper
{
uint WKVRCOptimizer_MeshMaterialID : WKVRCOptimizer_MeshMaterialID;
VertexOut returnWrappedStruct;
};
struct vertexInputWrapper
{
appdata v;
};
vertexOutputWrapper WKVRCOptimizer_vertexWithWrapper(vertexInputWrapper WKVRCOptimizer_vertexInput)
{
appdata v = WKVRCOptimizer_vertexInput.v;
WKVRCOptimizer_MeshID = ((uint)v.uv0.z >> 12) - 32;
WKVRCOptimizer_MaterialID = 0xFFF & (uint)v.uv0.z;
vertexOutputWrapper WKVRCOptimizer_vertexOutput = (vertexOutputWrapper)0;
WKVRCOptimizer_vertexOutput.WKVRCOptimizer_MeshMaterialID = WKVRCOptimizer_MaterialID | (WKVRCOptimizer_MeshID << 16);
VertexOut returnWrappedStruct = (VertexOut)0;
if (0.5 > _IsActiveMesh32) return WKVRCOptimizer_vertexOutput;
_LightingCap = isnan(asfloat(asuint(WKVRCOptimizer_LightingCap_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingCap : WKVRCOptimizer_LightingCap_ArrayIndex32;
_LightingAdditiveLimit = isnan(asfloat(asuint(WKVRCOptimizer_LightingAdditiveLimit_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingAdditiveLimit : WKVRCOptimizer_LightingAdditiveLimit_ArrayIndex32;
_LightingMinLightBrightness = isnan(asfloat(asuint(WKVRCOptimizer_LightingMinLightBrightness_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingMinLightBrightness : WKVRCOptimizer_LightingMinLightBrightness_ArrayIndex32;
_LightingMonochromatic = isnan(asfloat(asuint(WKVRCOptimizer_LightingMonochromatic_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingMonochromatic : WKVRCOptimizer_LightingMonochromatic_ArrayIndex32;
_LightingAdditiveMonochromatic = isnan(asfloat(asuint(WKVRCOptimizer_LightingAdditiveMonochromatic_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingAdditiveMonochromatic : WKVRCOptimizer_LightingAdditiveMonochromatic_ArrayIndex32;
_SSAOIntensity = isnan(asfloat(asuint(WKVRCOptimizer_SSAOIntensity_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _SSAOIntensity : WKVRCOptimizer_SSAOIntensity_ArrayIndex32;
_LightingEnableLightVolumes = isnan(asfloat(asuint(WKVRCOptimizer_LightingEnableLightVolumes_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingEnableLightVolumes : WKVRCOptimizer_LightingEnableLightVolumes_ArrayIndex32;
_SSAOAnimationToggle = isnan(asfloat(asuint(WKVRCOptimizer_SSAOAnimationToggle_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _SSAOAnimationToggle : WKVRCOptimizer_SSAOAnimationToggle_ArrayIndex32;
_MainHueShift = isnan(asfloat(asuint(WKVRCOptimizer_MainHueShift_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _MainHueShift : WKVRCOptimizer_MainHueShift_ArrayIndex32;
_Saturation = isnan(asfloat(asuint(WKVRCOptimizer_Saturation_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _Saturation : WKVRCOptimizer_Saturation_ArrayIndex32;
UNITY_SETUP_INSTANCE_ID(v);
VertexOut o;
PoiInitStruct(VertexOut, o);
UNITY_TRANSFER_INSTANCE_ID(v, o);
#ifdef POI_TESSELLATED
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
#endif
UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#ifdef POI_AUDIOLINK
float vertexAudioLink[5];
vertexAudioLink[0] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 0))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 0))[0];
vertexAudioLink[1] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 1))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 1))[0];
vertexAudioLink[2] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 2))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 2))[0];
vertexAudioLink[3] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 3))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 3))[0];
vertexAudioLink[4] = AudioLinkData(ALPASS_GENERALVU + float2(8, 0))[0];
#endif
if (1.0)
{
v.vertex.xyz = calcIntrudePos(v.vertex.xyz, v.normal, vertexUV(v, 3.0));
}
o.normal = UnityObjectToWorldNormal(v.normal);
o.tangent.xyz = UnityObjectToWorldDir(v.tangent);
o.tangent.w = v.tangent.w;
o.vertexColor = v.color;
o.uv[0] = float4(v.uv0.xy.xy, v.uv1.xy);
o.uv[1] = float4(v.uv2.xy, v.uv3.xy);
// Skipped 1 lines | #if defined(LIGHTMAP_ON)
// Skipped 1 lines | #ifdef DYNAMICLIGHTMAP_ON
o.localPos = v.vertex;
o.worldPos = mul(unity_ObjectToWorld, o.localPos);
float3 localOffset = float3(0, 0, 0);
float3 worldOffset = float3(0, 0, 0);
o.localPos.rgb += localOffset;
o.worldPos.rgb += worldOffset;
o.pos = UnityObjectToClipPos(o.localPos);
o.fogData.x = o.pos.z;
#ifdef FOG_EXP2
o.fogData.y = 1;
#else
o.fogData.y = 0;
#endif
#ifndef FORWARD_META_PASS
// Skipped 1 lines | #if !defined(UNITY_PASS_SHADOWCASTER)
v.vertex.xyz = o.localPos.xyz;
TRANSFER_SHADOW_CASTER_NOPOS(o, o.pos);
#endif
o.worldDir = dot(o.pos, CalculateFrustumCorrection());
if (0.0)
{
applyReducedRenderClipDistance(o);
}
#ifdef POI_PASS_META
o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);
#endif
#ifdef POI_PASS_LILFUR
#endif
{
returnWrappedStruct = o;
WKVRCOptimizer_vertexOutput.returnWrappedStruct = returnWrappedStruct;
return WKVRCOptimizer_vertexOutput;
}
}
VertexOut vert(appdata v)
{
UNITY_SETUP_INSTANCE_ID(v);
VertexOut o;
PoiInitStruct(VertexOut, o);
UNITY_TRANSFER_INSTANCE_ID(v, o);
#ifdef POI_TESSELLATED
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
#endif
UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#ifdef POI_AUDIOLINK
float vertexAudioLink[5];
vertexAudioLink[0] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 0))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 0))[0];
vertexAudioLink[1] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 1))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 1))[0];
vertexAudioLink[2] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 2))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 2))[0];
vertexAudioLink[3] = 0.0 == 0 ? AudioLinkData(ALPASS_AUDIOLINK + float2(0, 3))[0] : AudioLinkData(ALPASS_FILTEREDAUDIOLINK + float2((1 - 0.0) * 15.95, 3))[0];
vertexAudioLink[4] = AudioLinkData(ALPASS_GENERALVU + float2(8, 0))[0];
#endif
if (1.0)
{
v.vertex.xyz = calcIntrudePos(v.vertex.xyz, v.normal, vertexUV(v, 3.0));
}
o.normal = UnityObjectToWorldNormal(v.normal);
o.tangent.xyz = UnityObjectToWorldDir(v.tangent);
o.tangent.w = v.tangent.w;
o.vertexColor = v.color;
o.uv[0] = float4(v.uv0.xy.xy, v.uv1.xy);
o.uv[1] = float4(v.uv2.xy, v.uv3.xy);
// Skipped 1 lines | #if defined(LIGHTMAP_ON)
// Skipped 1 lines | #ifdef DYNAMICLIGHTMAP_ON
o.localPos = v.vertex;
o.worldPos = mul(unity_ObjectToWorld, o.localPos);
float3 localOffset = float3(0, 0, 0);
float3 worldOffset = float3(0, 0, 0);
o.localPos.rgb += localOffset;
o.worldPos.rgb += worldOffset;
o.pos = UnityObjectToClipPos(o.localPos);
o.fogData.x = o.pos.z;
#ifdef FOG_EXP2
o.fogData.y = 1;
#else
o.fogData.y = 0;
#endif
#ifndef FORWARD_META_PASS
// Skipped 1 lines | #if !defined(UNITY_PASS_SHADOWCASTER)
v.vertex.xyz = o.localPos.xyz;
TRANSFER_SHADOW_CASTER_NOPOS(o, o.pos);
#endif
o.worldDir = dot(o.pos, CalculateFrustumCorrection());
if (0.0)
{
applyReducedRenderClipDistance(o);
}
#ifdef POI_PASS_META
o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);
#endif
#ifdef POI_PASS_LILFUR
#endif
return o;
}
#define POI2D_SAMPLER_STOCHASTIC(tex, texSampler, uv, useStochastic) (useStochastic ? DeliotHeitzSampleTexture(tex, sampler##texSampler, uv) : POI2D_SAMPLER(tex, texSampler, uv))
#define POI2D_SAMPLER_PAN_STOCHASTIC(tex, texSampler, uv, pan, useStochastic) (useStochastic ? DeliotHeitzSampleTexture(tex, sampler##texSampler, POI_PAN_UV(uv, pan)) : POI2D_SAMPLER_PAN(tex, texSampler, uv, pan))
#define POI2D_SAMPLER_PANGRAD_STOCHASTIC(tex, texSampler, uv, pan, dx, dy, useStochastic) (useStochastic ? DeliotHeitzSampleTexture(tex, sampler##texSampler, POI_PAN_UV(uv, pan), dx, dy) : POI2D_SAMPLER_PANGRAD(tex, texSampler, uv, pan, dx, dy))
#if !defined(_STOCHASTICMODE_NONE)
float2 StochasticHash2D2D(float2 s)
{
return frac(sin(glsl_mod(float2(dot(s, float2(127.1, 311.7)), dot(s, float2(269.5, 183.3))), 3.14159)) * 43758.5453);
}
#endif
float3x3 DeliotHeitzStochasticUVBW(float2 uv)
{
const float2x2 stochasticSkewedGrid = float2x2(1.0, -0.57735027, 0.0, 1.15470054);
float2 skewUV = mul(stochasticSkewedGrid, uv * 3.4641 * 1.0);
float2 vxID = floor(skewUV);
float3 bary = float3(frac(skewUV), 0);
bary.z = 1.0 - bary.x - bary.y;
float3x3 pos = float3x3( float3(vxID, bary.z), float3(vxID + float2(0, 1), bary.y), float3(vxID + float2(1, 0), bary.x) );
float3x3 neg = float3x3( float3(vxID + float2(1, 1), -bary.z), float3(vxID + float2(1, 0), 1.0 - bary.y), float3(vxID + float2(0, 1), 1.0 - bary.x) );
return (bary.z > 0) ? pos : neg;
}
float4 DeliotHeitzSampleTexture(Texture2D tex, SamplerState texSampler, float2 uv, float2 dx, float2 dy)
{
float3x3 UVBW = DeliotHeitzStochasticUVBW(uv);
return mul(tex.SampleGrad(texSampler, uv + StochasticHash2D2D(UVBW[0].xy), dx, dy), UVBW[0].z) + mul(tex.SampleGrad(texSampler, uv + StochasticHash2D2D(UVBW[1].xy), dx, dy), UVBW[1].z) + mul(tex.SampleGrad(texSampler, uv + StochasticHash2D2D(UVBW[2].xy), dx, dy), UVBW[2].z) ;
}
float4 DeliotHeitzSampleTexture(Texture2D tex, SamplerState texSampler, float2 uv)
{
float2 dx = ddx(uv), dy = ddy(uv);
return DeliotHeitzSampleTexture(tex, texSampler, uv, dx, dy);
}
void applyAlphaOptions(inout PoiFragData poiFragData, in PoiMesh poiMesh, in PoiCam poiCam, in PoiMods poiMods)
{
poiFragData.alpha = saturate(poiFragData.alpha + 0.0);
if (0.0 > 0)
{
poiFragData.alpha = maskBlend(poiFragData.alpha, poiMods.globalMask[0.0 - 1], 2.0);
}
}
void ApplyGlobalMaskModifiers(in PoiMesh poiMesh, inout PoiMods poiMods, in PoiCam poiCam)
{
}
float2 calculatePolarCoordinate(in PoiMesh poiMesh)
{
float2 delta = poiMesh.uv[0.0] - float4(0.5,0.5,0,0);
float radius = length(delta) * 2 * 1.0;
float angle = atan2(delta.x, delta.y);
float phi = angle / (UNITY_PI * 2.0);
float phi_frac = frac(phi);
angle = fwidth(phi) - 0.0001 < fwidth(phi_frac) ? phi : phi_frac;
angle *= 1.0;
return float2(radius, angle + distance(poiMesh.uv[0.0], float4(0.5,0.5,0,0)) * 0.0);
}
float2 MonoPanoProjection(float3 coords)
{
float3 normalizedCoords = normalize(coords);
float latitude = acos(normalizedCoords.y);
float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
float phi = longitude / (UNITY_PI * 2.0);
float phi_frac = frac(phi);
longitude = fwidth(phi) - 0.0001 < fwidth(phi_frac) ? phi : phi_frac;
longitude *= 2;
float2 sphereCoords = float2(longitude, latitude) * float2(1.0, 1.0 / UNITY_PI);
sphereCoords = float2(1.0, 1.0) - sphereCoords;
return (sphereCoords + float4(0, 1 - unity_StereoEyeIndex, 1, 1.0).xy) * float4(0, 1 - unity_StereoEyeIndex, 1, 1.0).zw;
}
float2 StereoPanoProjection(float3 coords)
{
float3 normalizedCoords = normalize(coords);
float latitude = acos(normalizedCoords.y);
float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
float phi = longitude / (UNITY_PI * 2.0);
float phi_frac = frac(phi);
longitude = fwidth(phi) - 0.0001 < fwidth(phi_frac) ? phi : phi_frac;
longitude *= 2;
float2 sphereCoords = float2(longitude, latitude) * float2(0.5, 1.0 / UNITY_PI);
sphereCoords = float2(0.5, 1.0) - sphereCoords;
return (sphereCoords + float4(0, 1 - unity_StereoEyeIndex, 1, 0.5).xy) * float4(0, 1 - unity_StereoEyeIndex, 1, 0.5).zw;
}
float2 calculateWorldUV(in PoiMesh poiMesh)
{
return float2(0.0 != 3 ? poiMesh.worldPos[ 0.0] : 0.0f, 2.0 != 3 ? poiMesh.worldPos[2.0] : 0.0f);
}
float2 calculatelocalUV(in PoiMesh poiMesh)
{
float localUVs[8];
localUVs[0] = poiMesh.localPos.x;
localUVs[1] = poiMesh.localPos.y;
localUVs[2] = poiMesh.localPos.z;
localUVs[3] = 0;
localUVs[4] = poiMesh.vertexColor.r;
localUVs[5] = poiMesh.vertexColor.g;
localUVs[6] = poiMesh.vertexColor.b;
localUVs[7] = poiMesh.vertexColor.a;
return float2(localUVs[0.0],localUVs[1.0]);
}
float2 calculatePanosphereUV(in PoiMesh poiMesh)
{
float3 viewDirection = normalize(lerp(getCameraPosition().xyz, _WorldSpaceCameraPos.xyz, 1.0) - poiMesh.worldPos.xyz) * - 1;
return lerp(MonoPanoProjection(viewDirection), StereoPanoProjection(viewDirection), 0.0);
}
struct fragmentInputWrapper
{
uint WKVRCOptimizer_MeshMaterialID : WKVRCOptimizer_MeshMaterialID;
VertexOut i;
uint facing : SV_IsFrontFace;
};
float4 frag(
fragmentInputWrapper WKVRCOptimizer_fragmentInput
) : SV_Target
{
VertexOut i = WKVRCOptimizer_fragmentInput.i;
uint facing = WKVRCOptimizer_fragmentInput.facing;
WKVRCOptimizer_MaterialID = WKVRCOptimizer_fragmentInput.WKVRCOptimizer_MeshMaterialID & 0xFFFF;
WKVRCOptimizer_MeshID = WKVRCOptimizer_fragmentInput.WKVRCOptimizer_MeshMaterialID >> 16;
_LightingCap = isnan(asfloat(asuint(WKVRCOptimizer_LightingCap_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingCap : WKVRCOptimizer_LightingCap_ArrayIndex32;
_LightingAdditiveLimit = isnan(asfloat(asuint(WKVRCOptimizer_LightingAdditiveLimit_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingAdditiveLimit : WKVRCOptimizer_LightingAdditiveLimit_ArrayIndex32;
_LightingMinLightBrightness = isnan(asfloat(asuint(WKVRCOptimizer_LightingMinLightBrightness_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingMinLightBrightness : WKVRCOptimizer_LightingMinLightBrightness_ArrayIndex32;
_LightingMonochromatic = isnan(asfloat(asuint(WKVRCOptimizer_LightingMonochromatic_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingMonochromatic : WKVRCOptimizer_LightingMonochromatic_ArrayIndex32;
_LightingAdditiveMonochromatic = isnan(asfloat(asuint(WKVRCOptimizer_LightingAdditiveMonochromatic_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingAdditiveMonochromatic : WKVRCOptimizer_LightingAdditiveMonochromatic_ArrayIndex32;
_SSAOIntensity = isnan(asfloat(asuint(WKVRCOptimizer_SSAOIntensity_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _SSAOIntensity : WKVRCOptimizer_SSAOIntensity_ArrayIndex32;
_LightingEnableLightVolumes = isnan(asfloat(asuint(WKVRCOptimizer_LightingEnableLightVolumes_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _LightingEnableLightVolumes : WKVRCOptimizer_LightingEnableLightVolumes_ArrayIndex32;
_SSAOAnimationToggle = isnan(asfloat(asuint(WKVRCOptimizer_SSAOAnimationToggle_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _SSAOAnimationToggle : WKVRCOptimizer_SSAOAnimationToggle_ArrayIndex32;
_MainHueShift = isnan(asfloat(asuint(WKVRCOptimizer_MainHueShift_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _MainHueShift : WKVRCOptimizer_MainHueShift_ArrayIndex32;
_Saturation = isnan(asfloat(asuint(WKVRCOptimizer_Saturation_ArrayIndex32.x) ^ asuint(WKVRCOptimizer_Zero))) ? _Saturation : WKVRCOptimizer_Saturation_ArrayIndex32;
UNITY_SETUP_INSTANCE_ID(i);
UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
PoiSHAr = unity_SHAr;
PoiSHAg = unity_SHAg;
PoiSHAb = unity_SHAb;
PoiSHBr = unity_SHBr;
PoiSHBg = unity_SHBg;
PoiSHBb = unity_SHBb;
PoiSHC =  unity_SHC;
PoiMesh poiMesh;
PoiInitStruct(PoiMesh, poiMesh);
PoiLight poiLight;
PoiInitStruct(PoiLight, poiLight);
PoiVertexLights poiVertexLights;
PoiInitStruct(PoiVertexLights, poiVertexLights);
PoiCam poiCam;
PoiInitStruct(PoiCam, poiCam);
PoiMods poiMods;
PoiInitStruct(PoiMods, poiMods);
poiMods.globalEmission = 1;
PoiFragData poiFragData;
poiFragData.smoothness = 1;
poiFragData.smoothness2 = 1;
poiFragData.metallic = 1;
poiFragData.specularMask = 1;
poiFragData.reflectionMask = 1;
poiFragData.emission = 0;
poiFragData.baseColor = float3(0, 0, 0);
poiFragData.finalColor = float3(0, 0, 0);
poiFragData.alpha = 1;
poiFragData.toggleVertexLights = 0;
#ifdef POI_UDIMDISCARD
applyUDIMDiscard(i, facing);
#endif
poiMesh.objectPosition = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
poiMesh.objNormal = mul(unity_WorldToObject, i.normal);
poiMesh.normals[0] = i.normal;
poiMesh.tangent[0] = i.tangent.xyz;
poiMesh.binormal[0] = cross(i.normal, i.tangent.xyz) * (i.tangent.w * unity_WorldTransformParams.w);
poiMesh.worldPos = i.worldPos.xyz;
poiMesh.localPos = i.localPos.xyz;
poiMesh.vertexColor = i.vertexColor;
poiMesh.isFrontFace = facing;
poiMesh.dx = ddx(poiMesh.uv[0]);
poiMesh.dy = ddy(poiMesh.uv[0]);
poiMesh.isRightHand = i.tangent.w > 0.0;
#ifndef POI_PASS_OUTLINE
if (!poiMesh.isFrontFace && 1)
{
poiMesh.normals[0] *= -1;
poiMesh.tangent[0] *= -1;
poiMesh.binormal[0] *= -1;
}
#endif
poiCam.viewDir = !IsOrthographicCamera() ? normalize(_WorldSpaceCameraPos - i.worldPos.xyz) : normalize(UNITY_MATRIX_I_V._m02_m12_m22);
float3 tanToWorld0 = float3(poiMesh.tangent[0].x, poiMesh.binormal[0].x, poiMesh.normals[0].x);
float3 tanToWorld1 = float3(poiMesh.tangent[0].y, poiMesh.binormal[0].y, poiMesh.normals[0].y);
float3 tanToWorld2 = float3(poiMesh.tangent[0].z, poiMesh.binormal[0].z, poiMesh.normals[0].z);
float3 ase_tanViewDir = tanToWorld0 * poiCam.viewDir.x + tanToWorld1 * poiCam.viewDir.y + tanToWorld2 * poiCam.viewDir.z;
poiCam.tangentViewDir = normalize(ase_tanViewDir);
// Skipped 1 lines | #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
poiMesh.parallaxUV = poiCam.tangentViewDir.xy / max(poiCam.tangentViewDir.z, 0.0001);
poiMesh.uv[0] = i.uv[0].xy;
poiMesh.uv[1] = i.uv[0].zw;
poiMesh.uv[2] = i.uv[1].xy;
poiMesh.uv[3] = i.uv[1].zw;
poiMesh.uv[4] = poiMesh.uv[0];
poiMesh.uv[5] = poiMesh.uv[0];
poiMesh.uv[6] = poiMesh.uv[0];
poiMesh.uv[7] = poiMesh.uv[0];
poiMesh.uv[8] = poiMesh.uv[0];
poiMesh.uv[4] = calculatePanosphereUV(poiMesh);
poiMesh.uv[5] = calculateWorldUV(poiMesh);
poiMesh.uv[6] = calculatePolarCoordinate(poiMesh);
poiMesh.uv[8] = calculatelocalUV(poiMesh);
float3 worldViewUp = normalize(float3(0, 1, 0) - poiCam.viewDir * dot(poiCam.viewDir, float3(0, 1, 0)));
float3 worldViewRight = normalize(cross(poiCam.viewDir, worldViewUp));
poiMesh.uv[9] = float2(dot(worldViewRight, poiMesh.normals[0]), dot(worldViewUp, poiMesh.normals[0])) * 0.5 + 0.5;
poiMods.globalMask[0] = 1;
poiMods.globalMask[1] = 1;
poiMods.globalMask[2] = 1;
poiMods.globalMask[3] = 1;
poiMods.globalMask[4] = 1;
poiMods.globalMask[5] = 1;
poiMods.globalMask[6] = 1;
poiMods.globalMask[7] = 1;
poiMods.globalMask[8] = 1;
poiMods.globalMask[9] = 1;
poiMods.globalMask[10] = 1;
poiMods.globalMask[11] = 1;
poiMods.globalMask[12] = 1;
poiMods.globalMask[13] = 1;
poiMods.globalMask[14] = 1;
poiMods.globalMask[15] = 1;
ApplyGlobalMaskModifiers(poiMesh, poiMods, poiCam);
float2 mainUV = poiUV(poiMesh.uv[0.0].xy, float4(1,1,0,0));
if (0.0)
{
mainUV = sharpSample(float4(0.0009765625,0.0009765625,1024,1024), mainUV);
}
float4 mainTexture = POI2D_SAMPLER_PAN_STOCHASTIC(_MainTex, _MainTex, mainUV, float4(0,0,0,0), 0.0);
mainTexture.a = max(mainTexture.a, 0.0);
poiMesh.tangentSpaceNormal = UnpackScaleNormal(POI2D_SAMPLER_PAN_STOCHASTIC(_BumpMap, _MainTex, poiUV(poiMesh.uv[0.0].xy, float4(1,1,0,0)), float4(0,0,0,0), 0.0), 1.0);
// Skipped 1 lines
float3 tangentSpaceNormal = UnpackNormal(float4(0.5, 0.5, 1, 1));
poiMesh.normals[0] = normalize( tangentSpaceNormal.x * poiMesh.tangent[0] + tangentSpaceNormal.y * poiMesh.binormal[0] + tangentSpaceNormal.z * poiMesh.normals[0]
);
poiMesh.normals[1] = normalize( poiMesh.tangentSpaceNormal.x * poiMesh.tangent[0] + poiMesh.tangentSpaceNormal.y * poiMesh.binormal[0] + poiMesh.tangentSpaceNormal.z * poiMesh.normals[0]
);
poiMesh.tangent[1] = cross(poiMesh.binormal[0], -poiMesh.normals[1]);
poiMesh.binormal[1] = cross(-poiMesh.normals[1], poiMesh.tangent[0]);
poiCam.forwardDir = getCameraForward();
poiCam.worldPos = _WorldSpaceCameraPos;
poiCam.reflectionDir = reflect(-poiCam.viewDir, poiMesh.normals[1]);
poiCam.vertexReflectionDir = reflect(-poiCam.viewDir, poiMesh.normals[0]);
poiCam.clipPos = i.pos;
poiCam.distanceToVert = distance(poiMesh.worldPos, poiCam.worldPos);
poiCam.posScreenSpace = poiTransformClipSpacetoScreenSpaceFrag(poiCam.clipPos);
#if defined(POI_GRABPASS) && defined(POI_PASS_BASE)
poiCam.screenUV = poiCam.clipPos.xy / poiGetWidthAndHeight(_PoiGrab2);
#else
poiCam.screenUV = poiCam.clipPos.xy / _ScreenParams.xy;
#endif
#ifdef UNITY_SINGLE_PASS_STEREO
poiCam.posScreenSpace.x = poiCam.posScreenSpace.x * 0.5;
#endif
poiCam.posScreenPixels = calcPixelScreenUVs(poiCam.posScreenSpace);
poiCam.vDotN = abs(dot(poiCam.viewDir, poiMesh.normals[1]));
poiCam.worldDirection.xyz = poiMesh.worldPos.xyz - poiCam.worldPos;
poiCam.worldDirection.w = i.worldDir;
poiFragData.baseColor = mainTexture.rgb;
#if !defined(POI_PASS_BASETWO) && !defined(POI_PASS_ADDTWO)
poiFragData.baseColor *= poiThemeColor(poiMods, float4(1,1,1,1).rgb, 0.0);
poiFragData.alpha = mainTexture.a * float4(1,1,1,1).a;
#else
poiFragData.baseColor *= poiThemeColor(poiMods, _TwoPassColor.rgb, _TwoPassColorThemeIndex);
poiFragData.alpha = mainTexture.a * _TwoPassColor.a;
#endif
// Skipped 1 lines | #if defined(PROP_MAINCOLORADJUSTTEXTURE) || !defined(OPTIMIZER_ENABLED)
float4 hueShiftAlpha = 1;
if (0.0 > 0)
{
hueShiftAlpha.r = maskBlend(hueShiftAlpha.r, poiMods.globalMask[0.0 - 1], 2.0);
}
if (0.0 > 0)
{
hueShiftAlpha.b = maskBlend(hueShiftAlpha.b, poiMods.globalMask[0.0 - 1], 2.0);
}
if (0.0 > 0)
{
hueShiftAlpha.g = maskBlend(hueShiftAlpha.g, poiMods.globalMask[0.0 - 1], 2.0);
}
if (0.0 > 0)
{
hueShiftAlpha.a = maskBlend(hueShiftAlpha.a, poiMods.globalMask[0.0 - 1], 2.0);
}
if (1.0 == 1)
{
float shift = _MainHueShift;
#ifdef POI_AUDIOLINK
if (poiMods.audioLinkAvailable && 0.0)
{
shift += AudioLinkGetChronoTime(0.0, 0.0) * 1.0;
}
#endif
if (1.0)
{
poiFragData.baseColor = lerp(poiFragData.baseColor, hueShift(poiFragData.baseColor, shift + _MainHueShiftSpeed * _Time.x, 0.0, 1.0), hueShiftAlpha.r);
}
else
{
poiFragData.baseColor = hueShift(poiFragData.baseColor, frac((shift - (1 - hueShiftAlpha.r) + _MainHueShiftSpeed * _Time.x)), 0.0, 1.0);
}
}
if (0.0 && 0.0)
{
float3 tempColor = OpenLitLinearToSRGB(poiFragData.baseColor);
// Skipped 1 lines
#if defined(PROP_MAINGRADATIONTEX)
tempColor.r = POI_SAMPLE_1D_X(_MainGradationTex, sampler_linear_clamp, tempColor.r).r;
tempColor.g = POI_SAMPLE_1D_X(_MainGradationTex, sampler_linear_clamp, tempColor.g).g;
tempColor.b = POI_SAMPLE_1D_X(_MainGradationTex, sampler_linear_clamp, tempColor.b).b;
#else
tempColor = float3(1, 1, 1);
#endif
tempColor = OpenLitSRGBToLinear(tempColor);
poiFragData.baseColor = lerp(poiFragData.baseColor, tempColor, 0.0);
}
poiFragData.baseColor = lerp(poiFragData.baseColor, pow(abs(poiFragData.baseColor), 1.0), hueShiftAlpha.a);
poiFragData.baseColor = lerp(poiFragData.baseColor, dot(poiFragData.baseColor, float3(0.3, 0.59, 0.11)), - (_Saturation) * hueShiftAlpha.b);
poiFragData.baseColor = saturate(lerp(poiFragData.baseColor, poiFragData.baseColor * (0.0 + 1), hueShiftAlpha.g));
if (2.0)
{
// Skipped 1 lines | #if defined(PROP_ALPHAMASK) || !defined(OPTIMIZER_ENABLED)
float alphaMask = 1;
alphaMask = saturate(alphaMask * 1.0 + (0.0 ? 0.0 * - 1 : 0.0));
if (0.0) alphaMask = 1 - alphaMask;
if (2.0 == 1) poiFragData.alpha = alphaMask;
if (2.0 == 2) poiFragData.alpha = poiFragData.alpha * alphaMask;
if (2.0 == 3) poiFragData.alpha = saturate(poiFragData.alpha + alphaMask);
if (2.0 == 4) poiFragData.alpha = saturate(poiFragData.alpha - alphaMask);
}
applyAlphaOptions(poiFragData, poiMesh, poiCam, poiMods);
poiFragData.finalColor = poiFragData.baseColor;
#if !defined(POI_PASS_BASETWO) && !defined(POI_PASS_ADDTWO)
poiFragData.alpha = 0.0 ? 1 : poiFragData.alpha;
#else
poiFragData.alpha = _AlphaForceOpaque2 ? 1 : poiFragData.alpha;
#endif
if (9.0 == POI_MODE_OPAQUE)
{
poiFragData.alpha = 1;
}
clip(poiFragData.alpha - 0.01);
applyUnityFog(poiFragData.finalColor, i.fogData);
if (WKVRCOptimizer_Zero)
{
float WKVRCOptimizer_sum = 0;
#ifdef DUMMY_USE_TEXTURE_TO_PRESERVE_SAMPLER__MainTex
WKVRCOptimizer_sum += _MainTex.Load(0).x;
#endif
if (WKVRCOptimizer_sum) return (float4)0;
}
return float4(poiFragData.finalColor, poiFragData.alpha) + POI_SAFE_RGB0;
}
