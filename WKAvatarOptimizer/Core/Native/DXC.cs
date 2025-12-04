using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace WKAvatarOptimizer.Core.Native
{
    // Common IUnknown interface
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

    // IID_IDxcBlob: 8ba5fb08-5195-40e2-ac58-0d989c3a0102
    [ComImport]
    [Guid("8ba5fb08-5195-40e2-ac58-0d989c3a0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcBlob : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        [PreserveSig]
        IntPtr GetBufferPointer();

        [PreserveSig]
        UIntPtr GetBufferSize();
    }

    // IID_IDxcBlobEncoding: 7f61fc7d-950d-4b82-9c32-f30a4c9e1cae
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

        [PreserveSig]
        int GetEncoding(out int pKnown, out uint pCodePage);
    }

    // DxcBuffer Struct (Not an interface!)
    [StructLayout(LayoutKind.Sequential)]
    internal struct DxcBuffer
    {
        public IntPtr Ptr;
        public UIntPtr Size;
        public uint Encoding;
    }

    // IID_IDxcUtils: 4605c46c-5573-4090-b08f-3764e1f35878
    [ComImport]
    [Guid("4605c46c-5573-4090-b08f-3764e1f35878")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcUtils : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // 3: CreateBlobFromBlob
        void CreateBlobFromBlob(); 

        // 4: CreateBlobFromFile
        void CreateBlobFromFile(); 

        // 5: CreateBlobWithEncodingFromPinned
        [PreserveSig]
        int CreateBlobWithEncodingFromPinned(
            IntPtr pText,
            uint size,
            uint codePage,
            out IDxcBlobEncoding ppResult
        );

        // ... other methods omitted as they are not used yet
    }

    // IID_IDxcCompiler3: 228b4687-5a6a-4730-900c-9702b2203f54
    [ComImport]
    [Guid("228b4687-5a6a-4730-900c-9702b2203f54")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcCompiler3 : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // 3: Compile
        [PreserveSig]
        int Compile(
            [In] ref DxcBuffer pSource,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 2)]
            string[] pArguments,
            uint argCount,
            IntPtr pIncludeHandler,
            ref Guid riid,
            out IntPtr ppResult // IDxcResult
        );
        
        // ... other methods omitted
    }

    // IID_IDxcResult: 58346cdd-ce7b-44f9-9509-a052fd6ed1b4
    [ComImport]
    [Guid("58346cdd-ce7b-44f9-9509-a052fd6ed1b4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcResult : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // IDxcOperationResult methods
        
        // 3: GetStatus
        [PreserveSig]
        int GetStatus(out int pStatus);

        // 4: GetResult (Primary Output)
        [PreserveSig]
        int GetResult(out IDxcBlob ppResult);

        // 5: GetErrorBuffer
        [PreserveSig]
        int GetErrorBuffer(out IDxcBlobEncoding ppErrors);

        // IDxcResult specific methods
        
        // 6: HasOutput
        void HasOutput();

        // 7: GetOutput
        void GetOutput();
        
        // ...
    }

    // CLSID_DxcCompiler: 73e22d93-e6ce-47f3-b5bf-f0664f39c1b0
    [Guid("73e22d93-e6ce-47f3-b5bf-f0664f39c1b0")] 
    internal class DxcCompilerClass { }

    // CLSID_DxcUtils: 624ce670-3603-4edc-9137-1c0a218ce052
    [Guid("624ce670-3603-4edc-9137-1c0a218ce052")] 
    internal class DxcUtilsClass { }

    internal static class DxcNative
    {
        private const string DxcLibraryName = "dxcompiler.dll";

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        static DxcNative()
        {
            // Try to load dxcompiler.dll and dxil.dll explicitly
            // dxil.dll is required for signing/validation in some DXC versions and should be loaded first or alongside.
            try
            {
                // Assume we are in the project root (Unity Editor default)
                string dxilPath = Path.GetFullPath("WKAvatarOptimizer/Plugins/x86_64/dxil.dll");
                if (File.Exists(dxilPath) && GetModuleHandle("dxil.dll") == IntPtr.Zero)
                {
                    LoadLibrary(dxilPath);
                }

                string dxcPath = Path.GetFullPath("WKAvatarOptimizer/Plugins/x86_64/dxcompiler.dll");
                if (File.Exists(dxcPath) && GetModuleHandle("dxcompiler.dll") == IntPtr.Zero)
                {
                    LoadLibrary(dxcPath);
                }
            }
            catch
            {
                // Silent fail, rely on default search paths
            }
        }

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
                Marshal.ThrowExceptionForHR(hr);
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

            IDxcBlobEncoding sourceBlobEncoding = null;
            IDxcResult compileResult = null;
            IDxcBlob spirvBlob = null;
            IntPtr pResultPtr = IntPtr.Zero;

            try
            {
                int hr = _utils.CreateBlobWithEncodingFromPinned(pSource, (uint)sourceBytes.Length, 65001 /* UTF8 */, out sourceBlobEncoding);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                DxcBuffer sourceBuffer = new DxcBuffer();
                sourceBuffer.Ptr = sourceBlobEncoding.GetBufferPointer();
                sourceBuffer.Size = sourceBlobEncoding.GetBufferSize();
                sourceBuffer.Encoding = 65001; // UTF-8

                var args = new string[]
                {
                    "-E", entryPoint,
                    "-T", targetProfile,
                    "-spirv",
                    "-fvk-use-dx-layout",
                    "-fspv-target-env=vulkan1.2",
                    "-O0" 
                };

                Guid IDxcResult_GUID = typeof(IDxcResult).GUID;

                hr = _compiler.Compile(
                    ref sourceBuffer,
                    args,
                    (uint)args.Length,
                    IntPtr.Zero,
                    ref IDxcResult_GUID,
                    out pResultPtr
                );

                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                compileResult = (IDxcResult)Marshal.GetObjectForIUnknown(pResultPtr);

                int status;
                compileResult.GetStatus(out status);

                if (status != 0) // S_OK is 0
                {
                    IDxcBlobEncoding errorBlob;
                    compileResult.GetErrorBuffer(out errorBlob);
                    string errorMessages = string.Empty;
                    if (errorBlob != null)
                    {
                        IntPtr pError = errorBlob.GetBufferPointer();
                        errorMessages = Marshal.PtrToStringAnsi(pError);
                        Marshal.ReleaseComObject(errorBlob);
                    }
                    throw new Exception($"Shader compilation failed with status {status}: {errorMessages}");
                }

                compileResult.GetResult(out spirvBlob);

                byte[] spirvBytes = new byte[(int)spirvBlob.GetBufferSize().ToUInt32()];
                Marshal.Copy(spirvBlob.GetBufferPointer(), spirvBytes, 0, spirvBytes.Length);
                
                return spirvBytes;
            }
            finally
            {
                if (sourceBlobEncoding != null) Marshal.ReleaseComObject(sourceBlobEncoding);
                if (spirvBlob != null) Marshal.ReleaseComObject(spirvBlob);
                if (compileResult != null) Marshal.ReleaseComObject(compileResult);
                if (pResultPtr != IntPtr.Zero) Marshal.Release(pResultPtr);
                Marshal.FreeHGlobal(pSource);
            }
        }
    }
}
