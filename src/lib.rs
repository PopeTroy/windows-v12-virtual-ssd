use std::slice;
use std::panic;
use std::ptr;
use zstd::stream::{encode_all, decode_all};
use rayon::prelude::*;

/// FFI Error Codes
pub const SOVEREIGN_SUCCESS: i32 = 0;
pub const SOVEREIGN_ERR_NULL_POINTER: i32 = -1;
pub const SOVEREIGN_ERR_BUFFER_TOO_SMALL: i32 = -2;
pub const SOVEREIGN_ERR_COMPRESSION_FAILED: i32 = -3;
pub const SOVEREIGN_ERR_DECOMPRESSION_FAILED: i32 = -4;
pub const SOVEREIGN_ERR_PANIC: i32 = -5;

/// Compresses a raw input byte array into the destination buffer using Zstandard and Rayon.
/// 
/// # Safety
/// - `input_ptr` must point to valid memory of at least `input_len` bytes.
/// - `out_ptr` must point to valid writable memory of at least `out_cap` bytes.
/// - `out_written` must be a valid non-null pointer to write the actual byte length output.
#[no_mangle]
pub unsafe extern "C" fn sovereign_compress_chunk(
    input_ptr: *const u8,
    input_len: usize,
    out_ptr: *mut u8,
    out_cap: usize,
    out_written: *mut usize,
    compression_level: i32,
) -> i32 {
    if input_ptr.is_null() || out_ptr.is_null() || out_written.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    if input_len == 0 {
        *out_written = 0;
        return SOVEREIGN_SUCCESS;
    }

    let panic_result = panic::catch_unwind(|| {
        let input_slice = slice::from_raw_parts(input_ptr, input_len);

        // Utilize Rayon parallel chunk processing for large payloads (>1MB)
        let compressed_bytes = if input_len > 1_048_576 {
            let chunk_size = 524_288; // 512 KB chunks
            let chunks: Vec<&[u8]> = input_slice.chunks(chunk_size).collect();

            let processed_chunks: Result<Vec<Vec<u8>>, _> = chunks
                .par_iter()
                .map(|chunk| encode_all(*chunk, compression_level))
                .collect();

            match processed_chunks {
                Ok(vecs) => vecs.concat(),
                Err(_) => return Err(SOVEREIGN_ERR_COMPRESSION_FAILED),
            }
        } else {
            match encode_all(input_slice, compression_level) {
                Ok(data) => data,
                Err(_) => return Err(SOVEREIGN_ERR_COMPRESSION_FAILED),
            }
        };

        if compressed_bytes.len() > out_cap {
            return Err(SOVEREIGN_ERR_BUFFER_TOO_SMALL);
        }

        ptr::copy_nonoverlapping(compressed_bytes.as_ptr(), out_ptr, compressed_bytes.len());
        *out_written = compressed_bytes.len();

        Ok(SOVEREIGN_SUCCESS)
    });

    match panic_result {
        Ok(res) => match res {
            Ok(code) => code,
            Err(err_code) => err_code,
        },
        Err(_) => SOVEREIGN_ERR_PANIC,
    }
}

/// Decompresses a Zstandard compressed payload back into uncompressed memory.
/// 
/// # Safety
/// - `input_ptr` must point to valid compressed memory of at least `input_len` bytes.
/// - `out_ptr` must point to valid writable memory of at least `out_cap` bytes.
/// - `out_written` must be a valid non-null pointer to receive the decompressed byte count.
#[no_mangle]
pub unsafe extern "C" fn sovereign_decompress_chunk(
    input_ptr: *const u8,
    input_len: usize,
    out_ptr: *mut u8,
    out_cap: usize,
    out_written: *mut usize,
) -> i32 {
    if input_ptr.is_null() || out_ptr.is_null() || out_written.is_null() {
        return SOVEREIGN_ERR_NULL_POINTER;
    }

    if input_len == 0 {
        *out_written = 0;
        return SOVEREIGN_SUCCESS;
    }

    let panic_result = panic::catch_unwind(|| {
        let input_slice = slice::from_raw_parts(input_ptr, input_len);

        let decompressed_bytes = match decode_all(input_slice) {
            Ok(data) => data,
            Err(_) => return Err(SOVEREIGN_ERR_DECOMPRESSION_FAILED),
        };

        if decompressed_bytes.len() > out_cap {
            return Err(SOVEREIGN_ERR_BUFFER_TOO_SMALL);
        }

        ptr::copy_nonoverlapping(decompressed_bytes.as_ptr(), out_ptr, decompressed_bytes.len());
        *out_written = decompressed_bytes.len();

        Ok(SOVEREIGN_SUCCESS)
    });

    match panic_result {
        Ok(res) => match res {
            Ok(code) => code,
            Err(err_code) => err_code,
        },
        Err(_) => SOVEREIGN_ERR_PANIC,
    }
}
