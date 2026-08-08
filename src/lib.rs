use std::slice;
use zstd::stream::encode_all;

/// Native C-compatible FFI function exported via sovereign_compressor.dll
#[no_mangle]
pub unsafe extern "C" fn sovereign_compress_chunk(
    input_ptr: *const u8,
    input_len: usize,
    output_ptr: *mut u8,
    max_output_len: usize,
    compression_level: i32,
) -> i64 {
    if input_ptr.is_null() || output_ptr.is_null() || input_len == 0 {
        return -1;
    }

    let input_slice = slice::from_raw_parts(input_ptr, input_len);

    match encode_all(input_slice, compression_level) {
        Ok(compressed_data) => {
            if compressed_data.len() > max_output_len {
                return -2; // Buffer overflow protection
            }

            let output_slice = slice::from_raw_parts_mut(output_ptr, compressed_data.len());
            output_slice.copy_from_slice(&compressed_data);

            compressed_data.len() as i64
        }
        Err(_) => -3, // Compression failure
    }
}
