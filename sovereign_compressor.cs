using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SovereignEngine.Native
{
    /// <summary>
    /// Sovereign Virtual Spacetime SSD Compression Engine.
    /// Orchestrates native zero-copy I/O, SIMD vectorization, Fourier spectral filtering,
    /// and dynamic Geodesic trajectory calculations.
    /// </summary>
    public static class SovereignCompressor
    {
        private const string LibraryName = "sovereign_compressor";

        public const int SOVEREIGN_SUCCESS = 0;
        public const int SOVEREIGN_ERR_NULL_POINTER = -1;
        public const int SOVEREIGN_ERR_BUFFER_TOO_SMALL = -2;
        public const int SOVEREIGN_ERR_COMPRESSION_FAILED = -3;
        public const int SOVEREIGN_ERR_DECOMPRESSION_FAILED = -4;

        // --- FIELD GOVERNOR & MATRIX RATIOS ---
        private const int FIELD_GOVERNOR_BASE = 84;
        private const double LIGHT_MATRIX_RATIO = 2.0 / 7.0; 
        private const int LAMBDA_BRIDGE_THRESHOLD = 144_000; // 144 KB Spatial Trigger Boundary

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

        #region --- GEODESIC TRAJECTORY & FOURIER SPECTRAL MECHANICS ---

        /// <summary>
        /// Calculates the Brus Quantum Fourier frequency spectral response across a byte memory window.
        /// Evaluates high-frequency byte distribution entropy to skip compression on non-compressible streams.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double CalculateFourierSpectralEntropy(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty) return 0.0;

            int len = buffer.Length;
            int sampleSize = Math.Min(len, 4096);
            int step = Math.Max(1, len / sampleSize);

            fixed (byte* pBuf = buffer)
            {
                Span<uint> fourierBins = stackalloc uint[16];
                fourierBins.Clear();

                int sampleCount = 0;
                for (int idx = 0; idx < len; idx += step)
                {
                    byte b = pBuf[idx];
                    fourierBins[b & 0x0F]++;
                    sampleCount++;
                }

                if (sampleCount == 0) return 0.0;

                double entropy = 0.0;
                double invTotal = 1.0 / sampleCount;

                for (int b = 0; b < 16; b++)
                {
                    if (fourierBins[b] > 0)
                    {
                        double p = fourierBins[b] * invTotal;
                        entropy -= p * Math.Log2(p);
                    }
                }

                return entropy / 4.0;
            }
        }

        /// <summary>
        /// Computes Geodesic Trajectory Acceleration (d^2 x^alpha / d tau^2 = 0)
        /// and UGPE to determine dimensional overwrite, dynamic leveling, and core thread allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalculateUGPEAndGeodesic(
            int inputLength, 
            int requestedLevel, 
            double spectralEntropy, 
            out bool triggerDimensionalOverwrite, 
            out int optimizedCompressionLevel)
        {
            double p = Math.Max(1, requestedLevel);
            double eta = (1.0 + (p * 0.15)) * (1.1 - spectralEntropy); 
            double r = Avx2.IsSupported ? 0.25 : 1.0; 
            double c = inputLength / (double)LAMBDA_BRIDGE_THRESHOLD;

            double ugpe = (inputLength * p * eta) / (r * Math.Max(c, 0.001));

            triggerDimensionalOverwrite = inputLength >= LAMBDA_BRIDGE_THRESHOLD || ugpe >= 144000.0;

            if (spectralEntropy > 0.95)
            {
                optimizedCompressionLevel = 1;
            }
            else if (triggerDimensionalOverwrite)
            {
                optimizedCompressionLevel = Math.Min(requestedLevel, 5);
            }
            else
            {
                optimizedCompressionLevel = requestedLevel;
            }

            return ugpe;
        }

        /// <summary>
        /// 84 Field Governor Dynamic Core Calculator.
        /// Uses the Light-Matrix Ratio (2/7) to determine dynamic worker allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateGovernorThreads()
        {
            int sysCores = Environment.ProcessorCount;
            int governorScaled = (int)Math.Ceiling(sysCores * LIGHT_MATRIX_RATIO);
            return Math.Clamp(governorScaled, 2, FIELD_GOVERNOR_BASE);
        }

        #endregion

        #region --- High-Level Managed Operations ---

        public static unsafe byte[] Compress(ReadOnlySpan<byte> input, int compressionLevel = 3)
        {
            if (input.IsEmpty) return Array.Empty<byte>();

            double spectralEntropy = CalculateFourierSpectralEntropy(input);
            CalculateUGPEAndGeodesic(input.Length, compressionLevel, spectralEntropy, out _, out int effectiveLevel);

            int capacity = input.Length + (input.Length >> 6) + 1024;
            byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);

            try
            {
                fixed (byte* pIn = input)
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res;

                    fixed (byte* pOut = rented)
                    {
                        res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pOut, (UIntPtr)rented.Length, &written, effectiveLevel);
                    }

                    if (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                        capacity *= 2;
                        rented = ArrayPool<byte>.Shared.Rent(capacity);

                        fixed (byte* pRetry = rented)
                        {
                            res = NativeCompressChunk(pIn, (UIntPtr)input.Length, pRetry, (UIntPtr)rented.Length, &written, effectiveLevel);
                        }
                    }

                    if (res != SOVEREIGN_SUCCESS) 
                        throw new ExternalException($"Compression failed with native error code: {res}");

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
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res;

                    fixed (byte* pOut = rented)
                    {
                        res = NativeDecompressChunk(pIn, (UIntPtr)compressedInput.Length, pOut, (UIntPtr)rented.Length, &written);
                    }

                    while (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                        capacity *= 2;
                        rented = ArrayPool<byte>.Shared.Rent(capacity);

                        fixed (byte* pRetry = rented)
                        {
                            res = NativeDecompressChunk(pIn, (UIntPtr)compressedInput.Length, pRetry, (UIntPtr)rented.Length, &written);
                        }
                    }

                    if (res != SOVEREIGN_SUCCESS) 
                        throw new ExternalException($"Decompression failed with native error code: {res}");

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

                if (Avx2.IsSupported && len >= 32)
                {
                    ulong keyPattern = 0x0201CEFAEFBEADDE; 
                    Vector256<ulong> maskVector = Vector256.Create(keyPattern, keyPattern, keyPattern, keyPattern);
                    Vector256<byte> maskBytes = maskVector.AsByte();

                    for (; i <= len - 32; i += 32)
                    {
                        Vector256<byte> current = Avx2.LoadVector256(pBuffer + i);
                        Vector256<byte> xorResult = Avx2.Xor(current, maskBytes);
                        Avx2.Store(pBuffer + i, xorResult);
                    }
                }

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

            ReadOnlySpan<byte> inputSpan = new ReadOnlySpan<byte>((void*)inputPtr, inputLen);
            double entropy = CalculateFourierSpectralEntropy(inputSpan);
            CalculateUGPEAndGeodesic(inputLen, compressionLevel, entropy, out _, out int effectiveLevel);

            long bytesWritten = sovereign_compress_chunk_zerocopy(
                inputPtr, 
                (UIntPtr)inputLen, 
                outputPtr, 
                (UIntPtr)maxOutputLen, 
                effectiveLevel
            );

            if (bytesWritten > 0)
            {
                Span<byte> compressedSpan = new Span<byte>((void*)outputPtr, (int)bytesWritten);
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
                if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Pool size must be greater than zero.");

                SizeInBytes = size;
                NativePointer = (IntPtr)NativeMemory.Alloc((nuint)size);
                NativeMemory.Clear((void*)NativePointer, (nuint)size);
            }

            public unsafe Span<byte> AsSpan()
            {
                if (_disposed || NativePointer == IntPtr.Zero) 
                    throw new ObjectDisposedException(nameof(TailedBeastChakraPool));

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
                GC.SuppressFinalize(this);
            }

            ~TailedBeastChakraPool()
            {
                Dispose();
            }
        }

        #endregion
    }
}
