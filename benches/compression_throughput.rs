use criterion::{
    black_box, criterion_group, criterion_main, BenchmarkId, Criterion, Throughput,
};
use rayon::prelude::*;
use sovereign_compressor::sovereign_compress_chunk;
use std::slice;
use zstd::stream::encode_all;

/// Generates pseudo-random, moderately compressible data payload
fn generate_test_payload(size_bytes: usize) -> Vec<u8> {
    let mut data = Vec::with_capacity(size_bytes);
    let pattern = b"SOVEREIGN_ENGINE_ZERO_COPY_CHAKRA_TREE_STREAM_1234567890_NEURAL_VECTOR_";
    while data.len() < size_bytes {
        let remaining = size_bytes - data.len();
        let to_take = remaining.min(pattern.len());
        data.extend_from_slice(&pattern[..to_take]);
    }
    data
}

/// Single-threaded sequential Zstandard compression without Rayon parallelism
fn compress_single_threaded(input: &[u8], level: i32, output_buffer: &mut [u8]) -> i64 {
    match encode_all(input, level) {
        Ok(compressed_bytes) => {
            if compressed_bytes.len() > output_buffer.len() {
                return -2;
            }
            unsafe {
                std::ptr::copy_nonoverlapping(
                    compressed_bytes.as_ptr(),
                    output_buffer.as_mut_ptr(),
                    compressed_bytes.len(),
                );
            }
            compressed_bytes.len() as i64
        }
        Err(_) => -3,
    }
}

fn bench_compression_throughput(c: &mut Criterion) {
    let mut group = c.benchmark_group("Compression Throughput Comparison");
    let compression_level = 3;

    // Test sizes: 1 MB, 10 MB, and 50 MB payloads
    let payload_sizes = vec![
        1 * 1024 * 1024,  // 1 MB
        10 * 1024 * 1024, // 10 MB
        50 * 1024 * 1024, // 50 MB
    ];

    for size in payload_sizes {
        let input_data = generate_test_payload(size);
        let mut output_buffer = vec![0u8; size * 2]; // Oversized output buffer to prevent -2 overflow

        // Tell Criterion to calculate MB/s throughput based on input payload size
        group.throughput(Throughput::Bytes(size as u64));

        // 1. Single-Threaded Sequential Zstd Benchmark
        group.bench_with_input(
            BenchmarkId::new("Single-Threaded", format!("{}MB", size / (1024 * 1024))),
            &size,
            |b, _| {
                b.iter(|| {
                    compress_single_threaded(
                        black_box(&input_data),
                        compression_level,
                        black_box(&mut output_buffer),
                    )
                });
            },
        );

        // 2. Rayon Parallel Chunked C-FFI (sovereign_compress_chunk) Benchmark
        group.bench_with_input(
            BenchmarkId::new("Rayon Parallel C-FFI", format!("{}MB", size / (1024 * 1024))),
            &size,
            |b, _| {
                b.iter(|| {
                    sovereign_compress_chunk(
                        black_box(input_data.as_ptr()),
                        black_box(input_data.len()),
                        black_box(output_buffer.as_mut_ptr()),
                        black_box(output_buffer.len()),
                        compression_level,
                    )
                });
            },
        );
    }

    group.finish();
}

criterion_group!(benches, bench_compression_throughput);
criterion_main!(benches);
