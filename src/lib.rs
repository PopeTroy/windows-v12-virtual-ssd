use rayon::prelude::*;
use std::panic;
use std::ptr;
use std::slice;
use zstd::stream::{decode_all, encode_all};

pub const SOVEREIGN_SUCCESS: i32 = 0;
pub const SOVEREIGN_ERR_NULL_POINTER: i32 = -1;
pub const SOVEREIGN_ERR_BUFFER_TOO_SMALL: i32 = -2;
pub const SOVEREIGN_ERR_COMPRESSION_FAILED: i32 = -3;
pub const SOVEREIGN_ERR_DECOMPRESSION_FAILED: i32 = -4;
pub const SOVEREIGN_ERR_PANIC: i32 = -5;

pub fn compress_chunk(input: &[u8], compression_level: i32) -> Result<Vec<u8>, i32> {
    if input.is_empty() {
        return Ok(Vec::new());
    }
    if input.len() > 1_048_576 {
        let chunk_size = 524_288;
        let chunks: Vec<&[u8]> = input.chunks(chunk_size).collect();

        let processed_chunks: Result<Vec<Vec<u8>>, _> = chunks
            .par_iter()
            .map(|chunk| encode_all(*chunk, compression_level))
            .collect();

        match processed_chunks {
            Ok(vecs) => Ok(vecs.concat()),
            Err(_) => Err(SOVEREIGN_ERR_COMPRESSION_FAILED),
        }
    } else {
        encode_all(input, compression_level).map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED)
    }
}

pub fn decompress_chunk(input: &[u8]) -> Result<Vec<u8>, i32> {
    if input.is_empty() {
        return Ok(Vec::new());
    }
    decode_all(input).map_err(|_| SOVEREIGN_ERR_DECOMPRESSION_FAILED)
}

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
        match compress_chunk(input_slice, compression_level) {
            Ok(compressed) => {
                if compressed.len() > out_cap {
                    return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
                }
                ptr::copy_nonoverlapping(compressed.as_ptr(), out_ptr, compressed.len());
                *out_written = compressed.len();
                SOVEREIGN_SUCCESS
            }
            Err(err_code) => err_code,
        }
    });

    match panic_result {
        Ok(code) => code,
        Err(_) => SOVEREIGN_ERR_PANIC,
    }
}

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
        match decompress_chunk(input_slice) {
            Ok(decompressed) => {
                if decompressed.len() > out_cap {
                    return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
                }
                ptr::copy_nonoverlapping(decompressed.as_ptr(), out_ptr, decompressed.len());
                *out_written = decompressed.len();
                SOVEREIGN_SUCCESS
            }
            Err(err_code) => err_code,
        }
    });

    match panic_result {
        Ok(code) => code,
        Err(_) => SOVEREIGN_ERR_PANIC,
    }
}
