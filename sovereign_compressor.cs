using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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

        private static readonly byte[] ShinobiMaskKey = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFA, 0xCE, 0x01, 0x02 };

        #region --- Native P/Invoke Declarations ---

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

        #endregion

        #region --- High-Level Managed Operations ---

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

        #endregion

        #region --- Advanced Shinobi & Ocular Tactical Methods ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ApplyGhostingMask(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return;

            fixed (byte* pBuffer = buffer)
            {
                int len = buffer.Length;
                ulong* pULong = (ulong*)pBuffer;
                int blocks = len / 8;

                fixed (byte* pKey = ShinobiMaskKey)
                {
                    ulong keyMask = *(ulong*)pKey;
                    for (int i = 0; i < blocks; i++)
                    {
                        pULong[i] ^= keyMask;
                    }

                    int tailStart = blocks * 8;
                    for (int i = tailStart; i < len; i++)
                    {
                        pBuffer[i] ^= ShinobiMaskKey[i % 8];
                    }
                }
            }
        }

        public static unsafe long CompressZeroCopyStealth(IntPtr inputPtr, int inputLen, IntPtr outputPtr, int maxOutputLen, int compressionLevel = 3)
        {
            if (inputPtr == IntPtr.Zero || outputPtr == IntPtr.Zero) 
                throw new ArgumentNullException("Pointers cannot be null for stealth zero-copy operations.");

            long bytesWritten = sovereign_compress_chunk_zerocopy(
                inputPtr, 
                (UIntPtr)inputLen, 
                outputPtr, 
                (UIntPtr)maxOutputLen, 
                compressionLevel
            );

            if (bytesWritten > 0)
            {
                Span<byte> compressedSpan = new Span<byte>((void*)outputPtr, (int)bytesWritten);
                ApplyGhostingMask(compressedSpan);
            }

            return bytesWritten;
        }

        /// <summary>
        /// OCULAR TACTIC: AVX2 SIMD Memory Perception
        /// Inspects memory blocks via 256-bit registers to instantly spot zero-padding and structural patterns.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool ScanZeroPaddingVectorized(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return true;

            fixed (byte* pData = data)
            {
                int len = data.Length;
                int i = 0;

                if (Avx2.IsSupported && len >= 32)
                {
                    Vector256<byte> zeroVector = Vector256<byte>.Zero;
                    for (; i <= len - 32; i += 32)
                    {
                        Vector256<byte> currentBlock = Avx2.LoadVector256(pData + i);
                        Vector256<byte> cmp = Avx2.CompareEqual(currentBlock, zeroVector);
                        int mask = Avx2.MoveMask(cmp);

                        // If all 32 bytes are not equal to zero, padding check fails
                        if ((uint)mask != 0xFFFFFFFF) return false;
                    }
                }

                for (; i < len; i++)
                {
                    if (pData[i] != 0) return false;
                }
            }

            return true;
        }

        #endregion

        #region --- TAILED BEAST CHAKRA POOL (Unmanaged Memory Block) ---

        /// <summary>
        /// TAILED BEAST TACTIC: Off-Heap Chakra Reservoir
        /// Allocates zero-allocation unmanaged memory chunks managed outside GC bounds.
        /// </summary>
        public sealed class TailedBeastChakraPool : IDisposable
        {
            public IntPtr NativePointer { get; private set; }
            public long SizeInBytes { get; private set; }
            private bool _disposed;

            public unsafe TailedBeastChakraPool(long size)
            {
                SizeInBytes = size;
                // Allocate unmanaged aligned memory directly from OS heap
                NativePointer = (IntPtr)NativeMemory.Alloc((nuint)size);
                NativeMemory.Clear((void*)NativePointer, (nuint)size);
            }

            public unsafe Span<byte> AsSpan()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TailedBeastChakraPool));
                return new Span<byte>((void*)NativePointer, (int)SizeInBytes);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    if (NativePointer != IntPtr.Zero)
                    {
                        unsafe { NativeMemory.Free((void*)NativePointer); }
                        NativePointer = IntPtr.Zero;
                    }
                    _disposed = true;
                }
            }
        }

        #endregion
    }
}
