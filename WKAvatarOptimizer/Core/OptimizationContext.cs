using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WKAvatarOptimizer.Core.Universal; // Added

namespace WKAvatarOptimizer.Core
{
    using AnimationPath = System.ValueTuple<string, string, System.Type>;

    public class OptimizationContext
    {

        public string packageRootPath = "Assets/WKAvatarOptimizer";
        public string trashBinPath = "Assets/WKAvatarOptimizer/TrashBin/";
        public string binaryAssetBundlePath = null;
        public string materialAssetBundlePath = null;
        
        public HashSet<string> usedBlendShapes = new HashSet<string>();
        public Dictionary<SkinnedMeshRenderer, List<int>> blendShapesToBake = new Dictionary<SkinnedMeshRenderer, List<int>>();
        public Dictionary<AnimationPath, AnimationPath> newAnimationPaths = new Dictionary<AnimationPath, AnimationPath>();
        public HashSet<string> pathsToDeleteGameObjectTogglesOn = new HashSet<string>();
        
        public Dictionary<Material, (Material target, List<Material> sources, ShaderIR optimizerResult)> optimizedMaterials = new Dictionary<Material, (Material, List<Material>, ShaderIR)>();
        public List<string> optimizedMaterialImportPaths = new List<string>();
        
        public Dictionary<string, List<List<string>>> oldPathToMergedPaths = new Dictionary<string, List<List<string>>>();
        public Dictionary<string, string> oldPathToMergedPath = new Dictionary<string, string>();
        
        public Dictionary<string, List<string>> physBonesToDisable = new Dictionary<string, List<string>>();
        
        public Dictionary<(string path, int slot), HashSet<Material>> slotSwapMaterials = new Dictionary<(string, int), HashSet<Material>>();
        public Dictionary<(string path, int slot), Dictionary<Material, Material>> optimizedSlotSwapMaterials = new Dictionary<(string, int), Dictionary<Material, Material>>();
        public Dictionary<(string path, int index), (string path, int index)> materialSlotRemap = new Dictionary<(string, int), (string, int)>();
        
        public Dictionary<string, HashSet<string>> animatedMaterialProperties = new Dictionary<string, HashSet<string>>();
        public Dictionary<string, HashSet<string>> fusedAnimatedMaterialProperties = new Dictionary<string, HashSet<string>>();
        public Dictionary<string, Dictionary<string, Vector4>> animatedMaterialPropertyDefaultValues = new Dictionary<string, Dictionary<string, Vector4>>();
        
        public List<List<Texture2D>> textureArrayLists = new List<List<Texture2D>>();
        public List<Texture2DArray> textureArrays = new List<Texture2DArray>();
        public Dictionary<Material, List<(string name, Texture2DArray array)>> texArrayPropertiesToSet = new Dictionary<Material, List<(string name, Texture2DArray array)>>();
        
        public HashSet<Transform> keepTransforms = new HashSet<Transform>();
        public HashSet<string> convertedMeshRendererPaths = new HashSet<string>();
        public Dictionary<Transform, Transform> movingParentMap = new Dictionary<Transform, Transform>();
        public Dictionary<string, Transform> transformFromOldPath = new Dictionary<string, Transform>();
        
        public Dictionary<EditorCurveBinding, float> constantAnimatedValuesToAdd = new Dictionary<EditorCurveBinding, float>();
        
        public List<string> optimizationLogs = new List<string>();

        public void Log(string message) {
            optimizationLogs.Add($"[OptimizationContext] {message}");
        }

        private static void _Log(string message) {
        }

        public static readonly HashSet<string> MMDBlendShapes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "まばたき", "Blink",
            "笑い", "Smile",
            "ウィンク", "Wink",
            "ウィンク右", "Wink-a",
            "ウィンク２", "Wink-b",
            "ｳｨﾝｸ２右", "Wink-c",
            "なごみ", "Howawa",
            "はぅ", "> <",
            "びっくり", "Ha!!!",
            "じと目", "Jito-eye",
            "ｷﾘｯ", "Kiri-eye",
            "はちゅ目", "O O",
            "星目", "EyeStar",
            "はぁと", "EyeHeart",
            "瞳小", "EyeSmall",
            "瞳縦潰れ", "EyeSmall-v",
            "光下", "EyeUnderli",
            "恐ろしい子！", "EyeFunky",
            "ハイライト消", "EyeHi-off",
            "映り込み消", "EyeRef-off",
            "喜び", "Joy",
            "わぉ?!", "Wao?!",
            "なごみω", "Howawa ω",
            "悲しむ", "Wail",
            "敵意", "Hostility",
            "あ", "a",
            "い", "i",
            "う", "u",
            "え", "e",
            "お", "o",
            "あ２", "a 2",
            "ん", "n",
            "▲", "Mouse_1",
            "∧", "Mouse_2",
            "□", "□",
            "ワ", "Wa",
            "ω", "Omega",
            "ω□", "ω□",
            "にやり", "Niyari",
            "にやり２", "Niyari2",
            "にっこり", "Smile",
            "ぺろっ", "Pero",
            "てへぺろ", "Bero-tehe",
            "てへぺろ２", "Bero-tehe2",
            "口角上げ", "MouseUP",
            "口角下げ", "MouseDW",
            "口横広げ", "MouseWD",
            "歯無し上", "ToothAnon",
            "歯無し下", "ToothBnon",
            "真面目", "Serious",
            "困る", "Trouble",
            "にこり", "Smily",
            "怒り", "Get angry",
            "上", "UP",
            "下", "Down",
            "Grin",
            "Blink",
            "Blink Happy",
            "Pupil",
            "Wink",
            "Wink Right",
            "Wink 2",
            "Wink 2 Right",
            "Calm",
            "Stare",
            "Cheerful",
            "Sadness",
            "Anger",
            "Upper",
            "Lower"
        };

        public void Clear()
        {
            Log("Clear() called. Resetting optimization context.");
            packageRootPath = "Assets/WKVRCOptimizer";
            binaryAssetBundlePath = null;
            materialAssetBundlePath = null;
            
            usedBlendShapes.Clear();
            blendShapesToBake.Clear();
            newAnimationPaths.Clear();
            pathsToDeleteGameObjectTogglesOn.Clear();
            optimizedMaterials.Clear();
            optimizedMaterialImportPaths.Clear();
            oldPathToMergedPaths.Clear();
            oldPathToMergedPath.Clear();
            physBonesToDisable.Clear();
            slotSwapMaterials.Clear();
            optimizedSlotSwapMaterials.Clear();
            materialSlotRemap.Clear();
            animatedMaterialProperties.Clear();
            fusedAnimatedMaterialProperties.Clear();
            animatedMaterialPropertyDefaultValues.Clear();
            textureArrayLists.Clear();
            textureArrays.Clear();
            texArrayPropertiesToSet.Clear();
            keepTransforms.Clear();
            convertedMeshRendererPaths.Clear();
            movingParentMap.Clear();
            transformFromOldPath.Clear();
            constantAnimatedValuesToAdd.Clear();
            Log("Clear() finished. Optimization context reset.");
        }
    }
}