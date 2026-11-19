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
pub const FIELD_GOVERNOR_RATIO: f64 = 2.0 / 7.0; // Light-Matrix Ratio
pub const LAMBDA_BRIDGE_THRESHOLD: usize = 144_000; // 144 KB Spatial Overwrite Boundary[span_7](start_span)[span_7](end_span)

/// Aligns a memory offset/length to the Quadric Surface vector boundary (32-byte SIMD alignment).
#[inline(always)]
fn align_to_quadric_surface(len: usize) -> usize {
    if len == 0 {
        return QUADRIC_ALIGNMENT;
    }
    let aligned = len.saturating_add(QUADRIC_ALIGNMENT - 1) & !(QUADRIC_ALIGNMENT - 1);
    if aligned == 0 {
        len
    } else {
        aligned
    }
}

/// Evaluates normalized 4-bit nibble Shannon entropy [0.0, 1.0] across a sampled byte window[span_8](start_span)[span_8](end_span).
#[inline(always)]
pub fn compute_nibble_entropy_estimate(data: &[u8]) -> f64 {
    if data.is_empty() {
        return 0.0;
    }

    let mut nibble_bins = [0u32; 16];
    let step = cmp::max(1, data.len() / 2048);
    let mut total_samples = 0usize;

    for idx in (0..data.len()).step_by(step) {
        let byte_val = data[idx];
        nibble_bins[(byte_val & 0x0F) as usize] += 1;
        total_samples += 1;
    }

    if total_samples == 0 {
        return 0.0;
    }

    let inv_total = 1.0 / (total_samples as f64);
    let mut entropy = 0.0f64;

    for &count in &nibble_bins {
        if count > 0 {
            let p = (count as f64) * inv_total;
            entropy -= p * p.log2();
        }
    }

    (entropy / 4.0).clamp(0.0, 1.0)
}

#[deprecated(note = "Use compute_nibble_entropy_estimate instead")]
#[inline(always)]
fn compute_fourier_spectral_entropy(data: &[u8]) -> f64 {
    compute_nibble_entropy_estimate(data)
}

/// Estimates adjacent-byte change rate [0.0, 1.0] across sampled bytes[span_9](start_span)[span_9](end_span)[span_10](start_span)[span_10](end_span).
#[inline(always)]
pub fn compute_byte_correlation_entropy(data: &[u8]) -> f64 {
    if data.len() < 2 {
        return 0.0;
    }

    let step = cmp::max(1, (data.len() - 1) / 2048);
    let mut diff_count = 0usize;
    let mut total_pairs = 0usize;

    let mut idx = 0usize;
    while idx + 1 < data.len() {
        if data[idx] != data[idx + 1] {
            diff_count += 1;
        }
        total_pairs += 1;
        idx += step;
    }

    if total_pairs == 0 {
        return 0.0;
    }

    ((diff_count as f64) / (total_pairs as f64)).clamp(0.0, 1.0)
}

#[inline(always)]
fn select_effective_level(data: &[u8], requested_level: i32) -> i32 {
    let nibble_entropy = compute_nibble_entropy_estimate(data);
    let corr_entropy = compute_byte_correlation_entropy(data);
    let combined_entropy = (nibble_entropy * 0.7 + corr_entropy * 0.3).clamp(0.0, 1.0);

    if combined_entropy > 0.95 {
        1
    } else if data.len() >= LAMBDA_BRIDGE_THRESHOLD {
        requested_level.clamp(1, 5)
    } else {
        requested_level.clamp(1, 19)
    }
}

/// Core chunk compression engine with parallel block encoding for large buffers.
pub fn compress_chunk(data: &[u8], level: i32) -> Result<Vec<u8>, i32> {
    if data.is_empty() {
        return Ok(Vec::new());
    }

    let effective_level = select_effective_level(data, level);

    if data.len() >= 1024 * 1024 {
        let chunk_size = align_to_quadric_surface(512 * 1024);
        let chunks: Vec<&[u8]> = data.chunks(chunk_size).collect();

        let compressed_chunks: Result<Vec<Vec<u8>>, i32> = chunks
            .into_par_iter()
            .map(|chunk| {
                zstd::encode_all(chunk, effective_level)
                    .map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED)
            })
            .collect();

        let chunks_vec = compressed_chunks?;
        let total_len: usize = chunks_vec.iter().map(|c| c.len()).sum();
        let mut output = Vec::with_capacity(total_len);
        for chunk in chunks_vec {
            output.extend_from_slice(&chunk);
        }
        Ok(output)
    } else {
        zstd::encode_all(data, effective_level).map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED)
    }
}

/// Core chunk decompression engine.
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
    if out_written.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }
    *out_written = 0;

    if input_len == 0 {
        return SOVEREIGN_SUCCESS;
    }

    if input_ptr.is_null() || out_ptr.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match compress_chunk(input_slice, compression_level) {
        Ok(compressed_bytes) => {
            if compressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }
            if out_cap > 0 && !compressed_bytes.is_empty() {
                let dst = slice::from_raw_parts_mut(out_ptr, compressed_bytes.len());
                dst.copy_from_slice(&compressed_bytes);
            }
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
    if out_written.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }
    *out_written = 0;

    if input_len == 0 {
        return SOVEREIGN_SUCCESS;
    }

    if input_ptr.is_null() || out_ptr.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match decompress_chunk(input_slice) {
        Ok(decompressed_bytes) => {
            if decompressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }
            if out_cap > 0 && !decompressed_bytes.is_empty() {
                let dst = slice::from_raw_parts_mut(out_ptr, decompressed_bytes.len());
                dst.copy_from_slice(&decompressed_bytes);
            }
            *out_written = decompressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(err_code) => err_code,
    }
}

/// Zero-copy block compression entrypoint with bounds and capacity validation.
#[no_mangle]
pub unsafe extern "C" fn sovereign_compress_chunk_zerocopy(
    input_ptr: *const u8,
    input_len: size_t,
    output_ptr: *mut u8,
    max_output_len: size_t,
    compression_level: c_int,
) -> i64 {
    if input_len == 0 {
        return 0;
    }

    if input_ptr.is_null() || output_ptr.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER as i64;
    }

    if max_output_len == 0 {
        return SOVEREIGN_ERR_BUFFER_TOO_SMALL as i64;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);
    let output_slice = slice::from_raw_parts_mut(output_ptr, max_output_len);
    let effective_level = select_effective_level(input_slice, compression_level);

    match zstd::block::compress_to_buffer(input_slice, output_slice, effective_level) {
        Ok(written) => {
            if written > max_output_len {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL as i64;
            }
            i64::try_from(written).unwrap_or(SOVEREIGN_ERR_COMPRESSION_FAILED as i64)
        }
        Err(_) => SOVEREIGN_ERR_COMPRESSION_FAILED as i64,
    }
}

/// Parallel stream hasher using Blake3 and Rayon across chunked slices.
#[no_mangle]
pub unsafe extern "C" fn sovereign_hash_stream_parallel(
    data_ptr: *const u8,
    len: size_t,
    chunk_size: size_t,
    out_hashes_ptr: *mut u8,
) -> c_int {
    if len == 0 || chunk_size == 0 {
        return SOVEREIGN_SUCCESS;
    }

    if data_ptr.is_null() || out_hashes_ptr.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    let data_slice = slice::from_raw_parts(data_ptr, len);
    let quadric_chunk_size = align_to_quadric_surface(chunk_size);
    let chunks: Vec<&[u8]> = data_slice.chunks(quadric_chunk_size).collect();
    let num_chunks = chunks.len();

    let Some(total_hash_bytes) = num_chunks.checked_mul(32) else {
        return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
    };

    let out_hashes_slice = slice::from_raw_parts_mut(out_hashes_ptr, total_hash_bytes);

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
