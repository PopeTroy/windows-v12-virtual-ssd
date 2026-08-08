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

        [Fact]
        public void CorruptBuffer_ThrowsExternalException()
        {
            byte[] corrupt = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 };
            Assert.Throws<ExternalException>(() => SovereignCompressor.Decompress(corrupt));
        }
    }
}
