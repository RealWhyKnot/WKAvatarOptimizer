using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WKAvatarOptimizer.Core.Native
{
    // Common IUnknown interface (all COM interfaces derive from this)
    [ComImport]
    [Guid("00000000-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IUnknown
    {
        [PreserveSig]
        IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig]
        uint AddRef();
        [PreserveSig]
        uint Release();
    }

    // IID_IDxcBlob
    [ComImport]
    [Guid("8ba5fb08-5195-40e2-ac58-0d989c3a0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcBlob : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // LPVOID GetBufferPointer();
        [PreserveSig]
        IntPtr GetBufferPointer();

        // SIZE_T GetBufferSize();
        [PreserveSig]
        UIntPtr GetBufferSize();
    }

    // IID_IDxcBlobEncoding : IDxcBlob
    [ComImport]
    [Guid("7f61fc7d-950d-4b82-9c32-f30a4c9e1cae")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcBlobEncoding : IDxcBlob
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        [PreserveSig]
        new IntPtr GetBufferPointer();

        [PreserveSig]
        new UIntPtr GetBufferSize();

        // HRESULT GetEncoding(BOOL* pKnown, UINT32* pCodePage);
        [PreserveSig]
        int GetEncoding(out int pKnown, out uint pCodePage);
    }

    // IID_IDxcBuffer (This is for input to Compile, it's just an IDxcBlob)
    [ComImport]
    [Guid("696cf3ce-fd67-4000-b5bc-a15dba67d40c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcBuffer : IDxcBlob
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        [PreserveSig]
        new IntPtr GetBufferPointer();

        [PreserveSig]
        new UIntPtr GetBufferSize();
    }


    // IID_IDxcUtils
    [ComImport]
    [Guid("4d5e80d7-d4d1-4574-88cc-33b006c21209")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcUtils : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // HRESULT CreateBlobFromBlob(IDxcBlob* pBlob, UINT32 offset, UINT32 length, IDxcBlob** ppResult);
        void CreateBlobFromBlob(); // Placeholder, not implemented

        // HRESULT CreateBlobFromFile(LPCWSTR pFileName, UINT32* pCodePage, IDxcBlobEncoding** pResult);
        void CreateBlobFromFile(); // Placeholder, not implemented

        // HRESULT CreateBlobWithEncodingFromPinned(LPCVOID pText, UINT32 size, UINT32 codePage, IDxcBlobEncoding** pResult);
        [PreserveSig]
        int CreateBlobWithEncodingFromPinned(
            IntPtr pText,
            uint size,
            uint codePage,
            out IDxcBlobEncoding ppResult
        );

        // HRESULT CreateBlobWithEncodingOnHeapCopy(LPCVOID pText, UINT32 size, UINT32 codePage, IDxcBlobEncoding** pResult);
        void CreateBlobWithEncodingOnHeapCopy(); // Placeholder, not implemented

        // HRESULT CreateResizableBlob(UINT32 initialCapacity, IMalloc* pIMalloc, IDxcBlob** ppResult);
        void CreateResizableBlob(); // Placeholder, not implemented

        // HRESULT Malloc(IMalloc** ppResult);
        void Malloc(); // Placeholder, not implemented

        // HRESULT CreateReadStreamFromBlob(IDxcBlob* pBlob, IDxcBlobReadStream** ppResult);
        void CreateReadStreamFromBlob(); // Placeholder, not implemented

        // HRESULT GetDxilContainerPart(IDxcBlob* pDxilContainer, UINT32 idx, IDxcBlob** ppResult);
        void GetDxilContainerPart(); // Placeholder, not implemented

        // HRESULT GetDxilContainerPartCount(IDxcBlob* pDxilContainer, UINT32* pResult);
        void GetDxilContainerPartCount(); // Placeholder, not implemented

        // HRESULT GetDxilContainerPart(IDxcBlob* pDxilContainer, UINT32 idx, IDxcBlob** ppResult);
        void GetDxilContainerPartByIndex(); // Placeholder, not implemented

        // HRESULT CreateReflection(IDxcBlob* pData, REFIID iid, void** ppResult);
        [PreserveSig]
        int CreateReflection(IDxcBlob pData, ref Guid iid, out IntPtr ppvObject);

        // HRESULT BuildArguments(DxcArgBuilderFlags Flags, LPCWSTR pEntryPoint, LPCWSTR pTargetProfile,
        //                        LPCWSTR* pArguments, UINT32 argCount,
        //                        DxcDefine* pDefines, UINT32 defineCount,
        //                        IDxcIncludeHandler* pIncludeHandler, IDxcCompilerArgs** ppResult);
        void BuildArguments(); // Placeholder, not implemented
    }


    // IID_IDxcCompiler3
    [ComImport]
    [Guid("22f8cf51-28d0-4e97-8357-752156ed2a04")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcCompiler3 : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // HRESULT Compile(
        //   IDxcBuffer                 *pSource,
        //   LPCWSTR                    *pArguments,
        //   UINT32                     argCount,
        //   IDxcIncludeHandler         *pIncludeHandler,
        //   REFIID                     riid,
        //   LPVOID                     *ppResult
        // );
        [PreserveSig]
        int Compile(
            IDxcBuffer pSource,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 2)]
            string[] pArguments,
            uint argCount,
            IntPtr pIncludeHandler, // Use IntPtr for IDxcIncludeHandler for now
            ref Guid riid,
            out IntPtr ppResult // This will be an IDxcResult
        );
    }

    // IID_IDxcResult
    [ComImport]
    [Guid("58346cdd-ce7b-44f9-9509-a052fd6ed1b4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcResult : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // HRESULT GetStatus(HRESULT* pStatus);
        [PreserveSig]
        int GetStatus(out int pStatus);

        // HRESULT GetResult(IDxcBlob** ppResult);
        [PreserveSig]
        int GetResult(out IDxcBlob ppResult);

        // HRESULT GetErrorBuffer(IDxcBlobEncoding** ppErrors);
        [PreserveSig]
        int GetErrorBuffer(out IDxcBlobEncoding ppErrors);

        // HRESULT GetOutput(DxcOutKind OutKind, REFIID IID, LPVOID* ppResult, IDxcBlobUtf16** ppOutputName);
        void GetOutput(); // Placeholder

        // HRESULT GetOpcode(DxcOpcode* pOpcode);
        void GetOpcode(); // Placeholder

        // HRESULT GetOutputs(UINT32* pCount);
        void GetOutputs(); // Placeholder

        // HRESULT GetPrimaryOutput(REFIID IID, LPVOID* ppResult, IDxcBlobUtf16** ppOutputName);
        void GetPrimaryOutput(); // Placeholder

        // HRESULT HasOutput(DxcOutKind OutKind);
        void HasOutput(); // Placeholder
    }


    // For DxcCreateInstance
    [Guid("624ce670-3603-4edc-9137-1c0a218ce052")] // CLSID_DxcCompiler
    internal class DxcCompilerClass { }

    [Guid("cd1f6b67-2ab0-482d-8b9d-cd7cba4a0805")] // CLSID_DxcUtils
    internal class DxcUtilsClass { }


    internal static class DxcNative
    {
        private const string DxcLibraryName = "dxcompiler.dll";

        [DllImport(DxcLibraryName, ExactSpelling = true)]
        public static extern int DxcCreateInstance(
            ref Guid rclsid,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)]
            out object ppv
        );

        public static T CreateDxcInstance<T>(Guid clsid, Guid iid)
        {
            object obj;
            int hr = DxcCreateInstance(ref clsid, ref iid, out obj);
            if (hr != 0)
            {
                // HRESULT S_FALSE (1) is also success for some DXC functions, but DxcCreateInstance should be S_OK (0)
                if (hr != 0) // S_OK
                {
                    Marshal.ThrowExceptionForHR(hr);
                }
            }
            return (T)obj;
        }
    }

    internal class DxcCompiler
    {
        private IDxcCompiler3 _compiler;
        private IDxcUtils _utils;

        public DxcCompiler()
        {
            _compiler = DxcNative.CreateDxcInstance<IDxcCompiler3>(
                typeof(DxcCompilerClass).GUID, typeof(IDxcCompiler3).GUID
            );
            _utils = DxcNative.CreateDxcInstance<IDxcUtils>(
                typeof(DxcUtilsClass).GUID, typeof(IDxcUtils).GUID
            );
        }

        public byte[] CompileToSpirV(string source, string entryPoint, string targetProfile)
        {
            var sourceBytes = Encoding.UTF8.GetBytes(source);
            
            // Allocate unmanaged memory for the source code
            IntPtr pSource = Marshal.AllocHGlobal(sourceBytes.Length);
            Marshal.Copy(sourceBytes, 0, pSource, sourceBytes.Length);

            IDxcBlobEncoding sourceBlobEncoding;
            int hr = _utils.CreateBlobWithEncodingFromPinned(pSource, (uint)sourceBytes.Length, 0, out sourceBlobEncoding);
            if (hr != 0)
            {
                Marshal.FreeHGlobal(pSource);
                Marshal.ThrowExceptionForHR(hr);
            }

            IDxcBuffer sourceBuffer = (IDxcBuffer)sourceBlobEncoding; // Cast to IDxcBuffer

            var args = new string[]
            {
                "-E", entryPoint,
                "-T", targetProfile,
                "-spirv", // Compile to SPIR-V
                "-fvk-use-dx-layout", // Use DX memory layout
                "-fspv-target-env=vulkan1.2", // Target Vulkan 1.2
                "-O0", // No optimization (for easier analysis)
                "-Fd", "foo.pdb" // Generate pdb for debug info
            };

            Guid IDxcResult_GUID = typeof(IDxcResult).GUID;
            IntPtr pResultPtr;

            hr = _compiler.Compile(
                sourceBuffer,
                args,
                (uint)args.Length,
                IntPtr.Zero, // No include handler for now
                ref IDxcResult_GUID,
                out pResultPtr
            );
            
            // Release the source blob. The compiler copies the content, so we can release it.
            Marshal.ReleaseComObject(sourceBlobEncoding); 
            Marshal.FreeHGlobal(pSource); // Free the unmanaged memory

            if (hr != 0)
            {
                Marshal.Release(pResultPtr); // Always release the result pointer if not handled further
                Marshal.ThrowExceptionForHR(hr);
            }

            IDxcResult compileResult = (IDxcResult)Marshal.GetObjectForIUnknown(pResultPtr);

            int status;
            compileResult.GetStatus(out status);

            if (status != 0) // S_OK is 0
            {
                IDxcBlobEncoding errorBlob;
                compileResult.GetErrorBuffer(out errorBlob);
                string errorMessages = Marshal.PtrToStringAnsi(errorBlob.GetBufferPointer());
                Marshal.ReleaseComObject(errorBlob);
                Marshal.ReleaseComObject(compileResult);
                Marshal.Release(pResultPtr);
                throw new Exception($"Shader compilation failed with status {status}: {errorMessages}");
            }

            IDxcBlob spirvBlob;
            compileResult.GetResult(out spirvBlob);

            byte[] spirvBytes = new byte[(int)spirvBlob.GetBufferSize()];
            Marshal.Copy(spirvBlob.GetBufferPointer(), spirvBytes, 0, (int)spirvBlob.GetBufferSize());
            
            Marshal.ReleaseComObject(spirvBlob);
            Marshal.ReleaseComObject(compileResult);
            Marshal.Release(pResultPtr); // Release the final result pointer

            return spirvBytes;
        }
    }
}