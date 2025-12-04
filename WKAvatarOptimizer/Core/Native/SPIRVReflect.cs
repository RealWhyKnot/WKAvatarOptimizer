using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace WKAvatarOptimizer.Core.Native
{
    #region Enums
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
        SPV_REFLECT_RESULT_ERROR_MAX_VALUE = 2147483647
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
    #endregion

    #region Structs
    [StructLayout(LayoutKind.Sequential)]
    public struct SpvReflectModule
    {
        public SpvReflectResult result;
        public uint spirv_word_count;
        public IntPtr spirv_code; 
        public uint spirv_version;
        public uint generator_id;
        public IntPtr entry_point_name;
        public SpvExecutionModel entry_point_model;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct SpvReflectDescriptorBinding
    {
        public uint spirv_id;
        public uint binding;
        public uint input_variable_id;
        public uint count;
        public SpvReflectDescriptorType descriptor_type;
        public SpvReflectResourceType resource_type;
        public IntPtr name;
    }
    #endregion

    public static class SPIRVReflectNative
    {
        private const string SpirvReflectLibrary = "spirv_reflect.dll"; 

        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern SpvReflectResult spvReflectCreateShaderModule(
            UIntPtr size,
            IntPtr p_code,
            ref SpvReflectModule p_module
        );

        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void spvReflectDestroyShaderModule(
            ref SpvReflectModule p_module
        );

        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern SpvReflectResult spvReflectEnumerateDescriptorBindings(
            ref SpvReflectModule p_module,
            out uint p_count,
            [Out] IntPtr[] pp_bindings
        );

        [DllImport(SpirvReflectLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "spvReflectEnumerateDescriptorBindings")]
        public static extern SpvReflectResult spvReflectEnumerateDescriptorBindings_Count(
            ref SpvReflectModule p_module,
            out uint p_count,
            IntPtr pp_bindings
        );
    }
    
    public class SPIRVReflector : IDisposable
    {
        private SpvReflectModule _module;
        private GCHandle _pinnedCode;
        private bool _disposed = false;

        public SPIRVReflector(byte[] spirvBytecode)
        {
            _pinnedCode = GCHandle.Alloc(spirvBytecode, GCHandleType.Pinned);
            IntPtr pCode = _pinnedCode.AddrOfPinnedObject();

            SpvReflectResult result = SPIRVReflectNative.spvReflectCreateShaderModule(
                (UIntPtr)spirvBytecode.Length,
                pCode,
                ref _module
            );

            if (result != SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS)
            {
                if (_pinnedCode.IsAllocated) _pinnedCode.Free();
                throw new Exception($"Failed to create SPIR-V Reflect module: {result}");
            }
        }

        public SpvReflectDescriptorBinding[] GetDescriptorBindings()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SPIRVReflector));

            uint count;
            SpvReflectResult result = SPIRVReflectNative.spvReflectEnumerateDescriptorBindings_Count(
                ref _module, out count, IntPtr.Zero
            );
            if (result != SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS) throw new Exception($"Failed to get bindings count: {result}");

            if (count == 0) return new SpvReflectDescriptorBinding[0];

            IntPtr[] nativeBindingsPtrs = new IntPtr[count];
            result = SPIRVReflectNative.spvReflectEnumerateDescriptorBindings(
                ref _module, out count, nativeBindingsPtrs
            );
            if (result != SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS) throw new Exception($"Failed to enumerate bindings: {result}");

            SpvReflectDescriptorBinding[] bindings = new SpvReflectDescriptorBinding[count];
            for (int i = 0; i < count; i++)
            {
                bindings[i] = Marshal.PtrToStructure<SpvReflectDescriptorBinding>(nativeBindingsPtrs[i]);
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
                SPIRVReflectNative.spvReflectDestroyShaderModule(ref _module);
                
                if (_pinnedCode.IsAllocated)
                    _pinnedCode.Free();

                _disposed = true;
            }
        }

        ~SPIRVReflector()
        {
            Dispose(false);
        }
    }
}
