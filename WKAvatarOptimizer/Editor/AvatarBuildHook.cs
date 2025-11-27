using System;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace WKAvatarOptimizer.Editor
{
    [InitializeOnLoad]
    public class AvatarBuildHook : IVRCSDKPreprocessAvatarCallback
    {
        private static void _Log(string message) {
            Debug.Log($"[AvatarBuildHook] {message}");
        }

        // Modular Avatar is at -25, we want to be after that. However usually vrcsdk removes IEditorOnly at -1024.
        // MA patches that to happen last so we can only be at -15 if MA is installed otherwise our component will be removed before getting run.
        #if MODULAR_AVATAR_EXISTS
        public int callbackOrder => -15;
        #else
        public int callbackOrder => -1025;
        #endif

        static private bool didRunInPlayMode = false;
        [InitializeOnEnterPlayMode]
        static void OnEnterPlaymodeInEditor(EnterPlayModeOptions options)
        {
            _Log("OnEnterPlaymodeInEditor() called.");
            didRunInPlayMode = false;
            _Log("OnEnterPlaymodeInEditor() finished.");
        }

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            _Log($"OnPreprocessAvatar() called for avatar: {avatarGameObject.name}");
            var optimizer = avatarGameObject.GetComponent<AvatarOptimizer>();
            
            if (optimizer == null)
            {
                _Log($"AvatarOptimizer is null for {avatarGameObject.name}. Skipping optimization.");
                return true;
            }
            try
            {
                if (Application.isPlaying)
                {
                    if (didRunInPlayMode)
                    {
                        Debug.LogWarning($"Only one avatar can be optimized per play mode session. Skipping optimization of {avatarGameObject.name}");
                        _Log($"Skipping optimization of {avatarGameObject.name} as an avatar was already optimized in this play mode session.");
                        return true;
                    }
                }
                didRunInPlayMode = Application.isPlaying;
                _Log($"Optimizing avatar: {avatarGameObject.name}");
                optimizer.Optimize();
                _Log($"Optimization of {avatarGameObject.name} completed successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                _Log($"Optimization of {avatarGameObject.name} failed with exception: {e.Message}");
                return false;
            }
        }
    }
}