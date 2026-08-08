use rayon::prelude::*;
use std::io::Read;
use std::panic::catch_unwind;
use std::slice;
use zstd::stream::decoder::Decoder;
use zstd::stream::encode_all;

/// Standard chunk size for parallel processing (512 KB per chunk)
const PARALLEL_CHUNK_SIZE: usize = 512 * 1024;

/// Native C-FFI function to compress byte chunks using Zstandard + Rayon parallel execution.
/// Returns squeezed byte length (>0) on success, or a negative error code on failure.
/// Error Codes:
///  -1 = Invalid or Null Pointer
///  -2 = Output Buffer Overflow Prevention
///  -3 = Zstandard Compression Failure
///  -4 = Internal Panic Caught at Boundary
#[no_mangle]
pub extern "C" fn sovereign_compress_chunk(
    input_ptr: *const u8,
    input_len: usize,
    output_ptr: *mut u8,
    max_output_len: usize,
    compression_level: i32,
) -> i64 {
    // Prevent Rust panics from unwinding across the C-FFI boundary into C#
    let result = catch_unwind(|| {
        // Null pointer check
        if input_ptr.is_null() || output_ptr.is_null() {
            return -1; // Error: Invalid pointer
        }

        if input_len == 0 {
            return 0; // Success: Zero bytes compressed
        }

        let input_data = unsafe { slice::from_raw_parts(input_ptr, input_len) };

        // For small payloads under threshold, compress sequentially
        if input_len <= PARALLEL_CHUNK_SIZE {
            match encode_all(input_data, compression_level) {
                Ok(compressed_bytes) => {
                    if compressed_bytes.len() > max_output_len {
                        return -2; // Error: Buffer overflow prevention
                    }

                    unsafe {
                        std::ptr::copy_nonoverlapping(
                            compressed_bytes.as_ptr(),
                            output_ptr,
                            compressed_bytes.len(),
                        );
                    }

                    compressed_bytes.len() as i64
                }
                Err(_) => -3, // Error: Compression failure
            }
        } else {
            // High-throughput parallel chunked compression using Rayon
            let chunk_results: Result<Vec<Vec<u8>>, _> = input_data
                .par_chunks(PARALLEL_CHUNK_SIZE)
                .map(|chunk| encode_all(chunk, compression_level))
                .collect();

            match chunk_results {
                Ok(compressed_chunks) => {
                    let total_len: usize = compressed_chunks.iter().map(|c| c.len()).sum();

                    if total_len > max_output_len {
                        return -2; // Error: Buffer overflow prevention
                    }

                    let mut offset = 0;
                    for chunk in compressed_chunks {
                        unsafe {
                            std::ptr::copy_nonoverlapping(
                                chunk.as_ptr(),
                                output_ptr.add(offset),
                                chunk.len(),
                            );
                        }
                        offset += chunk.len();
                    }

                    total_len as i64
                }
                Err(_) => -3, // Error: Compression failure
            }
        }
    });

    match result {
        Ok(code) => code,
        Err(_) => -4, // Error: Panic caught at C-FFI boundary
    }
}

/// Native C-FFI function to decompress Zstandard streams back into uncompressed memory.
/// Returns decompressed byte length (>0) on success, or a negative error code on failure.
/// Error Codes:
///  -1 = Invalid or Null Pointer
///  -2 = Output Buffer Overflow Prevention
///  -3 = Zstandard Decompression Failure / Corrupted Payload
///  -4 = Internal Panic Caught at Boundary
#[no_mangle]
pub extern "C" fn sovereign_decompress_chunk(
    input_ptr: *const u8,
    input_len: usize,
    output_ptr: *mut u8,
    max_output_len: usize,
) -> i64 {
    let result = catch_unwind(|| {
        // Null pointer check
        if input_ptr.is_null() || output_ptr.is_null() {
            return -1; // Error: Invalid pointer
        }

        if input_len == 0 {
            return 0; // Success: Zero bytes decompressed
        }

        let compressed_data = unsafe { slice::from_raw_parts(input_ptr, input_len) };

        // Process single frame or multi-chunk streams seamlessly via Zstandard Decoder
        match Decoder::new(compressed_data) {
            Ok(mut decoder) => {
                let mut decompressed_buffer = Vec::new();
                if decoder.read_to_end(&mut decompressed_buffer).is_err() {
                    return -3; // Error: Decompression streaming failed
                }

                if decompressed_buffer.len() > max_output_len {
                    return -2; // Error: Output buffer overflow prevention
                }

                unsafe {
                    std::ptr::copy_nonoverlapping(
                        decompressed_buffer.as_ptr(),
                        output_ptr,
                        decompressed_buffer.len(),
                    );
                }

                decompressed_buffer.len() as i64
            }
            Err(_) => -3, // Error: Invalid Zstandard header or payload corrupted
        }
    });

    match result {
        Ok(code) => code,
        Err(_) => -4, // Error: Panic caught at C-FFI boundary
    }
}
