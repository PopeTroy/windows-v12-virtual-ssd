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

        // Threshold Constants derived from Overwrite Equations
        private const int LAMBDA_BRIDGE_THRESHOLD = 144_000; // 144 KB Trigger
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

        #region --- UGPE & Overwrite Mathematical Diagnostics ---

        /// <summary>
        /// Computes Unified Grand Potential (UGPE) to dynamically determine if
        /// local rules must be overridden by zero-copy stealth execution paths.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalculateUGPE(int inputLength, int compressionLevel, out bool triggerDimensionalOverwrite)
        {
            // P = Compression Level, η = Estimated Efficiency Scale, R = Overhead Cost, C = Buffer Cap ratio
            double p = Math.Max(1, compressionLevel);
            double eta = 1.0 + (p * 0.15); // Efficiency multiplier
            double r = Avx2.IsSupported ? 0.25 : 1.0; // Resistance reduced with SIMD acceleration
            double c = inputLength / (double)LAMBDA_BRIDGE_THRESHOLD;

            // UGPE integral approximation
            double ugpe = (inputLength * p * eta) / (r * Math.Max(c, 0.001));

            // Heaviside Step Function Check: Threshold = 144,000
            triggerDimensionalOverwrite = inputLength >= LAMBDA_BRIDGE_THRESHOLD || ugpe >= 144000.0;
            return ugpe;
        }

        #endregion

        #region --- High-Level Managed Operations ---

        public static unsafe byte[] Compress(ReadOnlySpan<byte> input, int compressionLevel = 3)
        {
            if (input.IsEmpty) return Array.Empty<byte>();

            // Calculate state mechanics
            CalculateUGPE(input.Length, compressionLevel, out bool triggerOverwrite);

            // Dynamic capacity estimation to guarantee zero retry overhead
            int capacity = input.Length + (input.Length >> 6) + 1024;
            byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);

            try
            {
                fixed (byte* pIn = input)
                fixed (byte* pOut = rented)
                {
                    UIntPtr written = UIntPtr.Zero;

                    // If Overwrite condition met, adjust compression level dynamically for max throughput
                    int effectiveLevel = triggerOverwrite ? Math.Min(compressionLevel, 5) : compressionLevel;

                    int res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pOut, (UIntPtr)rented.Length, &written, effectiveLevel);

                    if (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                        rented = ArrayPool<byte>.Shared.Rent(capacity * 2);
                        fixed (byte* pRetry = rented)
                        {
                            res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pRetry, (UIntPtr)rented.Length, &written, effectiveLevel);
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

        #region --- Advanced Stealth & SIMD Masking ---

        /// <summary>
        /// Optimized AVX2 Vectorized Masking pipeline.
        /// Applies 256-bit XOR operations to minimize loop resistance (R -> 0).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ApplyGhostingMask(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return;

            fixed (byte* pBuffer = buffer)
            {
                int len = buffer.Length;
                int i = 0;

                // AVX2 256-bit vectorized XOR pass
                if (Avx2.IsSupported && len >= 32)
                {
                    // Construct 256-bit vector with 8-byte key pattern repeated 4 times
                    ulong keyPattern = 0x0201CEFAEFBEADDE; // Little-endian 0xDE, 0xAD, 0xBE, 0xEF, 0xFA, 0xCE, 0x01, 0x02
                    Vector256<ulong> maskVector = Vector256.Create(keyPattern, keyPattern, keyPattern, keyPattern);
                    Vector256<byte> maskBytes = maskVector.AsByte();

                    for (; i <= len - 32; i += 32)
                    {
                        Vector256<byte> current = Avx2.LoadVector256(pBuffer + i);
                        Vector256<byte> xorResult = Avx2.Xor(current, maskBytes);
                        Avx2.Store(pBuffer + i, xorResult);
                    }
                }

                // 64-bit fallback for remaining full blocks
                int remaining = len - i;
                int ulongBlocks = remaining / 8;
                ulong* pULong = (ulong*)(pBuffer + i);

                fixed (byte* pKey = ShinobiMaskKey)
                {
                    ulong keyMask = *(ulong*)pKey;
                    for (int j = 0; j < ulongBlocks; j++)
                    {
                        pULong[j] ^= keyMask;
                    }
                }

                // Scalar tail execution
                int tailStart = i + (ulongBlocks * 8);
                for (int k = tailStart; k < len; k++)
                {
                    pBuffer[k] ^= ShinobiMaskKey[k % 8];
                }
            }
        }

        /// <summary>
        /// Executes zero-copy stealth compression with automated dimensional overwrite logic.
        /// </summary>
        public static unsafe long CompressZeroCopyStealth(IntPtr inputPtr, int inputLen, IntPtr outputPtr, int maxOutputLen, int compressionLevel = 3)
        {
            if (inputPtr == IntPtr.Zero || outputPtr == IntPtr.Zero) 
                throw new ArgumentNullException("Pointers cannot be null for stealth zero-copy operations.");

            // Check if Overwrite threshold is reached
            CalculateUGPE(inputLen, compressionLevel, out bool triggerOverwrite);

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
                
                // Vectorized masking pipeline
                ApplyGhostingMask(compressedSpan);
            }

            return bytesWritten;
        }

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

        #region --- TAILED BEAST CHAKRA POOL ---

        public sealed class TailedBeastChakraPool : IDisposable
        {
            public IntPtr NativePointer { get; private set; }
            public long SizeInBytes { get; private set; }
            private bool _disposed;

            public unsafe TailedBeastChakraPool(long size)
            {
                SizeInBytes = size;
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
