Shader "WKAvatarOptimizer/UniversalAvatar"
{
    Properties
    {
        [Header(Base Properties)]
        _BaseMap ("Albedo (RGB) / Alpha (A)", 2DArray) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _NormalMap ("Normal Map", 2DArray) = "bump" {}
        _NormalScale ("Normal Scale", Float) = 1.0

        _MetallicGlossMap ("Metallic (R) / Smoothness (A)", 2DArray) = "white" {}
        _MetallicStrength ("Metallic Strength", Range(0.0, 1.0)) = 0.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5

        [Header(Toon Shading)]
        _ShadeMap ("Shade Map", 2DArray) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.5,0.5,0.5,1)
        _RampTexture ("Shade Ramp (RGB)", 2DArray) = "white" {}
        _ShadowThreshold ("Shadow Threshold", Range(0.0, 1.0)) = 0.5
        _ShadowSmooth ("Shadow Smoothness", Range(0.0, 1.0)) = 0.1

        [Header(Matcap)]
        _MatcapTexture ("Matcap", 2DArray) = "black" {}
        _MatcapColor ("Matcap Tint", Color) = (1,1,1,1)
        _MatcapTexture2 ("Matcap 2 (Blend)", 2DArray) = "black" {}
        _UseMatcapSecond ("Use Second Matcap", Float) = 0

        [Header(Rim Lighting)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.0, 10.0)) = 1.0
        _RimIntensity ("Rim Intensity", Range(0.0, 1.0)) = 0.0

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.01
        _OutlineMask ("Outline Mask (R)", 2DArray) = "white" {}
        _OutlineScreenSpace ("Screen-space Outline", Float) = 1

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2DArray) = "black" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0.0, 10.0)) = 1.0

        [Header(Dissolve)]
        _DissolveMask ("Dissolve Mask (R)", 2DArray) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0.0, 1.0)) = 0.0

        [Header(Detail Map)]
        _DetailMap ("Detail Map", 2DArray) = "white" {}
        _DetailScale ("Detail Scale", Float) = 1.0
        
        // Internal properties for MaterialOptimizer to use
        [HideInInspector] _WKVRCOpt_MeshID ("Mesh ID", Float) = 0
        [HideInInspector] _WKVRCOpt_BaseMap_Idx ("BaseMap Index", Float) = 0
        [HideInInspector] _WKVRCOpt_NormalMap_Idx ("NormalMap Index", Float) = 0
        [HideInInspector] _WKVRCOpt_MetallicGlossMap_Idx ("MetallicGlossMap Index", Float) = 0
        [HideInInspector] _WKVRCOpt_ShadeMap_Idx ("ShadeMap Index", Float) = 0
        [HideInInspector] _WKVRCOpt_RampTexture_Idx ("RampTexture Index", Float) = 0
        [HideInInspector] _WKVRCOpt_MatcapTexture_Idx ("MatcapTexture Index", Float) = 0
        [HideInInspector] _WKVRCOpt_MatcapTexture2_Idx ("MatcapTexture2 Index", Float) = 0
        [HideInInspector] _WKVRCOpt_OutlineMask_Idx ("OutlineMask Index", Float) = 0
        [HideInInspector] _WKVRCOpt_EmissionMap_Idx ("EmissionMap Index", Float) = 0
        [HideInInspector] _WKVRCOpt_DissolveMask_Idx ("DissolveMask Index", Float) = 0
        [HideInInspector] _WKVRCOpt_DetailMap_Idx ("DetailMap Index", Float) = 0

        // Render States managed by ShaderIR
        [HideInInspector] _SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector] _DstBlend ("_DstBlend", Float) = 0
        [HideInInspector] _ZWrite ("_ZWrite", Float) = 1
        [HideInInspector] _Cull ("_Cull", Float) = 2
        [HideInInspector] _IgnoreProjector ("_IgnoreProjector", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        // --- Passes ---
        // ForwardBase pass for main lighting
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            HLSLPROGRAM
            #pragma target 3.5 // VRChat Quest compatible, supports Texture2DArray
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            // Shading models
            #pragma shader_feature _SHADING_MODE_UNLIT
            #pragma shader_feature _SHADING_MODE_TOON
            #pragma shader_feature _SHADING_MODE_PBR

            // Core features
            #pragma shader_feature _USE_NORMAL_MAP
            #pragma shader_feature _USE_METALLIC_GLOSS_MAP
            #pragma shader_feature _USE_SHADE_MAP
            #pragma shader_feature _USE_RAMP_TEXTURE
            #pragma shader_feature _USE_MATCAP_TEXTURE
            #pragma shader_feature _USE_MATCAP_SECOND
            #pragma shader_feature _USE_RIM_LIGHTING
            #pragma shader_feature _USE_EMISSION
            #pragma shader_feature _USE_DISSOLVE
            #pragma shader_feature _USE_DETAIL_MAP
            #pragma shader_feature _DOUBLE_SIDED // Handled by Cull mode, but good for explicit branching

            // Blending (controlled by properties from ShaderIR)
            #pragma shader_feature _TRANSPARENCY_OFF
            #pragma shader_feature _TRANSPARENCY_CUTOUT
            #pragma shader_feature _TRANSPARENCY_ALPHA
            #pragma shader_feature _TRANSPARENCY_ADDITIVE
            #pragma shader_feature _TRANSPARENCY_PREMUL

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "Assets/WKAvatarOptimizer/Shaders/UniversalAvatarCore.hlsl" // Core shading logic

            ENDHLSL
        }

        // ForwardAdd pass for additional lights
        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off // No ZWrite in Additive pass

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog

            // Same keywords as ForwardBase
            #pragma shader_feature _SHADING_MODE_UNLIT _SHADING_MODE_TOON _SHADING_MODE_PBR
            #pragma shader_feature _USE_NORMAL_MAP
            #pragma shader_feature _USE_METALLIC_GLOSS_MAP
            #pragma shader_feature _USE_SHADE_MAP
            #pragma shader_feature _USE_RAMP_TEXTURE
            #pragma shader_feature _USE_MATCAP_TEXTURE
            #pragma shader_feature _USE_MATCAP_SECOND
            #pragma shader_feature _USE_RIM_LIGHTING
            #pragma shader_feature _USE_EMISSION
            #pragma shader_feature _USE_DISSOLVE
            #pragma shader_feature _USE_DETAIL_MAP
            #pragma shader_feature _DOUBLE_SIDED

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "Assets/WKAvatarOptimizer/Shaders/UniversalAvatarCore.hlsl"

            ENDHLSL
        }

        // ShadowCaster pass
        Pass
        {
            Name "SHADOW_CASTER"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_shadowcaster
            #pragma shader_feature _TRANSPARENCY_CUTOUT

            #include "UnityCG.cginc"
            #include "Assets/WKAvatarOptimizer/Shaders/UniversalAvatarCore.hlsl"

            struct appdata_shadow
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f_shadow
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_shadow vert_shadow (appdata_shadow v)
            {
                v2f_shadow o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.uv;
                return o;
            }

            float4 frag_shadow (v2f_shadow i) : SV_Target
            {
                // For cutout, sample main texture and discard
                #if _TRANSPARENCY_CUTOUT
                    float instanceID = _WKVRCOpt_MeshID; // Placeholder for actual instance ID from vertex data
                    float baseMapIdx = _WKVRCOpt_BaseMap_Idx; // Placeholder for texture index from vertex data
                    float4 baseColorTex = UNITY_SAMPLE_TEX2DARRAY(_BaseMap, float3(i.uv, baseMapIdx)); // Or use i.uv
                    clip(baseColorTex.a - 0.5); // Replace with actual alpha cutoff property
                #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}
