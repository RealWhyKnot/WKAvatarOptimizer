using UnityEngine;
using UnityEditor;
using WKAvatarOptimizer.Core;
using WKAvatarOptimizer.Core.Universal;
using System.Linq;

namespace WKAvatarOptimizer.Editor
{
    public class ShaderAnalyzerDebugger : EditorWindow
    {
        [MenuItem("Window/WKAvatarOptimizer/Shader Analyzer Debugger")]
        public static void ShowWindow()
        {
            GetWindow<ShaderAnalyzerDebugger>("Shader Debugger");
        }

        private Shader shader;
        private Material material;
        private ShaderIR ir;
        private Vector2 scrollPosition;

        void OnGUI()
        {
            shader = (Shader)EditorGUILayout.ObjectField("Shader", shader, typeof(Shader), false);
            material = (Material)EditorGUILayout.ObjectField("Material", material, typeof(Material), false);

            if (GUILayout.Button("Analyze"))
            {
                if (shader != null && material != null)
                {
                    ir = ShaderAnalyzer.ParseUniversal(shader, material);
                }
                else
                {
                    Debug.LogError("Please assign both Shader and Material.");
                }
            }

            if (ir != null)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                EditorGUILayout.LabelField("Shader IR", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Name: {ir.Name}");
                EditorGUILayout.LabelField($"Material: {ir.MaterialName}");
                EditorGUILayout.LabelField($"Shading Model: {ir.shadingModel}");
                EditorGUILayout.LabelField($"Blend Mode: {ir.blendMode}");
                EditorGUILayout.LabelField($"Cull Mode: {ir.cullMode}");
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
                DrawTexture("BaseColor", ir.baseColor);
                DrawTexture("NormalMap", ir.normalMap);
                DrawTexture("MetallicGloss", ir.metallicGlossMap);
                DrawTexture("Emission", ir.emissionMap);
                DrawTexture("ShadeMap", ir.shadeMap);
                DrawTexture("Matcap", ir.matcapTexture);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Custom Nodes", EditorStyles.boldLabel);
                foreach(var node in ir.customNodes)
                {
                    EditorGUILayout.LabelField($"- {node.category}: {node.name} ({node.description})");
                }

                EditorGUILayout.EndScrollView();
            }
        }

        void DrawTexture(string label, TextureProperty prop)
        {
            if (prop.texture != null)
            {
                EditorGUILayout.ObjectField(label, prop.texture, typeof(Texture2D), false);
            }
        }
    }
}