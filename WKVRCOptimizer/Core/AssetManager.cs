#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using WKVRCOptimizer.Core.Util;

namespace WKVRCOptimizer.Core
{
    public static class AssetManager
    {
        public static string GetTrashBinPath(OptimizationContext context)
        {
            var path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("WKVRCOptimizer")[0]);
            context.packageRootPath = path.Substring(0, path.LastIndexOf('/'));
            context.packageRootPath = context.packageRootPath.Substring(0, context.packageRootPath.LastIndexOf('/'));
            var trashBinRoot = context.packageRootPath;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            if (packageInfo?.source != UnityEditor.PackageManager.PackageSource.Embedded)
            {
                trashBinRoot = "Assets/WKVRCOptimizer";
                if (!AssetDatabase.IsValidFolder("Assets/WKVRCOptimizer"))
                {
                    AssetDatabase.CreateFolder("Assets", "WKVRCOptimizer");
                }
            }
            context.trashBinPath = trashBinRoot + "/TrashBin/";
            if (!AssetDatabase.IsValidFolder(trashBinRoot + "/TrashBin"))
            {
                AssetDatabase.CreateFolder(trashBinRoot, "TrashBin");
            }
            return context.trashBinPath;
        }

        public static void ClearTrashBin(OptimizationContext context)
        {
            var path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("WKVRCOptimizer")[0]);
            context.packageRootPath = path.Substring(0, path.LastIndexOf('/'));
            context.packageRootPath = context.packageRootPath.Substring(0, context.packageRootPath.LastIndexOf('/'));
            var trashBinRoot = context.packageRootPath;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            if (packageInfo?.source != UnityEditor.PackageManager.PackageSource.Embedded)
            {
                trashBinRoot = "Assets/WKVRCOptimizer";
                if (!AssetDatabase.IsValidFolder("Assets/WKVRCOptimizer"))
                {
                    AssetDatabase.CreateFolder("Assets", "WKVRCOptimizer");
                }
            }
            context.trashBinPath = trashBinRoot + "/TrashBin/";
            // Check if folder exists before deleting
            if (AssetDatabase.IsValidFolder(trashBinRoot + "/TrashBin"))
            {
                AssetDatabase.DeleteAsset(trashBinRoot + "/TrashBin");
            }
            AssetDatabase.CreateFolder(trashBinRoot, "TrashBin");
        }

        public static void CreateUniqueAsset(OptimizationContext context, Object asset, string name)
        {
            Profiler.StartSection("AssetDatabase.CreateAsset()");
            var invalids = Path.GetInvalidFileNameChars();
            var sanitizedName = string.Join("_", name.Split(invalids, System.StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
            
            if (asset is Material)
            {
                if (context.materialAssetBundlePath == null)
                {
                    context.materialAssetBundlePath = AssetDatabase.GenerateUniqueAssetPath(context.trashBinPath + sanitizedName);
                    AssetDatabase.CreateAsset(asset, context.materialAssetBundlePath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(asset, context.materialAssetBundlePath);
                }
            }
            else
            {
                if (context.binaryAssetBundlePath == null)
                {
                    context.binaryAssetBundlePath = AssetDatabase.GenerateUniqueAssetPath(context.trashBinPath + "BinaryAssetBundle.asset");
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<BinarySerializationSO>(), context.binaryAssetBundlePath);
                }
                AssetDatabase.AddObjectToAsset(asset, context.binaryAssetBundlePath);
            }
            Profiler.EndSection();
        }
    }
}
#endif