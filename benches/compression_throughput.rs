use criterion::{
    black_box, criterion_group, criterion_main, BenchmarkId, Criterion, Throughput,
};
use sovereign_compressor::{compress_chunk, decompress_chunk};
use std::time::Duration;

/// Generates pseudo-random, non-trivial binary data to prevent unrealistic zero-byte compression ratios.
fn generate_sample_data(size_bytes: usize) -> Vec<u8> {
    let mut data = Vec::with_capacity(size_bytes);
    let mut state: u64 = 0xDEADBEEFCAFE;

    for i in 0..size_bytes {
        // Xorshift64 LCG algorithm to generate deterministic pseudo-random bytes
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;

        // Mix structured repetition (simulates compressible payload logs/JSON) with entropy
        let byte = if i % 4 == 0 {
            (i & 0xFF) as u8
        } else {
            (state & 0xFF) as u8
        };
        data.push(byte);
    }

    data
}

/// Benchmarks Zstd + Rayon parallel compression throughput across varying sizes and levels
fn bench_compression_throughput(c: &mut Criterion) {
    let mut group = c.benchmark_group("Sovereign_Compression_Throughput");

    // Configure Criterion statistical parameters for high precision
    group.warm_up_time(Duration::from_secs(2));
    group.measurement_time(Duration::from_secs(5));
    group.sample_size(30);

    // Test cases: 64 KB (single-thread path), 1 MB (boundary), 8 MB (Rayon parallel path)
    let payload_sizes = [
        ("64KB", 64 * 1024),
        ("1MB", 1_024 * 1024),
        ("8MB", 8 * 1024 * 1024),
    ];

    // Tested Zstandard Compression Levels: 1 (Fast), 3 (Default), 6 (High), 19 (Ultra)
    let levels = [1, 3, 6, 19];

    for (size_label, size_bytes) in payload_sizes {
        let raw_data = generate_sample_data(size_bytes);

        // Tell Criterion how many bytes are processed per iteration to measure MB/s throughput
        group.throughput(Throughput::Bytes(size_bytes as u64));

        for &level in &levels {
            let bench_id = BenchmarkId::new(format!("size_{}_level", size_label), level);

            group.bench_with_input(bench_id, &raw_data, |b, data| {
                b.iter(|| {
                    let compressed = compress_chunk(black_box(data), black_box(level))
                        .expect("Compression failed during benchmark");
                    black_box(compressed);
                });
            });
        }
    }

    group.finish();
}

/// Benchmarks Zstd decompression throughput across varying sizes
fn bench_decompression_throughput(c: &mut Criterion) {
    let mut group = c.benchmark_group("Sovereign_Decompression_Throughput");

    group.warm_up_time(Duration::from_secs(2));
    group.measurement_time(Duration::from_secs(5));
    group.sample_size(30);

    let payload_sizes = [
        ("64KB", 64 * 1024),
        ("1MB", 1_024 * 1024),
        ("8MB", 8 * 1024 * 1024),
    ];

    for (size_label, size_bytes) in payload_sizes {
        let raw_data = generate_sample_data(size_bytes);
        
        // Compress data once at Level 3 for the decompression source input
        let compressed_payload = compress_chunk(&raw_data, 3)
            .expect("Pre-compression for decompression benchmark failed");

        group.throughput(Throughput::Bytes(size_bytes as u64));

        let bench_id = BenchmarkId::new("decompress_size", size_label);

        group.bench_with_input(bench_id, &compressed_payload, |b, payload| {
            b.iter(|| {
                let decompressed = decompress_chunk(black_box(payload))
                    .expect("Decompression failed during benchmark");
                black_box(decompressed);
            });
        });
    }

    group.finish();
}

criterion_group!(
    benches,
    bench_compression_throughput,
    bench_decompression_throughput
);
criterion_main!(benches);
