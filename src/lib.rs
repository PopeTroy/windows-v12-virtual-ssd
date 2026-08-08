use std::slice;
use rayon::prelude::*;

pub const SOVEREIGN_SUCCESS: i32 = 0;
pub const SOVEREIGN_ERR_NULL_POINTER: i32 = -1;
pub const SOVEREIGN_ERR_BUFFER_TOO_SMALL: i32 = -2;
pub const SOVEREIGN_ERR_COMPRESSION_FAILED: i32 = -3;
pub const SOVEREIGN_ERR_DECOMPRESSION_FAILED: i32 = -4;

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

    let input_slice = slice::from_raw_parts(input_ptr, input_len);
    
    // Zstd compression pipeline execution
    match zstd::encode_all(input_slice, compression_level) {
        Ok(compressed_bytes) => {
            if compressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }

            slice::from_raw_parts_mut(out_ptr, compressed_bytes.len())
                .copy_from_slice(&compressed_bytes);
            *out_written = compressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(_) => SOVEREIGN_ERR_COMPRESSION_FAILED,
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

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match zstd::decode_all(input_slice) {
        Ok(decompressed_bytes) => {
            if decompressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }

            slice::from_raw_parts_mut(out_ptr, decompressed_bytes.len())
                .copy_from_slice(&decompressed_bytes);
            *out_written = decompressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(_) => SOVEREIGN_ERR_DECOMPRESSION_FAILED,
    }
}
