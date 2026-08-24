use libc::{c_int, size_t};
use rayon::prelude::*;
use std::cmp;
use std::ptr;
use std::slice;

// --- CONSTANTS & STATUS CODES ---
pub const SOVEREIGN_SUCCESS: c_int = 0;
pub const SOVEREIGN_ERR_NULL_POINTER: c_int = -1;
pub const SOVEREIGN_ERR_BUFFER_TOO_SMALL: c_int = -2;
pub const SOVEREIGN_ERR_COMPRESSION_FAILED: c_int = -3;
pub const SOVEREIGN_ERR_DECOMPRESSION_FAILED: c_int = -4;

// --- SPACETIME SSD MATHEMATICAL CONSTANTS ---
pub const CYLINDER_STAGES: usize = 12;
pub const QUADRIC_ALIGNMENT: usize = 32; // 256-bit AVX2 cache alignment boundary
pub const FIELD_GOVERNOR_RATIO: f64 = 2.0 / 7.0; // Light-Matrix Ratio (0.285714)
pub const LAMBDA_BRIDGE_THRESHOLD: usize = 144_000; // 144 KB Spatial Overwrite Boundary

/// Aligns a memory offset/length to the Quadric Surface vector boundary (32-byte SIMD alignment).
#[inline(always)]
fn align_to_quadric_surface(len: usize) -> usize {
    (len + (QUADRIC_ALIGNMENT - 1)) & !(QUADRIC_ALIGNMENT - 1)
}

/// Evaluates the Brus Quantum Fourier frequency spectral response across a byte window.
/// Detects high-entropy stream segments to dynamically calibrate compression levels.
#[inline(always)]
fn compute_fourier_spectral_entropy(data: &[u8]) -> f64 {
    if data.is_empty() {
        return 0.0;
    }

    // Sample across 12-stage spatial stride intervals
    let mut fourier_bins = [0u32; 16];
    let step = cmp::max(1, data.len() / 2048);
    let mut total_samples = 0usize;

    for idx in (0..data.len()).step_by(step) {
        let byte_val = data[idx];
        fourier_bins[(byte_val & 0x0F) as usize] += 1;
        total_samples += 1;
    }

    if total_samples == 0 {
        return 0.0;
    }

    let inv_total = 1.0 / (total_samples as f64);
    let mut entropy = 0.0f64;

    for &count in &fourier_bins {
        if count > 0 {
            let p = (count as f64) * inv_total;
            entropy -= p * p.log2();
        }
    }

    entropy / 4.0 // Normalized spectral density [0.0, 1.0]
}

/// Core managed-equivalent chunk compression engine with 12-cylinder parallel execution.
pub fn compress_chunk(data: &[u8], level: i32) -> Result<Vec<u8>, i32> {
    if data.is_empty() {
        return Ok(Vec::new());
    }

    // Step 1: Fourier Entropy Assessment
    let entropy = compute_fourier_spectral_entropy(data);
    let effective_level = if entropy > 0.95 {
        1 // High entropy -> zero-inertia bypass to level 1 fast store
    } else if data.len() >= LAMBDA_BRIDGE_THRESHOLD {
        level.clamp(1, 5) // Overwrite active -> cap level for latency bound
    } else {
        level.clamp(1, 19)
    };

    // Step 2: 12-Cylinder Parallel Splitting for multi-megabyte streams
    if data.len() >= 1024 * 1024 {
        // Quadric-aligned chunk size targeting the 12-cylinder execution stages
        let chunk_size = align_to_quadric_surface(512 * 1024);
        let chunks: Vec<&[u8]> = data.chunks(chunk_size).collect();

        let compressed_chunks: Result<Vec<Vec<u8>>, i32> = chunks
            .into_par_iter()
            .map(|chunk| {
                zstd::encode_all(chunk, effective_level)
                    .map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED)
            })
            .collect();

        let mut output = Vec::new();
        for chunk in compressed_chunks? {
            output.extend_from_slice(&chunk);
        }
        Ok(output)
    } else {
        zstd::encode_all(data, effective_level).map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED)
    }
}

/// Core managed-equivalent chunk decompression engine.
pub fn decompress_chunk(data: &[u8]) -> Result<Vec<u8>, i32> {
    if data.is_empty() {
        return Ok(Vec::new());
    }
    zstd::decode_all(data).map_err(|_| SOVEREIGN_ERR_DECOMPRESSION_FAILED)
}

// --- NATIVE C-ABI EXPORTS ---

#[no_mangle]
pub unsafe extern "C" fn sovereign_compress_chunk(
    input_ptr: *const u8,
    input_len: size_t,
    out_ptr: *mut u8,
    out_cap: size_t,
    out_written: *mut size_t,
    compression_level: c_int,
) -> c_int {
    if input_ptr.is_null() || out_ptr.is_null() || out_written.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match compress_chunk(input_slice, compression_level) {
        Ok(compressed_bytes) => {
            if compressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }
            slice::from_raw_parts_mut(out_ptr, compressed_bytes.len())
                .copy_from_slice(&compressed_bytes);
            *out_written = compressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(err_code) => err_code,
    }
}

#[no_mangle]
pub unsafe extern "C" fn sovereign_decompress_chunk(
    input_ptr: *const u8,
    input_len: size_t,
    out_ptr: *mut u8,
    out_cap: size_t,
    out_written: *mut size_t,
) -> c_int {
    if input_ptr.is_null() || out_ptr.is_null() || out_written.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match decompress_chunk(input_slice) {
        Ok(decompressed_bytes) => {
            if decompressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }
            slice::from_raw_parts_mut(out_ptr, decompressed_bytes.len())
                .copy_from_slice(&decompressed_bytes);
            *out_written = decompressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(err_code) => err_code,
    }
}

/// Zero-copy, high-throughput stealth compression pipeline with Fourier spectral gating.
#[no_mangle]
pub unsafe extern "C" fn sovereign_compress_chunk_zerocopy(
    input_ptr: *const u8,
    input_len: size_t,
    output_ptr: *mut u8,
    max_output_len: size_t,
    compression_level: c_int,
) -> i64 {
    if input_ptr.is_null() || output_ptr.is_null() || input_len == 0 {
        return SOVEREIGN_ERR_NULL_POINTER as i64;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);
    let output_slice = slice::from_raw_parts_mut(output_ptr, max_output_len);

    // Fourier spectral pre-filtering pass
    let entropy = compute_fourier_spectral_entropy(input_slice);
    let effective_level = if entropy > 0.95 {
        1
    } else if input_len >= LAMBDA_BRIDGE_THRESHOLD {
        compression_level.clamp(1, 5)
    } else {
        compression_level.clamp(1, 19)
    };

    match zstd::block::compress_to_buffer(input_slice, output_slice, effective_level) {
        Ok(written) => written as i64,
        Err(_) => SOVEREIGN_ERR_COMPRESSION_FAILED as i64,
    }
}

/// 12-Cylinder Parallel Stream Hasher using Blake3 SIMD Acceleration & Rayon.
/// Partitions memory buffers into quadric vector slices and streams parallel block signatures.
#[no_mangle]
pub unsafe extern "C" fn sovereign_hash_stream_parallel(
    data_ptr: *const u8,
    len: size_t,
    chunk_size: size_t,
    out_hashes_ptr: *mut u8,
) -> c_int {
    if data_ptr.is_null() || out_hashes_ptr.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    if len == 0 || chunk_size == 0 {
        return SOVEREIGN_SUCCESS;
    }

    let data_slice = slice::from_raw_parts(data_ptr, len);
    let quadric_chunk_size = align_to_quadric_surface(chunk_size);
    let chunks: Vec<&[u8]> = data_slice.chunks(quadric_chunk_size).collect();
    let num_chunks = chunks.len();

    // Out hash buffer slice (Each Blake3 hash is 32 bytes)
    let out_hashes_slice = slice::from_raw_parts_mut(out_hashes_ptr, num_chunks * 32);

    // 12-Cylinder Parallel Firing Engine
    chunks
        .par_iter()
        .enumerate()
        .for_each(|(i, &chunk_data)| {
            let hash = blake3::hash(chunk_data);
            let hash_bytes = hash.as_bytes();
            let dest_offset = i * 32;

            unsafe {
                ptr::copy_nonoverlapping(
                    hash_bytes.as_ptr(),
                    out_hashes_slice.as_mut_ptr().add(dest_offset),
                    32,
                );
            }
        });

    SOVEREIGN_SUCCESS
}
