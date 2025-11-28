using UnityEngine;
using System.Collections.Generic;

namespace WKAvatarOptimizer.Core
{
    public class CacheManager
    {

                private readonly OptimizationContext context;
                private readonly GameObject gameObject;
                
                public CacheManager(OptimizationContext context, GameObject root)
                {
                    this.context = context;
                    this.gameObject = root;
                }
        public Dictionary<string, bool> cache_MeshUses4BoneSkinning = null;
        public Dictionary<string, bool> cache_CanUseNaNimationOnMesh = null;
        public HashSet<string> cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation = null;
        public Dictionary<string, HashSet<AnimationClip>> cache_FindAllAnimationClipsAffectingRenderer = null;
        public bool? cache_withNaNimation = null;
        public Dictionary<(Renderer, Renderer), bool> cache_RendererHaveSameAnimationCurves = null;
        public Dictionary<float, AnimationClip> cache_DummyAnimationClipOfLength = null;

        public void ClearCaches()
        {
            cache_MeshUses4BoneSkinning = null;
            cache_CanUseNaNimationOnMesh = null;
            cache_FindAllPathsWhereMeshOrGameObjectHasOnlyOnAnimation = null;
            cache_FindAllAnimationClipsAffectingRenderer = null;
            cache_withNaNimation = null;
            cache_RendererHaveSameAnimationCurves = null;
            cache_DummyAnimationClipOfLength = null;
            context.Clear();
        }
    }
}