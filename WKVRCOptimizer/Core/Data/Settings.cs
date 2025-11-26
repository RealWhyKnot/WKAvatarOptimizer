using UnityEngine;

namespace WKVRCOptimizer.Data
{
    [System.Serializable]
    public class Settings
    {
        public bool ApplyOnUpload = true;
        public bool WritePropertiesAsStaticValues = false;
        public bool MergeSkinnedMeshes = true;
        public int MergeSkinnedMeshesWithShaderToggle = 0;
        public int MergeSkinnedMeshesWithNaNimation = 0;
        public bool NaNimationAllow3BoneSkinning = false;
        public bool MergeSkinnedMeshesSeparatedByDefaultEnabledState = true;
        public bool MergeStaticMeshesAsSkinned = false;
        public bool MergeDifferentPropertyMaterials = false;
        public bool MergeSameDimensionTextures = false;
        public bool MergeMainTex = false;
        public bool OptimizeFXLayer = true;
        public bool CombineApproximateMotionTimeAnimations = false;
        public bool DisablePhysBonesWhenUnused = true;
        public bool MergeSameRatioBlendShapes = true;
        public bool MMDCompatibility = true;
        public bool DeleteUnusedComponents = true;
        public int DeleteUnusedGameObjects = 0;
        public bool UseRingFingerAsFootCollider = false;
        public bool ProfileTimeUsed = false;
    }
}
