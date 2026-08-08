use rayon::prelude::*;
use std::slice;

pub const SOVEREIGN_SUCCESS: i32 = 0;
pub const SOVEREIGN_ERR_NULL_POINTER: i32 = -1;
pub const SOVEREIGN_ERR_BUFFER_TOO_SMALL: i32 = -2;
pub const SOVEREIGN_ERR_COMPRESSION_FAILED: i32 = -3;
pub const SOVEREIGN_ERR_DECOMPRESSION_FAILED: i32 = -4;

pub fn compress_chunk(data: &[u8], level: i32) -> Result<Vec<u8>, i32> {
    if data.is_empty() {
        return Ok(Vec::new());
    }

    if data.len() >= 1024 * 1024 {
        let chunk_size = 512 * 1024;
        let chunks: Vec<&[u8]> = data.chunks(chunk_size).collect();

        let compressed_chunks: Result<Vec<Vec<u8>>, i32> = chunks
            .into_par_iter()
            .map(|chunk| zstd::encode_all(chunk, level).map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED))
            .collect();

        let mut output = Vec::new();
        for chunk in compressed_chunks? {
            output.extend_from_slice(&chunk);
        }
        Ok(output)
    } else {
        zstd::encode_all(data, level).map_err(|_| SOVEREIGN_ERR_COMPRESSION_FAILED)
    }
}

pub fn decompress_chunk(data: &[u8]) -> Result<Vec<u8>, i32> {
    if data.is_empty() {
        return Ok(Vec::new());
    }
    zstd::decode_all(data).map_err(|_| SOVEREIGN_ERR_DECOMPRESSION_FAILED)
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

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match compress_chunk(input_slice, compression_level) {
        Ok(compressed_bytes) => {
            if compressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }
            slice::from_raw_parts_mut(out_ptr, compressed_bytes.len()).copy_from_slice(&compressed_bytes);
            *out_written = compressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(err_code) => err_code,
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

    match decompress_chunk(input_slice) {
        Ok(decompressed_bytes) => {
            if decompressed_bytes.len() > out_cap {
                return SOVEREIGN_ERR_BUFFER_TOO_SMALL;
            }
            slice::from_raw_parts_mut(out_ptr, decompressed_bytes.len()).copy_from_slice(&decompressed_bytes);
            *out_written = decompressed_bytes.len();
            SOVEREIGN_SUCCESS
        }
        Err(err_code) => err_code,
    }
}
