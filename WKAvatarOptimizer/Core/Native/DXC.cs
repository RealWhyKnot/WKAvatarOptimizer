using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace WKAvatarOptimizer.Core.Native
{
    #region Interfaces

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

    [StructLayout(LayoutKind.Sequential)]
    internal struct DxcBuffer
    {
        public IntPtr Ptr;
        public UIntPtr Size;
        public uint Encoding;
    }

    [ComImport]
    [Guid("e5204dc7-d18c-4c3c-bdfb-851673980fe7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcLibrary : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        // 3: SetMalloc
        void SetMalloc();

        // 4: CreateBlobFromBlob
        void CreateBlobFromBlob();

        // 5: CreateBlobFromFile
        void CreateBlobFromFile();

        // 6: CreateBlobWithEncodingFromPinned
        void CreateBlobWithEncodingFromPinned();

        // 7: CreateBlobWithEncodingOnHeapCopy
        [PreserveSig]
        int CreateBlobWithEncodingOnHeapCopy(
            IntPtr pText,
            uint size,
            uint codePage,
            out IDxcBlobEncoding ppResult
        );
    }

    [ComImport]
    [Guid("228b4687-5a6a-4730-900c-9702b2203f54")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcCompiler3 : IUnknown
    {
        new IntPtr QueryInterface(ref Guid riid, out IntPtr ppvObject);
        new uint AddRef();
        new uint Release();

        [PreserveSig]
        int Compile(
            [In] ref DxcBuffer pSource,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 2)]
            string[] pArguments,
            uint argCount,
            IntPtr pIncludeHandler,
            ref Guid riid,
            out IntPtr ppResult
        );

        void Disassemble();
    }

    [ComImport, Guid("58346c82-7ed3-42d0-bc51-63636e677ed3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDxcResult
    {
        // IDxcOperationResult methods (Slots 3-5)
        [PreserveSig]
        int GetStatus(out int pStatus); // 3

        [PreserveSig]
        int GetResult([MarshalAs(UnmanagedType.Interface)] out IDxcBlob ppResult); // 4

        [PreserveSig]
        int GetErrorBuffer([MarshalAs(UnmanagedType.Interface)] out IDxcBlobEncoding ppErrors); // 5

        // IDxcResult specific methods (Slots 6-10)
        [PreserveSig]
        int HasOutput(uint dxcOutKind, [MarshalAs(UnmanagedType.Bool)] out bool pHasOutput); // 6

        [PreserveSig]
        int GetOutput(uint dxcOutKind, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject, [MarshalAs(UnmanagedType.Interface)] out IDxcBlob ppOutputName); // 7

        [PreserveSig]
        int GetNumOutputs(out uint pNumOutputs); // 8

        [PreserveSig]
        int GetOutputByIndex(uint Index, out uint pKind); // 9

        [PreserveSig]
        int GetPrimaryOutput(out uint pKind); // 10
    }

    #endregion

    #region Classes

    [Guid("73e22d93-e6ce-47f3-b5bf-f0664f39c1b0")] 
    internal class DxcCompilerClass { }

    [Guid("6245d6af-66e0-48fd-80b4-4d271796748c")] 
    internal class DxcUtilsClass { }

    #endregion

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
                if (TryLoadFromPluginsFolder()) return;

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
                        }
                    }
                    catch (IOException) {}
                }

                string dxilPath = Path.Combine(tempFolder, "dxil.dll");
                if (File.Exists(dxilPath) && GetModuleHandle("dxil.dll") == IntPtr.Zero) LoadLibrary(dxilPath);

                string dxcPath = Path.Combine(tempFolder, "dxcompiler.dll");
                if (File.Exists(dxcPath) && GetModuleHandle("dxcompiler.dll") == IntPtr.Zero) LoadLibrary(dxcPath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WKAvatarOptimizer] Exception during native library loading: {ex}");
            }
        }

        private static bool TryLoadFromPluginsFolder()
        {
            try {
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyDir = Path.GetDirectoryName(assembly.Location);
                string[] searchPaths = {
                    Path.Combine(assemblyDir, "Plugins", "x86_64"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "WKAvatarOptimizer", "Plugins", "x86_64"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Packages", "com.wk.avataroptimizer", "Plugins", "x86_64")
                };

                foreach (var path in searchPaths)
                {
                    string dxil = Path.Combine(path, "dxil.dll");
                    string dxc = Path.Combine(path, "dxcompiler.dll");
                    if (File.Exists(dxil) && File.Exists(dxc))
                    {
                        if (GetModuleHandle("dxil.dll") == IntPtr.Zero) LoadLibrary(dxil);
                        if (GetModuleHandle("dxcompiler.dll") == IntPtr.Zero) LoadLibrary(dxc);
                        return true;
                    }
                }
            } catch { }
            return false;
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
                Marshal.ThrowExceptionForHR(hr);
            }
            return (T)Marshal.GetObjectForIUnknown(ptr);
        }
    }

    public class DxcConfiguration
    {
        public string EntryPoint { get; set; } = "main";
        public string TargetProfile { get; set; } = "ps_6_0";
        public bool OutputSpirV { get; set; } = true;
        public bool VulkanLayout { get; set; } = true;
        public string VulkanTarget { get; set; } = "vulkan1.2";
        public bool Optimization { get; set; } = false;

        public string[] ToArguments()
        {
            var args = new List<string>
            {
                "-E", EntryPoint,
                "-T", TargetProfile
            };
            if (OutputSpirV) args.Add("-spirv");
            if (VulkanLayout) args.Add("-fvk-use-dx-layout");
            if (!string.IsNullOrEmpty(VulkanTarget)) args.Add("-fspv-target-env=" + VulkanTarget);
            args.Add(Optimization ? "-O3" : "-O0");
            return args.ToArray();
        }
    }

    internal class DxcCompiler
    {
        private IDxcCompiler3 _compiler;
        // Utils/Library not strictly needed for compilation if we use DxcBuffer directly
        
        public DxcCompiler()
        {
            _compiler = DxcNative.CreateDxcInstance<IDxcCompiler3>(
                typeof(DxcCompilerClass).GUID, typeof(IDxcCompiler3).GUID
            );
        }

        public byte[] CompileToSpirV(string source, DxcConfiguration config)
        {
            if (string.IsNullOrEmpty(source)) throw new ArgumentException("Shader source is empty", nameof(source));
            if (_compiler == null) throw new InvalidOperationException("DXC compiler not initialized");

            var sourceBytes = Encoding.UTF8.GetBytes(source);
            IntPtr pSource = Marshal.AllocHGlobal(sourceBytes.Length);
            Marshal.Copy(sourceBytes, 0, pSource, sourceBytes.Length);

            IDxcResult compileResult = null;
            IDxcBlob spirvBlob = null;
            IntPtr pResultPtr = IntPtr.Zero;

            try
            {
                // Direct DxcBuffer construction - avoids CreateBlob crashes
                DxcBuffer sourceBuffer = new DxcBuffer
                {
                    Ptr = pSource,
                    Size = (UIntPtr)sourceBytes.Length,
                    Encoding = 65001 // UTF-8
                };

                string[] args = config.ToArguments();
                Guid IDxcResult_GUID = typeof(IDxcResult).GUID;

                int hr = _compiler.Compile(
                    ref sourceBuffer,
                    args,
                    (uint)args.Length,
                    IntPtr.Zero,
                    ref IDxcResult_GUID,
                    out pResultPtr
                );

                if (hr != 0) Marshal.ThrowExceptionForHR(hr);
                if (pResultPtr == IntPtr.Zero) throw new Exception("Compile returned null result pointer");

                compileResult = (IDxcResult)Marshal.GetObjectForIUnknown(pResultPtr);
                if (compileResult == null) throw new Exception("Failed to get IDxcResult from pointer");

                int status;
                compileResult.GetStatus(out status);

                if (status != 0)
                {
                    IDxcBlobEncoding errorBlob;
                    compileResult.GetErrorBuffer(out errorBlob);
                    string errorMessages = "Unknown compilation error";
                    if (errorBlob != null)
                    {
                        IntPtr pError = errorBlob.GetBufferPointer();
                        errorMessages = Marshal.PtrToStringAnsi(pError);
                        Marshal.ReleaseComObject(errorBlob);
                    }
                    throw new Exception($"Shader compilation failed with status {status}: {errorMessages}");
                }

                compileResult.GetResult(out spirvBlob);
                if (spirvBlob == null) throw new Exception("Compilation succeeded but no SPIR-V blob was returned.");

                byte[] spirvBytes = new byte[(int)spirvBlob.GetBufferSize().ToUInt32()];
                Marshal.Copy(spirvBlob.GetBufferPointer(), spirvBytes, 0, spirvBytes.Length);
                
                return spirvBytes;
            }
            finally
            {
                if (spirvBlob != null) Marshal.ReleaseComObject(spirvBlob);
                if (compileResult != null) Marshal.ReleaseComObject(compileResult);
                if (pResultPtr != IntPtr.Zero) Marshal.Release(pResultPtr);
                Marshal.FreeHGlobal(pSource);
            }
        }
    }
}