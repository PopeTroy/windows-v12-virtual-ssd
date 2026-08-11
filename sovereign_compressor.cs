using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace SovereignEngine.Native
{
    public static class SovereignCompressor
    {
        private const string LibraryName = "sovereign_compressor";

        private const int SOVEREIGN_SUCCESS = 0;
        private const int SOVEREIGN_ERR_NULL_POINTER = -1;
        private const int SOVEREIGN_ERR_BUFFER_TOO_SMALL = -2;
        private const int SOVEREIGN_ERR_COMPRESSION_FAILED = -3;
        private const int SOVEREIGN_ERR_DECOMPRESSION_FAILED = -4;

        // --- Original Native P/Invoke Declarations ---

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_compress_chunk")]
        private static unsafe extern int NativeCompressChunk(
            byte* inputPtr,
            UIntPtr inputLen,
            byte* outPtr,
            UIntPtr outCap,
            UIntPtr* outWritten,
            int compressionLevel
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_decompress_chunk")]
        private static unsafe extern int NativeDecompressChunk(
            byte* inputPtr,
            UIntPtr inputLen,
            byte* outPtr,
            UIntPtr outCap,
            UIntPtr* outWritten
        );

        // --- New Native P/Invoke Acceleration Declarations ---

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_compress_chunk_zerocopy")]
        public static extern long sovereign_compress_chunk_zerocopy(
            IntPtr inputPtr,
            UIntPtr inputLen,
            IntPtr outputPtr,
            UIntPtr maxOutputLen,
            int compressionLevel
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_hash_stream_parallel")]
        public static extern int sovereign_hash_stream_parallel(
            IntPtr dataPtr,
            UIntPtr len,
            UIntPtr chunkSize,
            IntPtr outHashesPtr
        );

        // --- High-Level Managed Methods ---

        public static unsafe byte[] Compress(ReadOnlySpan<byte> input, int compressionLevel = 3)
        {
            if (input.IsEmpty) return Array.Empty<byte>();

            int capacity = input.Length + (input.Length >> 8) + 512;
            byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);

            try
            {
                fixed (byte* pIn = input)
                fixed (byte* pOut = rented)
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pOut, (UIntPtr)rented.Length, &written, compressionLevel);

                    if (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                        rented = ArrayPool<byte>.Shared.Rent(capacity * 2);
                        fixed (byte* pRetry = rented)
                        {
                            res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pRetry, (UIntPtr)rented.Length, &written, compressionLevel);
                        }
                    }

                    if (res != SOVEREIGN_SUCCESS) throw new ExternalException($"Compression error code: {res}");

                    byte[] output = new byte[(int)written];
                    Buffer.BlockCopy(rented, 0, output, 0, (int)written);
                    return output;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public static unsafe byte[] Decompress(ReadOnlySpan<byte> compressedInput, int expectedUncompressedSize = 0)
        {
            if (compressedInput.IsEmpty) return Array.Empty<byte>();

            int capacity = expectedUncompressedSize > 0 ? expectedUncompressedSize : Math.Max(compressedInput.Length * 4, 64 * 1024);
            byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);

            try
            {
                fixed (byte* pIn = compressedInput)
                fixed (byte* pOut = rented)
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res = NativeDecompressChunk(pIn, (UIntPtr)compressedInput.Length, pOut, (UIntPtr)rented.Length, &written);

                    while (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        int newCap = rented.Length * 2;
                        ArrayPool<byte>.Shared.Return(rented);
                        rented = ArrayPool<byte>.Shared.Rent(newCap);

                        fixed (byte* pRetry = rented)
                        {
                            res = NativeDecompressChunk(pIn, (UIntPtr)compressedInput.Length, pRetry, (UIntPtr)rented.Length, &written);
                        }
                    }

                    if (res != SOVEREIGN_SUCCESS) throw new ExternalException($"Decompression error code: {res}");

                    byte[] output = new byte[(int)written];
                    Buffer.BlockCopy(rented, 0, output, 0, (int)written);
                    return output;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
