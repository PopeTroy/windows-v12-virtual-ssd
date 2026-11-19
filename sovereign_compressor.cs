using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SovereignEngine.Native
{
    public sealed record CompressionPolicy(
        int Level = 3,
        CompressionStrategy Strategy = CompressionStrategy.Balanced,
        double SkipThresholdEntropy = 0.95);

    public enum CompressionStrategy
    {
        Fast,
        Balanced,
        High,
        Stealth
    }

    public readonly record struct GeodesicTelemetry(
        double Ugpe,
        bool TriggerDimensionalOverwrite,
        int OptimizedCompressionLevel);

    public static class SovereignCompressor
    {
        private const string LibraryName = "sovereign_compressor";

        public const int SOVEREIGN_SUCCESS = 0;
        public const int SOVEREIGN_ERR_NULL_POINTER = -1;
        public const int SOVEREIGN_ERR_BUFFER_TOO_SMALL = -2;
        public const int SOVEREIGN_ERR_COMPRESSION_FAILED = -3;
        public const int SOVEREIGN_ERR_DECOMPRESSION_FAILED = -4;

        private const int FIELD_GOVERNOR_BASE = 84;
        private const double LIGHT_MATRIX_RATIO = 2.0 / 7.0;
        private const int LAMBDA_BRIDGE_THRESHOLD = 144_000;
        private const double UGPE_BASE_FRICTION = 0.666;
        private const double GRAVITY_CONSTANT = 9.80665;
        private const double SIMD_EFFICIENCY_AVX2 = 0.25;
        private const double SIMD_EFFICIENCY_SCALAR = 1.0;

        private static readonly byte[] ShinobiMaskKey = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xFA, 0xCE, 0x01, 0x02 };

        public static CompressionPolicy ActivePolicy { get; set; } = new();

        #region --- Native P/Invoke Declarations ---

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_compress_chunk")]
        private static unsafe extern int NativeCompressChunk(
            byte* inputPtr,
            nuint inputLen,
            byte* outPtr,
            nuint outCap,
            out UIntPtr outWritten,
            int compressionLevel
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_decompress_chunk")]
        private static unsafe extern int NativeDecompressChunk(
            byte* inputPtr,
            nuint inputLen,
            byte* outPtr,
            nuint outCap,
            out UIntPtr outWritten
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_compress_chunk_zerocopy")]
        public static extern long sovereign_compress_chunk_zerocopy(
            IntPtr inputPtr,
            nuint inputLen,
            IntPtr outputPtr,
            nuint maxOutputLen,
            int compressionLevel
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sovereign_hash_stream_parallel")]
        public static extern int sovereign_hash_stream_parallel(
            IntPtr dataPtr,
            nuint len,
            nuint chunkSize,
            IntPtr outHashesPtr
        );

        #endregion

        #region --- GEODESIC TRAJECTORY & ENTROPY HEURISTICS ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateNibbleEntropyEstimate(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty) return 0.0;

            int len = buffer.Length;
            int sampleSize = Math.Min(len, 4096);
            int step = Math.Max(1, len / sampleSize);

            Span<uint> nibbleBins = stackalloc uint[16];
            nibbleBins.Clear();

            int sampleCount = 0;
            for (int idx = 0; idx < len; idx += step)
            {
                byte b = buffer[idx];
                nibbleBins[b & 0x0F]++;
                sampleCount++;
            }

            if (sampleCount == 0) return 0.0;

            double entropy = 0.0;
            double invTotal = 1.0 / sampleCount;

            for (int i = 0; i < 16; i++)
            {
                if (nibbleBins[i] > 0)
                {
                    double p = nibbleBins[i] * invTotal;
                    entropy -= p * Math.Log2(p);
                }
            }

            return Math.Clamp(entropy / 4.0, 0.0, 1.0);
        }

        [Obsolete("Use CalculateNibbleEntropyEstimate instead.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateFourierSpectralEntropy(ReadOnlySpan<byte> buffer)
            => CalculateNibbleEntropyEstimate(buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CalculateByteCorrelationEntropy(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 2) return 0.0;

            int len = buffer.Length;
            int sampleSize = Math.Min(len - 1, 4096);
            int step = Math.Max(1, (len - 1) / sampleSize);

            int diffCount = 0;
            int comparisons = 0;

            for (int i = 0; i < len - 1; i += step)
            {
                if (buffer[i] != buffer[i + 1])
                {
                    diffCount++;
                }
                comparisons++;
            }

            if (comparisons == 0) return 0.0;
            return Math.Clamp((double)diffCount / comparisons, 0.0, 1.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GeodesicTelemetry EvaluateGeodesicAndUGPE(
            long inputLength,
            CompressionPolicy? policy,
            double nibbleEntropy,
            double correlationEntropy = 0.5)
        {
            CompressionPolicy active = policy ?? ActivePolicy;
            double levelFactor = Math.Max(1, active.Level);
            double efficiency = Avx2.IsSupported ? SIMD_EFFICIENCY_AVX2 : SIMD_EFFICIENCY_SCALAR;
            double bridgeRatio = Math.Max(inputLength / (double)LAMBDA_BRIDGE_THRESHOLD, 0.001);

            double combinedEntropy = Math.Clamp((nibbleEntropy * 0.7) + (correlationEntropy * 0.3), 0.0, 1.0);
            double eta = (1.0 + (levelFactor * 0.15)) * Math.Max(0.1, 1.1 - combinedEntropy);
            double ugpe = (UGPE_BASE_FRICTION * GRAVITY_CONSTANT * inputLength * levelFactor * eta)
                / (efficiency * bridgeRatio);

            bool triggerOverwrite = inputLength >= LAMBDA_BRIDGE_THRESHOLD || ugpe >= LAMBDA_BRIDGE_THRESHOLD;

            int optimizedLevel;
            if (combinedEntropy > active.SkipThresholdEntropy)
            {
                optimizedLevel = 1;
            }
            else if (triggerOverwrite)
            {
                optimizedLevel = Math.Min(active.Level, 5);
            }
            else
            {
                optimizedLevel = active.Level;
            }

            return new GeodesicTelemetry(ugpe, triggerOverwrite, optimizedLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalculateUGPEAndGeodesic(
            int inputLength,
            int requestedLevel,
            double spectralEntropy,
            out bool triggerDimensionalOverwrite,
            out int optimizedCompressionLevel)
        {
            CompressionPolicy policy = ActivePolicy with { Level = requestedLevel };
            GeodesicTelemetry telemetry = EvaluateGeodesicAndUGPE(inputLength, policy, spectralEntropy);
            triggerDimensionalOverwrite = telemetry.TriggerDimensionalOverwrite;
            optimizedCompressionLevel = telemetry.OptimizedCompressionLevel;
            return telemetry.Ugpe;
        }

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

            CompressionPolicy policy = ActivePolicy with { Level = compressionLevel };
            double nibbleEntropy = CalculateNibbleEntropyEstimate(input);
            double corrEntropy = CalculateByteCorrelationEntropy(input);
            GeodesicTelemetry telemetry = EvaluateGeodesicAndUGPE(input.Length, policy, nibbleEntropy, corrEntropy);

            int capacity = input.Length + (input.Length >> 6) + 1024;
            byte[]? rented = null;

            try
            {
                rented = ArrayPool<byte>.Shared.Rent(capacity);

                fixed (byte* pIn = input)
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res;

                    fixed (byte* pOut = rented)
                    {
                        res = NativeCompressChunk(
                            pIn,
                            (nuint)input.Length,
                            pOut,
                            (nuint)rented.Length,
                            out written,
                            telemetry.OptimizedCompressionLevel);
                    }

                    while (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        ArrayPool<byte>.Shared.Return(rented, clearArray: false);
                        rented = null;

                        if (capacity > int.MaxValue / 2)
                            throw new OutOfMemoryException("Compression output buffer exceeded maximum array size.");

                        capacity *= 2;
                        rented = ArrayPool<byte>.Shared.Rent(capacity);

                        fixed (byte* pRetry = rented)
                        {
                            res = NativeCompressChunk(
                                pIn,
                                (nuint)input.Length,
                                pRetry,
                                (nuint)rented.Length,
                                out written,
                                telemetry.OptimizedCompressionLevel);
                        }
                    }

                    if (res != SOVEREIGN_SUCCESS)
                        throw new ExternalException($"Compression failed with native error code: {res}");

                    nuint writtenCount = (nuint)written;
                    if (writtenCount > (nuint)rented.Length || writtenCount > int.MaxValue)
                        throw new InvalidOperationException("Native compressor reported invalid output length.");

                    int outLen = (int)writtenCount;
                    byte[] output = new byte[outLen];
                    Buffer.BlockCopy(rented, 0, output, 0, outLen);
                    return output;
                }
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented, clearArray: false);
                }
            }
        }

        public static unsafe byte[] Decompress(ReadOnlySpan<byte> compressedInput, int expectedUncompressedSize = 0)
        {
            if (compressedInput.IsEmpty) return Array.Empty<byte>();

            int capacity = expectedUncompressedSize > 0
                ? expectedUncompressedSize
                : Math.Max(compressedInput.Length * 4, 64 * 1024);

            byte[]? rented = null;

            try
            {
                rented = ArrayPool<byte>.Shared.Rent(capacity);

                fixed (byte* pIn = compressedInput)
                {
                    UIntPtr written = UIntPtr.Zero;
                    int res;

                    fixed (byte* pOut = rented)
                    {
                        res = NativeDecompressChunk(
                            pIn,
                            (nuint)compressedInput.Length,
                            pOut,
                            (nuint)rented.Length,
                            out written);
                    }

                    while (res == SOVEREIGN_ERR_BUFFER_TOO_SMALL)
                    {
                        ArrayPool<byte>.Shared.Return(rented, clearArray: false);
                        rented = null;

                        if (capacity > int.MaxValue / 2)
                            throw new OutOfMemoryException("Decompression output buffer exceeded maximum array size.");

                        capacity *= 2;
                        rented = ArrayPool<byte>.Shared.Rent(capacity);

                        fixed (byte* pRetry = rented)
                        {
                            res = NativeDecompressChunk(
                                pIn,
                                (nuint)compressedInput.Length,
                                pRetry,
                                (nuint)rented.Length,
                                out written);
                        }
                    }

                    if (res != SOVEREIGN_SUCCESS)
                        throw new ExternalException($"Decompression failed with native error code: {res}");

                    nuint writtenCount = (nuint)written;
                    if (writtenCount > (nuint)rented.Length || writtenCount > int.MaxValue)
                        throw new InvalidOperationException("Native decompressor reported invalid output length.");

                    int outLen = (int)writtenCount;
                    byte[] output = new byte[outLen];
                    Buffer.BlockCopy(rented, 0, output, 0, outLen);
                    return output;
                }
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented, clearArray: false);
                }
            }
        }

        #endregion

        #region --- Advanced Stealth & SIMD Masking ---

        public static byte[] CreateSessionMaskKey(ReadOnlySpan<byte> keyMaterial)
        {
            byte[] sessionKey = new byte[8];
            byte[] entropy = new byte[8];
            RandomNumberGenerator.Fill(entropy);

            for (int i = 0; i < 8; i++)
            {
                byte baseByte = ShinobiMaskKey[i];
                byte matByte = keyMaterial.Length > 0 ? keyMaterial[i % keyMaterial.Length] : (byte)0x5A;
                sessionKey[i] = (byte)(baseByte ^ matByte ^ entropy[i]);
            }

            return sessionKey;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyGhostingMask(Span<byte> buffer)
            => ApplyGhostingMask(buffer, ShinobiMaskKey);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ApplyGhostingMask(Span<byte> buffer, ReadOnlySpan<byte> key8)
        {
            if (buffer.IsEmpty) return;

            Span<byte> effectiveKey = stackalloc byte[8];
            for (int k = 0; k < 8; k++)
            {
                effectiveKey[k] = key8.Length > 0 ? key8[k % key8.Length] : ShinobiMaskKey[k];
            }

            ulong keyPattern;
            fixed (byte* pKey = effectiveKey)
            {
                keyPattern = *(ulong*)pKey;
            }

            fixed (byte* pBuffer = buffer)
            {
                int len = buffer.Length;
                int i = 0;

                if (Vector512.IsHardwareAccelerated && len >= 64)
                {
                    Vector512<ulong> mask512Ulong = Vector512.Create(keyPattern);
                    Vector512<byte> mask512 = mask512Ulong.AsByte();

                    for (; i <= len - 64; i += 64)
                    {
                        Vector512<byte> current = Vector512.Load(pBuffer + i);
                        Vector512<byte> xored = Vector512.BitwiseXor(current, mask512);
                        xored.Store(pBuffer + i);
                    }
                }

                if (Avx2.IsSupported && i <= len - 32)
                {
                    Vector256<byte> mask256 = Vector2
