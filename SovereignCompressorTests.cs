using System;
using System.Runtime.InteropServices;
using System.Text;
using SovereignEngine.Native;
using Xunit;

namespace SovereignSSD.Tests
{
    public class SovereignCompressorTests
    {
        [Fact]
        public void ZeroLengthPayload_ReturnsEmptyArray()
        {
            byte[] input = Array.Empty<byte>();
            byte[] compressed = SovereignCompressor.Compress(input);
            byte[] decompressed = SovereignCompressor.Decompress(compressed);

            Assert.Empty(compressed);
            Assert.Empty(decompressed);
        }

        [Theory]
        [InlineData(512)]
        [InlineData(64 * 1024)]
        [InlineData(143_999)]
        [InlineData(144_000)]
        [InlineData(144_001)]
        [InlineData(1024 * 1024)]
        public void Roundtrip_VaryingDataSizes_MatchesOriginalBytes(int size)
        {
            byte[] raw = new byte[size];
            new Random(42).NextBytes(raw);

            byte[] compressed = SovereignCompressor.Compress(raw, compressionLevel: 3);
            byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: raw.Length);

            Assert.Equal(raw.Length, decompressed.Length);
            Assert.Equal(raw, decompressed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(19)]
        public void Roundtrip_StructuredPayload_AcrossLevels_MatchesOriginalBytes(int level)
        {
            string pattern = "UESP_V12_STRUCTURED_SECTOR_PAYLOAD_0123456789_ABCDEF|";
            byte[] raw = Encoding.UTF8.GetBytes(string.Concat(System.Linq.Enumerable.Repeat(pattern, 2048)));

            byte[] compressed = SovereignCompressor.Compress(raw, compressionLevel: level);
            byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: raw.Length);

            Assert.NotEmpty(compressed);
            Assert.Equal(raw, decompressed);
        }

        [Fact]
        public void CorruptBuffer_ThrowsExternalException()
        {
            byte[] corrupt = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            Assert.Throws<ExternalException>(() => SovereignCompressor.Decompress(corrupt));
        }

        [Fact]
        public void NullInputBuffer_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SovereignCompressor.Compress(null!));
            Assert.Throws<ArgumentNullException>(() => SovereignCompressor.Decompress(null!));
        }
    }
}
