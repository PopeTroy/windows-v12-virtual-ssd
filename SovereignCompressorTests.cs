using System;
using System.Security.Cryptography;
using System.Text;
using SovereignEngine.Native;
using Xunit;

namespace SovereignEngine.Tests
{
    public class SovereignCompressorTests
    {
        #region Helper Methods

        /// <summary>
        /// Generates deterministic pseudo-random binary payload data for reproducible testing.
        /// </summary>
        private static byte[] GenerateTestData(int sizeBytes)
        {
            byte[] data = new byte[sizeBytes];
            // Uses fixed seed to ensure determinism across test runs
            Random random = new Random(42);
            random.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Generates repetitive text payload data (highly compressible).
        /// </summary>
        private static byte[] GenerateStructuredTextData(int repeatCount)
        {
            string logLine = "[LOG-2026-08-08 15:30:00] [INFO] Sovereign Engine System Event ID: 94827104 - Processing data chunk stream.\n";
            StringBuilder sb = new StringBuilder(logLine.Length * repeatCount);
            for (int i = 0; i < repeatCount; i++)
            {
                sb.Append(logLine);
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion

        #region Roundtrip Tests

        [Fact]
        public void CompressAndDecompress_EmptyPayload_ReturnsEmptyArray()
        {
            // Arrange
            byte[] input = Array.Empty<byte>();

            // Act
            byte[] compressed = SovereignCompressor.Compress(input);
            byte[] decompressed = SovereignCompressor.Decompress(compressed);

            // Assert
            Assert.Empty(compressed);
            Assert.Empty(decompressed);
        }

        [Fact]
        public void CompressAndDecompress_SmallTextPayload_RestoresOriginalData()
        {
            // Arrange
            byte[] original = Encoding.UTF8.GetBytes("Sovereign Engine high-throughput native compression test payload.");

            // Act
            byte[] compressed = SovereignCompressor.Compress(original);
            byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: original.Length);

            // Assert
            Assert.NotNull(compressed);
            Assert.NotEmpty(compressed);
            Assert.Equal(original, decompressed);
        }

        [Theory]
        [InlineData(64)]                 // 64 Bytes
        [InlineData(4 * 1024)]           // 4 KB
        [InlineData(64 * 1024)]          // 64 KB
        [InlineData(1024 * 1024)]        // 1 MB (Chunk boundary)
        [InlineData(8 * 1024 * 1024)]    // 8 MB (Triggers Rayon parallel path)
        public void CompressAndDecompress_VaryingPayloadSizes_MatchesOriginalData(int payloadSizeBytes)
        {
            // Arrange
            byte[] originalData = GenerateTestData(payloadSizeBytes);

            // Act
            byte[] compressed = SovereignCompressor.Compress(originalData);
            byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: originalData.Length);

            // Assert
            Assert.NotNull(compressed);
            Assert.True(compressed.Length > 0, "Compressed buffer should contain encoded bytes.");
            Assert.Equal(originalData.Length, decompressed.Length);
            Assert.Equal(originalData, decompressed);
        }

        [Theory]
        [InlineData(1)]  // Fast
        [InlineData(3)]  // Default
        [InlineData(6)]  // High
        [InlineData(19)] // Ultra
        public void CompressAndDecompress_VaryingCompressionLevels_RestoresOriginalData(int level)
        {
            // Arrange
            byte[] originalData = GenerateStructuredTextData(500); // ~50 KB repetitive log payload

            // Act
            byte[] compressed = SovereignCompressor.Compress(originalData, compressionLevel: level);
            byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: originalData.Length);

            // Assert
            Assert.True(compressed.Length < originalData.Length, "Structured text should achieve noticeable compression.");
            Assert.Equal(originalData, decompressed);
        }

        #endregion

        #region Verification & Error Handling Tests

        [Fact]
        public void Compress_StructuredData_AchievesCompressionRatio()
        {
            // Arrange
            byte[] original = GenerateStructuredTextData(2000); // ~200 KB text

            // Act
            byte[] compressed = SovereignCompressor.Compress(original, compressionLevel: 3);

            // Assert
            double ratio = (double)compressed.Length / original.Length;
            Assert.True(ratio < 0.20, $"Expected compression ratio under 20%, but got {ratio * 100:F2}%.");
        }

        [Fact]
        public void Decompress_CorruptedData_ThrowsExternalException()
        {
            // Arrange
            byte[] invalidCompressedData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };

            // Act & Assert
            Assert.Throws<System.Runtime.InteropServices.ExternalException>(() =>
            {
                SovereignCompressor.Decompress(invalidCompressedData);
            });
        }

        [Fact]
        public void Decompress_WithoutExpectedSizeHint_ExpandsBufferDynamicallyAndSucceeds()
        {
            // Arrange
            byte[] originalData = GenerateTestData(2 * 1024 * 1024); // 2 MB
            byte[] compressed = SovereignCompressor.Compress(originalData);

            // Act (Pass 0 to force ArrayPool dynamic resizing logic inside SovereignCompressor)
            byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: 0);

            // Assert
            Assert.Equal(originalData, decompressed);
        }

        #endregion
    }
}
