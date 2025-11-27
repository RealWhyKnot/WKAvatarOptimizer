using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor.Animations;
using UnityEngine;

namespace WKAvatarOptimizer.Extensions
{
    public static class RendererExtensions
    {
        public static Mesh GetSharedMesh(this Renderer renderer)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer)
            {
                mesh = (renderer as SkinnedMeshRenderer).sharedMesh;
            }
            else if (renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }
            return mesh;
        }

        public static Transform GetRootBone(this Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.rootBone != null)
            {
                return skinnedMeshRenderer.rootBone;
            }
            return renderer.transform;
        }

        public static void SetBoneWeights(this Mesh mesh, BoneWeight[] boneWeights)
        {
            var weightsPerVertex = new byte[boneWeights.Length];
            var boneWeights1 = new List<BoneWeight1>();
            for (int i = 0; i < boneWeights.Length; i++)
            {
                weightsPerVertex[i] = 0;
                var w = boneWeights[i];
                if (w.weight0 > 0)
                {
                    weightsPerVertex[i]++;
                    boneWeights1.Add(new BoneWeight1() { boneIndex = w.boneIndex0, weight = w.weight0 });
                }
                if (w.weight1 > 0)
                {
                    weightsPerVertex[i]++;
                    boneWeights1.Add(new BoneWeight1() { boneIndex = w.boneIndex1, weight = w.weight1 });
                }
                if (w.weight2 > 0)
                {
                    weightsPerVertex[i]++;
                    boneWeights1.Add(new BoneWeight1() { boneIndex = w.boneIndex2, weight = w.weight2 });
                }
                if (w.weight3 > 0)
                {
                    weightsPerVertex[i]++;
                    boneWeights1.Add(new BoneWeight1() { boneIndex = w.boneIndex3, weight = w.weight3 });
                }
            }
            mesh.SetBoneWeights(new NativeArray<byte>(weightsPerVertex, Allocator.Temp), new NativeArray<BoneWeight1>(boneWeights1.ToArray(), Allocator.Temp));
        }
    }

    public static class TransformExtensions
    {
        public static IEnumerable<Transform> GetAllDescendants(this Transform transform)
        {
            var stack = new Stack<Transform>();
            foreach (Transform child in transform) {
                stack.Push(child);
            }
            while (stack.Count > 0) {
                var current = stack.Pop();
                yield return current;
                foreach (Transform child in current) {
                    stack.Push(child);
                }
            }
        }

        public static bool IsDescendantOf(this Transform transform, Transform ancestor)
        {
            if (transform == null || ancestor == null) {
                return false;
            }
            var current = transform;
            while (current != null) {
                if (current == ancestor) {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        public static Component[] GetNonNullComponents(this Transform transform)
        {
            var components = transform.GetComponents<Component>();
            if (components.All(c => c != null)) {
                return components;
            }
            
            var path = new List<string>();
            var current = transform;
            while (current != null) {
                path.Add(current.name);
                current = current.parent;
            }
            string pathString = string.Join("/", path.Reverse<string>());
            var nonNullComponents = components.Where(c => c != null).ToArray();
            Debug.LogWarning($"Found {components.Length - nonNullComponents.Length} null components on {pathString}. You might be missing some scripts.");
            return nonNullComponents;
        }

        public static Component[] GetNonNullComponents(this GameObject gameObject)
        {
            var result = gameObject.transform.GetNonNullComponents();
            return result;
        }

        public static string GetPathToRoot(this Transform t, Transform root)
        {
            if (t == root) {
                return "";
            }
            string path = t.name;
            while ((t = t.parent) != root) {
                if (t == null) {
                    return null;
                }
                path = $"{t.name}/{path}";
            }
            return path;
        }

        public static string GetPathToRoot(this GameObject obj, Transform root)
        {
            var result = obj.transform.GetPathToRoot(root);
            return result;
        }

        public static string GetPathToRoot(this Component component, Transform root)
        {
            var result = component.transform.GetPathToRoot(root);
            return result;
        }

        public static Transform GetTransformFromPath(this Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) {
                return root;
            }
            string[] pathParts = path.Split('/');
            Transform t = root;
            for (int i = 0; i < pathParts.Length; i++) {
                t = t.Find(pathParts[i]);
                if (t == null) {
                    return null;
                }
            }
            return t;
        }
    }

    public static class AnimatorControllerExtensions
    {
        public static IEnumerable<AnimatorState> EnumerateAllStates(this AnimatorController controller)
        {
            var queue = new Queue<AnimatorStateMachine>();
            foreach (var layer in controller.layers) {
                queue.Enqueue(layer.stateMachine);
                while (queue.Count > 0) {
                    var stateMachine = queue.Dequeue();
                    foreach (var subStateMachine in stateMachine.stateMachines) {
                        queue.Enqueue(subStateMachine.stateMachine);
                    }
                    foreach (var state in stateMachine.states.Select(s => s.state)) {
                        yield return state;
                    }
                }
            }
        }

        public static IEnumerable<AnimatorState> EnumerateAllStates(this AnimatorStateMachine stateMachine)
        {
            var queue = new Queue<AnimatorStateMachine>();
            queue.Enqueue(stateMachine);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                foreach (var subStateMachine in current.stateMachines) {
                    queue.Enqueue(subStateMachine.stateMachine);
                }
                foreach (var state in current.states.Select(s => s.state)) {
                    yield return state;
                }
            }
        }

        public static IEnumerable<StateMachineBehaviour> EnumerateAllBehaviours(this AnimatorStateMachine stateMachine)
        {
            var queue = new Queue<AnimatorStateMachine>();
            queue.Enqueue(stateMachine);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                foreach (var subStateMachine in current.stateMachines) {
                    queue.Enqueue(subStateMachine.stateMachine);
                }
                foreach (var behaviour in current.behaviours) {
                    yield return behaviour;
                }
                foreach (var state in current.states.Select(s => s.state)) {
                    foreach (var behaviour in state.behaviours) {
                        yield return behaviour;
                    }
                }
            }
        }

        public static List<AnimatorTransitionBase> EnumerateAllTransitions(this AnimatorStateMachine stateMachine)
        {
            var queue = new Queue<AnimatorStateMachine>();
            var stateTransitions = new List<AnimatorTransitionBase>();
            var transitions = new List<AnimatorTransitionBase>();
            queue.Enqueue(stateMachine);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                transitions.AddRange(current.entryTransitions);
                stateTransitions.AddRange(current.anyStateTransitions);
                stateTransitions.AddRange(current.states.SelectMany(s => s.state.transitions));
                foreach (var subStateMachine in current.stateMachines) {
                    queue.Enqueue(subStateMachine.stateMachine);
                    transitions.AddRange(current.GetStateMachineTransitions(subStateMachine.stateMachine));
                }
            }
            transitions.AddRange(stateTransitions);
            return transitions;
        }

        public static IEnumerable<AnimationClip> EnumerateAllClips(this Motion motion)
        {
            if (motion is AnimationClip clip) {
                yield return clip;
            } else if (motion is BlendTree tree) {
                var childNodes = tree.children;
                for (int i = 0; i < childNodes.Length; i++) {
                    if (childNodes[i].motion == null) continue;
                    foreach (var childClip in childNodes[i].motion.EnumerateAllClips()) {
                        yield return childClip;
                    }
                }
            }
        }

        public static IEnumerable<(AnimatorState state, Motion motion)> EnumerateAllMotionOverrides(this AnimatorControllerLayer layer)
        {
            if (layer.syncedLayerIndex < 0) {
                yield break;
            }
            var field = typeof(AnimatorControllerLayer).GetField("m_Motions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = field.GetValue(layer);
            if (value == null) {
                yield break;
            }
            var stateMotionPairs = value as System.Array;
            if (stateMotionPairs == null) {
                yield break;
            }
            foreach (var stateMotionPair in stateMotionPairs) {
                var stateField = stateMotionPair.GetType().GetField("m_State", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var state = stateField.GetValue(stateMotionPair) as AnimatorState;
                var motionField = stateMotionPair.GetType().GetField("m_Motion", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var motionValue = motionField.GetValue(stateMotionPair) as Motion;
                if (state != null) {
                    yield return (state, motionValue);
                }
            }
        }
    }

    public static class Vector3Extensions
    {
        public static Vector3 Multiply(this Vector3 vec, float x, float y, float z)
        {
            vec.x *= x;
            vec.y *= y;
            vec.z *= z;
            return vec;
        }
    }

    public static class StringExtensions
    {
        public static bool StartsWithSimple(this string str, string value)
        {
            if (str.Length < value.Length) {
                return false;
            }
            for (int i = 0; i < value.Length; i++) {
                if (str[i] != value[i]) {
                    return false;
                }
            }
            return true;
        }
        
        public static bool StartsWithSimple(this string str, string value, int startIndex)
        {
            if (str.Length - startIndex < value.Length) {
                return false;
            }
            for (int i = 0; i < value.Length; i++) {
                if (str[startIndex + i] != value[i]) {
                    return false;
                }
            }
            return true;
        }
    }
}