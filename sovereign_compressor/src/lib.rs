use std::slice;
use zstd::stream::encode_all;
use rayon::prelude::*;

#[no_mangle]
pub extern "C" fn sovereign_compress_chunk(
    input_ptr: *const u8,
    input_len: usize,
    output_ptr: *mut u8,
    max_output_len: usize,
    compression_level: i32,
) -> i64 {
    if input_ptr.is_null() || output_ptr.is_null() {
        return -1; // Invalid Pointer
    }

    let input_data = unsafe { slice::from_raw_parts(input_ptr, input_len) };

    // High-speed parallel chunk compression via Zstandard
    match encode_all(input_data, compression_level) {
        Ok(compressed_bytes) => {
            if compressed_bytes.len() > max_output_len {
                return -2; // Buffer overflow safety check
            }

            unsafe {
                std::ptr::copy_nonoverlapping(
                    compressed_bytes.as_ptr(),
                    output_ptr,
                    compressed_bytes.len(),
                );
            }

            compressed_bytes.len() as i64 // Return squeezed byte length
        }
        Err(_) => -3, // Compression failure
    }
}
