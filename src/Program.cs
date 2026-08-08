using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SovereignEngine.Native
{
    public static class SovereignCompressor
    {
        private const string LibraryName = "sovereign_compressor";

        static SovereignCompressor()
        {
            NativeLibrary.SetDllImportResolver(typeof(SovereignCompressor).Assembly, ResolveSovereignNativeLibrary);
        }

        private static IntPtr ResolveSovereignNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != LibraryName) return IntPtr.Zero;

            string baseDir = AppContext.BaseDirectory;
            string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "sovereign_compressor.dll" : "libsovereign_compressor.so";
            string candidate = Path.Combine(baseDir, fileName);

            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
            {
                return handle;
            }

            if (NativeLibrary.TryLoad(fileName, assembly, searchPath, out IntPtr defaultHandle))
            {
                return defaultHandle;
            }

            throw new DllNotFoundException($"Could not load native library '{fileName}'.");
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_compress_chunk")]
        private static unsafe extern int NativeCompressChunk(byte* inputPtr, UIntPtr inputLen, byte* outPtr, UIntPtr outCap, UIntPtr* outWritten, int level);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_decompress_chunk")]
        private static unsafe extern int NativeDecompressChunk(byte* inputPtr, UIntPtr inputLen, byte* outPtr, UIntPtr outCap, UIntPtr* outWritten);

        public static unsafe byte[] Compress(ReadOnlySpan<byte> input, int compressionLevel = 3)
        {
            if (input.IsEmpty) return Array.Empty<byte>();

            int cap = input.Length + (input.Length >> 8) + 512;
            byte[] rented = ArrayPool<byte>.Shared.Rent(cap);

            try
            {
                fixed (byte* pIn = input)
                fixed (byte* pOut = rented)
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pOut, (UIntPtr)rented.Length, &written, compressionLevel);

                    if (res != 0) throw new ExternalException($"Compression failed with code {res}");

                    byte[] result = new byte[(int)written];
                    Buffer.BlockCopy(rented, 0, result, 0, (int)written);
                    return result;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
