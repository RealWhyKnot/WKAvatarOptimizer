using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Reflection;

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

    // DxcBuffer Struct
    [StructLayout(LayoutKind.Sequential)]
    internal struct DxcBuffer
    {
        public IntPtr Ptr;
        public UIntPtr Size;
        public uint Encoding;
    }

    // IID_IDxcUtils: 4605C4CB-2019-492A-ADA4-65F20BB7D67F
    [ComImport]
    [Guid("4605C4CB-2019-492A-ADA4-65F20BB7D67F")]
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
    }

    // CLSID_DxcCompiler: 73e22d93-e6ce-47f3-b5bf-f0664f39c1b0
    [Guid("73e22d93-e6ce-47f3-b5bf-f0664f39c1b0")] 
    internal class DxcCompilerClass { }

    // CLSID_DxcUtils: 6245d6af-66e0-48fd-80b4-4d271796748c
    [Guid("6245d6af-66e0-48fd-80b4-4d271796748c")] 
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
            ExtractAndLoadLibraries();
        }

        private static void ExtractAndLoadLibraries()
        {
            try
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), "WKAvatarOptimizer_Runtime");
                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                string[] resources = { "dxil.dll", "dxcompiler.dll" };
                var assembly = Assembly.GetExecutingAssembly();
                
                foreach (var resName in resources)
                {
                    string resourcePath = $"WKAvatarOptimizer.Plugins.x86_64.{resName}";
                    string outputPath = Path.Combine(tempFolder, resName);

                    // Basic check to avoid constant rewriting if file exists and is open
                    // But for dev, we overwrite.
                    try {
                        using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
                        {
                            if (stream != null)
                            {
                                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                                {
                                    stream.CopyTo(fileStream);
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.LogError($"[WKAvatarOptimizer] Native resource not found: {resourcePath}");
                            }
                        }
                    }
                    catch (IOException) {
                        // File might be in use, which is fine if it's already loaded.
                    }
                }

                // Load dxil.dll
                string dxilPath = Path.Combine(tempFolder, "dxil.dll");
                if (File.Exists(dxilPath))
                {
                     IntPtr hDxil = LoadLibrary(dxilPath);
                     if (hDxil == IntPtr.Zero)
                     {
                         int err = Marshal.GetLastWin32Error();
                         UnityEngine.Debug.LogError($"[WKAvatarOptimizer] Failed to load dxil.dll from {dxilPath}. Error: {err}");
                     }
                }

                // Load dxcompiler.dll
                string dxcPath = Path.Combine(tempFolder, "dxcompiler.dll");
                if (File.Exists(dxcPath))
                {
                     IntPtr hDxc = LoadLibrary(dxcPath);
                     if (hDxc == IntPtr.Zero)
                     {
                         int err = Marshal.GetLastWin32Error();
                         UnityEngine.Debug.LogError($"[WKAvatarOptimizer] Failed to load dxcompiler.dll from {dxcPath}. Error: {err}");
                     }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WKAvatarOptimizer] Exception during native library loading: {ex}");
            }
        }

        [DllImport(DxcLibraryName, ExactSpelling = true)]
        public static extern int DxcCreateInstance(
            ref Guid rclsid,
            ref Guid riid,
            out IntPtr ppv
        );

        public static T CreateDxcInstance<T>(Guid clsid, Guid iid)
        {
            IntPtr ptr;
            int hr = DxcCreateInstance(ref clsid, ref iid, out ptr);
            
            if (hr != 0 || ptr == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[WKAvatarOptimizer] DxcCreateInstance failed for CLSID {clsid} / IID {iid}. HRESULT: 0x{hr:X}");
                Marshal.ThrowExceptionForHR(hr);
            }
            
            // Convert IntPtr to COM Object
            object obj = Marshal.GetObjectForIUnknown(ptr);
            return (T)obj;
        }
    }

    internal class DxcCompiler
    {
        private IDxcCompiler3 _compiler;
        private IDxcUtils _utils;

        public DxcCompiler()
        {
            try 
            {
                _compiler = DxcNative.CreateDxcInstance<IDxcCompiler3>(
                    typeof(DxcCompilerClass).GUID, typeof(IDxcCompiler3).GUID
                );
                
                _utils = DxcNative.CreateDxcInstance<IDxcUtils>(
                    typeof(DxcUtilsClass).GUID, typeof(IDxcUtils).GUID
                );
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WKAvatarOptimizer] Failed to initialize DXC Compiler: {ex}");
                throw;
            }
        }

        public byte[] CompileToSpirV(string source, string entryPoint, string targetProfile)
        {
            var sourceBytes = Encoding.UTF8.GetBytes(source);
            
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
