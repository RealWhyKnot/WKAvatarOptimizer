using System;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace WKAvatarOptimizer.Editor
{
    [InitializeOnLoad]
    public class AvatarBuildHook : IVRCSDKPreprocessAvatarCallback
    {
        #if MODULAR_AVATAR_EXISTS
        public int callbackOrder => -15;
        #else
        public int callbackOrder => -1025;
        #endif

        static private bool didRunInPlayMode = false;
        [InitializeOnEnterPlayMode]
        static void OnEnterPlaymodeInEditor(EnterPlayModeOptions options)
        {
            didRunInPlayMode = false;
        }

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var optimizer = avatarGameObject.GetComponent<AvatarOptimizer>();
            
            if (optimizer == null)
            {
                return true;
            }
            try
            {
                if (Application.isPlaying)
                {
                    if (didRunInPlayMode)
                    {
                        Debug.LogWarning($"Only one avatar can be optimized per play mode session. Skipping optimization of {avatarGameObject.name}");
                        return true;
                    }
                }
                didRunInPlayMode = Application.isPlaying;
                optimizer.Optimize();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }
    }
}