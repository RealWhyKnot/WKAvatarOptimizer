using UnityEngine;
using System;
using UnityEngine.Rendering;
using WKVRCOptimizer.Extensions;

namespace WKVRCOptimizer.Data
{
    public struct MaterialSlot
    {
        public Renderer renderer;
        public int index;
        public Material material
        {
            get { return renderer.sharedMaterials[index]; }
        }
        public MaterialSlot(Renderer renderer, int index)
        {
            this.renderer = renderer;
            this.index = index;
        }
        public static MaterialSlot[] GetAllSlotsFrom(Renderer renderer)
        {
            var result = new MaterialSlot[renderer.sharedMaterials.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new MaterialSlot(renderer, i);
            }
            return result;
        }
#if UNITY_EDITOR
        public MeshTopology GetTopology()
        {
            return renderer.GetSharedMesh()?.GetTopology(Math.Min(index, renderer.GetSharedMesh().subMeshCount - 1)) ?? MeshTopology.Triangles;
        }
#endif
    }
}
