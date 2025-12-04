#ifndef UNIVERSAL_AVATAR_CORE_HLSL
#define UNIVERSAL_AVATAR_CORE_HLSL

#include "UnityInstancing.cginc"

// Texture Arrays
UNITY_DECLARE_TEX2DARRAY(_BaseMap);
UNITY_DECLARE_TEX2DARRAY(_NormalMap);
UNITY_DECLARE_TEX2DARRAY(_MetallicGlossMap);
UNITY_DECLARE_TEX2DARRAY(_ShadeMap);
UNITY_DECLARE_TEX2DARRAY(_RampTexture);
UNITY_DECLARE_TEX2DARRAY(_MatcapTexture);
UNITY_DECLARE_TEX2DARRAY(_MatcapTexture2);
UNITY_DECLARE_TEX2DARRAY(_OutlineMask);
UNITY_DECLARE_TEX2DARRAY(_EmissionMap);
UNITY_DECLARE_TEX2DARRAY(_DissolveMask);
UNITY_DECLARE_TEX2DARRAY(_DetailMap);

// Properties
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _NormalScale;
    float _MetallicStrength;
    float _Smoothness;
    float4 _ShadeColor;
    float _ShadowThreshold;
    float _ShadowSmooth;
    float4 _MatcapColor;
    float _UseMatcapSecond;
    float4 _RimColor;
    float _RimPower;
    float _RimIntensity;
    float4 _OutlineColor;
    float _OutlineWidth;
    float _OutlineScreenSpace;
    float4 _EmissionColor;
    float _EmissionIntensity;
    float _DissolveAmount;
    float _DetailScale;
    
    // Internal properties
    float _WKVRCOpt_MeshID;
CBUFFER_END

// Input structures
struct appdata_full
{
    float4 vertex : POSITION;
    float4 tangent : TANGENT;
    float3 normal : NORMAL;
    float4 texcoord : TEXCOORD0; // xy = uv, z = Material ID (packed)
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f_full
{
    float4 pos : SV_POSITION;
    float3 uv : TEXCOORD0; // xy = uv, z = Material ID
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float3 worldPos : TEXCOORD1;
    float3 worldNormal : TEXCOORD2;
    float3 worldTangent : TEXCOORD3;
    float3 worldBinormal : TEXCOORD4;
    float4 screenPos : TEXCOORD5;
    UNITY_FOG_COORDS(6)
    LIGHTING_COORDS(7,8)
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// --- Vertex Shader ---
v2f_full vert (appdata_full v)
{
    v2f_full o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    // Pass Material ID from texcoord.z to fragment
    // Note: MaterialOptimizer packs ID into z. 
    // For now, we assume raw z value corresponds to the array index.
    o.uv = v.texcoord.xyz; 

    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
    o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w;

    UNITY_TRANSFER_FOG(o, o.pos);
    TRANSFER_VERTEX_TO_FRAGMENT(o);

    o.screenPos = ComputeScreenPos(o.pos);

    return o;
}

// --- Fragment Shader ---
float4 frag (v2f_full i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);

    // Material ID from vertex stream
    // Using the integer part of uv.z as the index.
    // The optimizer packs it as 'sourceUv.z + internalMaterialID'.
    // Assuming sourceUv.z was 0 or mesh ID, we need to be careful.
    // For the Universal pipeline, we will enforce uv.z = Material Index.
    float materialIdx = i.uv.z; 

    // Sample Textures using materialIdx
    float4 baseColorTex = UNITY_SAMPLE_TEX2DARRAY(_BaseMap, float3(i.uv.xy, materialIdx));
    float4 finalColor = baseColorTex * _BaseColor;

    #if _USE_NORMAL_MAP
        float4 normalTex = UNITY_SAMPLE_TEX2DARRAY(_NormalMap, float3(i.uv.xy, materialIdx));
        float3 normal = UnpackNormal(normalTex);
        normal = normalize(mul(float3x3(i.worldTangent, i.worldBinormal, i.worldNormal), normal * float3(_NormalScale, _NormalScale, 1.0)));
    #else
        float3 normal = i.worldNormal;
    #endif
    normal = normalize(normal);

    #if _USE_METALLIC_GLOSS_MAP
        float4 metallicGlossTex = UNITY_SAMPLE_TEX2DARRAY(_MetallicGlossMap, float3(i.uv.xy, materialIdx));
        float metallic = metallicGlossTex.r * _MetallicStrength;
        float smoothness = metallicGlossTex.a * _Smoothness;
    #else
        float metallic = _MetallicStrength;
        float smoothness = _Smoothness;
    #endif

    // --- Lighting Calculation (PBR & Toon Hybrid) ---
    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos.xyz);
    
    // Ambient Light
    float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb;

    // Directional Light
    float NdotL = dot(normal, lightDir);
    float NdotV = dot(normal, viewDir);
    float3 lightColor = _LightColor0.rgb;

    float3 diffuse = finalColor.rgb;
    float3 specular = 0;

    #if _SHADING_MODE_PBR
        diffuse = finalColor.rgb * (1.0 - metallic);
        specular = metallic * lightColor.rgb * (NdotL > 0 ? 1 : 0); 
    #endif

    #if _SHADING_MODE_TOON
        float toonLight = step(_ShadowThreshold, NdotL);
        #if _USE_RAMP_TEXTURE
            float2 rampUV = float2(NdotL * 0.5 + 0.5, 0.5);
            toonLight = UNITY_SAMPLE_TEX2DARRAY(_RampTexture, float3(rampUV, materialIdx)).r;
        #endif

        float3 toonShade = UNITY_SAMPLE_TEX2DARRAY(_ShadeMap, float3(i.uv.xy, materialIdx)).rgb * _ShadeColor.rgb;
        diffuse = lerp(toonShade, finalColor.rgb, toonLight);
    #endif

    float3 bakedGI = SHADOW_ATTENUATION(i) * lightColor.rgb;
    float3 finalLighting = ambient + diffuse * bakedGI;


    // --- Matcap ---
    #if _USE_MATCAP_TEXTURE
        float3 matcapNormal = mul((float3x3)UNITY_MATRIX_V, normal);
        matcapNormal = normalize(matcapNormal);
        float2 matcapUV = matcapNormal.xy * 0.5 + 0.5;
        float3 matcap = UNITY_SAMPLE_TEX2DARRAY(_MatcapTexture, float3(matcapUV, materialIdx)).rgb * _MatcapColor.rgb;
        finalLighting += matcap;

        #if _USE_MATCAP_SECOND
            float3 matcap2 = UNITY_SAMPLE_TEX2DARRAY(_MatcapTexture2, float3(matcapUV, materialIdx)).rgb;
            finalLighting = lerp(finalLighting, matcap2, _UseMatcapSecond);
        #endif
    #endif

    // --- Rim Lighting ---
    #if _USE_RIM_LIGHTING
        float rim = 1.0 - saturate(dot(normal, viewDir));
        rim = pow(rim, _RimPower) * _RimIntensity;
        finalLighting += _RimColor.rgb * rim;
    #endif

    // --- Emission ---
    #if _USE_EMISSION
        float3 emission = UNITY_SAMPLE_TEX2DARRAY(_EmissionMap, float3(i.uv.xy, materialIdx)).rgb * _EmissionColor.rgb * _EmissionIntensity;
        finalLighting += emission;
    #endif

    // --- Final Color Assembly ---
    float4 outputColor = float4(finalLighting, finalColor.a);

    // --- Dissolve ---
    #if _USE_DISSOLVE
        float dissolveMask = UNITY_SAMPLE_TEX2DARRAY(_DissolveMask, float3(i.uv.xy, materialIdx)).r;
        clip(dissolveMask - _DissolveAmount);
    #endif

    // --- Transparency ---
    #if _TRANSPARENCY_CUTOUT
        clip(outputColor.a - 0.5);
    #endif

    UNITY_APPLY_FOG(i.fogCoord, outputColor);
    return outputColor;
}

#endif // UNIVERSAL_AVATAR_CORE_HLSL