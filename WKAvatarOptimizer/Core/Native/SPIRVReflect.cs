using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace WKAvatarOptimizer.Core.Native
{
    internal static class SPIRVReflectNative
    {
        // On Windows, the DLL would be named `spirv_reflect.dll`.
        // We will build a small C++ wrapper that exposes these functions
        // and link it against spirv_reflect.c/h.
        private const string SpirvReflectLibrary = "spirv_reflect.dll"; 

        // Enums and Structs from spirv_reflect.h
        // Only including what's immediately necessary for basic reflection

        public enum SpvReflectResult
        {
            SPV_REFLECT_RESULT_SUCCESS = 0,
            SPV_REFLECT_RESULT_NOT_READY = 1,
            SPV_REFLECT_RESULT_ERROR_PARSE_FAILED = 2,
            SPV_REFLECT_RESULT_ERROR_ALLOC_FAILED = 3,
            SPV_REFLECT_RESULT_ERROR_INTERNAL_ERROR = 4,
            SPV_REFLECT_RESULT_ERROR_NULL_POINTER = 5,
            SPV_REFLECT_RESULT_ERROR_OUT_OF_BOUNDS = 6,
            SPV_REFLECT_RESULT_ERROR_INPUT_SPIRV = 7,
            SPV_REFLECT_RESULT_ERROR_COUNT_MISMATCH = 8,
            SPV_REFLECT_RESULT_ERROR_ELEMENT_NOT_FOUND = 9,
            SPV_REFLECT_RESULT_ERROR_MAX_VALUE = 2147483647 // INT_MAX
        }

        public enum SpvReflectResourceType
        {
            SPV_REFLECT_RESOURCE_FLAG_UNDEFINED = 0x00000000,
            SPV_REFLECT_RESOURCE_FLAG_SRV = 0x00000001,
            SPV_REFLECT_RESOURCE_FLAG_UAV = 0x00000002,
            SPV_REFLECT_RESOURCE_FLAG_CBV = 0x00000004,
            SPV_REFLECT_RESOURCE_FLAG_SAMPLER = 0x00000008,
            SPV_REFLECT_RESOURCE_FLAG_MAX_VALUE = 0x7FFFFFFF
        }

        public enum SpvReflectDescriptorType
        {
            SPV_REFLECT_DESCRIPTOR_TYPE_SAMPLER = 0,
            SPV_REFLECT_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER = 1,
            SPV_REFLECT_DESCRIPTOR_TYPE_SAMPLED_IMAGE = 2,
            SPV_REFLECT_DESCRIPTOR_TYPE_STORAGE_IMAGE = 3,
            SPV_REFLECT_DESCRIPTOR_TYPE_UNIFORM_TEXEL_BUFFER = 4,
            SPV_REFLECT_DESCRIPTOR_TYPE_STORAGE_TEXEL_BUFFER = 5,
            SPV_REFLECT_DESCRIPTOR_TYPE_UNIFORM_BUFFER = 6,
            SPV_REFLECT_DESCRIPTOR_TYPE_STORAGE_BUFFER = 7,
            SPV_REFLECT_DESCRIPTOR_TYPE_INPUT_ATTACHMENT = 8,
            SPV_REFLECT_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE = 9,
            SPV_REFLECT_DESCRIPTOR_TYPE_MAX_ENUM = 0x7FFFFFFF
        }

        public enum SpvExecutionModel
        {
            SpvExecutionModelVertex = 0,
            SpvExecutionModelTessellationControl = 1,
            SpvExecutionModelTessellationEvaluation = 2,
            SpvExecutionModelGeometry = 3,
            SpvExecutionModelFragment = 4,
            SpvExecutionModelGLCompute = 5,
            SpvExecutionModelKernel = 6,
            SpvExecutionModelRayGenerationKHR = 7,
            SpvExecutionModelIntersectionKHR = 8,
            SpvExecutionModelAnyHitKHR = 9,
            SpvExecutionModelClosestHitKHR = 10,
            SpvExecutionModelMissKHR = 11,
            SpvExecutionModelCallableKHR = 12,
            SpvExecutionModelTaskEXT = 13,
            SpvExecutionModelMeshEXT = 14,
            SpvExecutionModelMax = 0x7FFFFFFF
        }

        // Minimal SpvReflectModule struct for now, expand as needed
        [StructLayout(LayoutKind.Sequential)]
        public struct SpvReflectModule
        {
            public SpvReflectResult result;
            public uint spirv_word_count;
            public IntPtr spirv_code; // Pointer to the SPIR-V bytecode
            public uint spirv_version;
            public uint generator_id;
            public IntPtr entry_point_name; // char*
            public SpvExecutionModel entry_point_model;

            // These are internal to the C library, we get them via enumeration functions
            // public uint descriptor_binding_count;
            // public IntPtr descriptor_bindings; // SpvReflectDescriptorBinding**
        }
        
        // Minimal SpvReflectDescriptorBinding struct
        [StructLayout(LayoutKind.Sequential)]
        public struct SpvReflectDescriptorBinding
        {
            public uint spirv_id;
            public uint binding;
            public uint input_variable_id;
            public uint count; // Array count. 0 or 1 if not an array. >1 if array.
            
            public SpvReflectDescriptorType descriptor_type;
            public SpvReflectResourceType resource_type;
            
            public IntPtr name; // char*
            
            // For texture/sampler specific data, can be added later
            // public SpvReflectImageTraits image;
            // public SpvReflectSamplerTraits sampler;
        }

        // spvReflectCreateShaderModule
        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern SpvReflectResult spvReflectCreateShaderModule(
            UIntPtr size,
            IntPtr p_code,
            ref SpvReflectModule p_module
        );

        // spvReflectDestroyShaderModule
        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void spvReflectDestroyShaderModule(
            ref SpvReflectModule p_module
        );

        // spvReflectEnumerateDescriptorBindings
        // First call to get count, second call to get pointers to bindings
        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern SpvReflectResult spvReflectEnumerateDescriptorBindings(
            ref SpvReflectModule p_module,
            out uint p_count,
            [Out] IntPtr[] pp_bindings // Array of pointers to SpvReflectDescriptorBinding structs
        );

        // Overload for getting count only (pass null for pp_bindings)
        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "spvReflectEnumerateDescriptorBindings")]
        public static extern SpvReflectResult spvReflectEnumerateDescriptorBindings_Count(
            ref SpvReflectModule p_module,
            out uint p_count,
            IntPtr pp_bindings // Should be IntPtr.Zero
        );
        
        // spvReflectGetEntryPoint
        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr spvReflectGetEntryPoint(
            ref SpvReflectModule p_module,
            string entryPointName
        );


    }
    
    // Minimal wrapper for SPIRV-Reflect operations
    internal class SPIRVReflector : IDisposable
    {
        private SpvReflectNative.SpvReflectModule _module;
        private bool _disposed = false;

        public SPIRVReflector(byte[] spirvBytecode)
        {
            // Pin the bytecode array to prevent GC from moving it
            GCHandle pinnedCode = GCHandle.Alloc(spirvBytecode, GCHandleType.Pinned);
            IntPtr pCode = pinnedCode.AddrOfPinnedObject();

            SpvReflectNative.SpvReflectResult result = SpvReflectNative.spvReflectCreateShaderModule(
                (UIntPtr)spirvBytecode.Length,
                pCode,
                ref _module
            );

            // Release the pinned handle immediately after use
            pinnedCode.Free();

            if (result != SpvReflectNative.SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS)
            {
                throw new Exception($"Failed to create SPIR-V Reflect module: {result}");
            }
        }

        public string GetEntryPointName()
        {
            if (_module.entry_point_name != IntPtr.Zero)
            {
                return Marshal.PtrToStringAnsi(_module.entry_point_name);
            }
            return "unknown";
        }

        public SpvReflectNative.SpvExecutionModel GetEntryPointModel()
        {
            return _module.entry_point_model;
        }

        public SpvReflectNative.SpvReflectDescriptorBinding[] GetDescriptorBindings()
        {
            uint count;
            // First call to get the count of bindings
            SpvReflectNative.SpvReflectResult result = SpvReflectNative.spvReflectEnumerateDescriptorBindings_Count(
                ref _module, out count, IntPtr.Zero
            );
            if (result != SpvReflectNative.SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS)
            {
                throw new Exception($"Failed to get descriptor bindings count: {result}");
            }

            if (count == 0) return new SpvReflectNative.SpvReflectDescriptorBinding[0];

            // Allocate an array of IntPtrs to hold the pointers to the bindings
            IntPtr[] nativeBindingsPtrs = new IntPtr[count];
            
            // Second call to fill the array of pointers
            result = SpvReflectNative.spvReflectEnumerateDescriptorBindings(
                ref _module, out count, nativeBindingsPtrs
            );
            if (result != SpvReflectNative.SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS)
            {
                throw new Exception($"Failed to enumerate descriptor bindings: {result}");
            }

            // Marshal each native pointer to a C# struct
            SpvReflectNative.SpvReflectDescriptorBinding[] bindings = new SpvReflectNative.SpvReflectDescriptorBinding[count];
            for (int i = 0; i < count; i++)
            {
                bindings[i] = Marshal.PtrToStructure<SpvReflectNative.SpvReflectDescriptorBinding>(nativeBindingsPtrs[i]);
            }

            return bindings;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                SpvReflectNative.spvReflectDestroyShaderModule(ref _module);
                _disposed = true;
            }
        }

        ~SPIRVReflector()
        {
            Dispose(false);
        }
    }
}