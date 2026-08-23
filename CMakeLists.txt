cmake_minimum_required(VERSION 3.14)
project(RealTimePidOnnxControl CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Optimization Flags for Deterministic Execution
if(MSVC)
    add_compile_options(/O2 /W4)
else()
    add_compile_options(-O3 -Wall -Wextra -march=native -ffast-math)
endif()

# Source Files
set(SOURCES
    main.cpp
)

# Executable Target
add_executable(pid_control_node ${SOURCES})

# Platform Specific Linking
if(UNIX AND NOT APPLE)
    # Link POSIX Real-Time Extensions Library (librt) & Threads
    find_package(Threads REQUIRED)
    target_link_libraries(pid_control_node PRIVATE rt Threads::Threads)
    message(STATUS "[BUILD] Configured Linux POSIX Real-Time Shared Memory (librt)")
elseif(WIN32)
    message(STATUS "[BUILD] Configured Windows Win32 Shared Memory API")
endif()

# Header Include Directories
target_include_directories(pid_control_node PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
