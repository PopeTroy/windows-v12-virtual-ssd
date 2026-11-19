cmake_minimum_required(VERSION 3.14)
project(RealTimePidOnnxControl CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(MSVC)
    add_compile_options(/O2 /W4 /permissive-)
else()
    add_compile_options(-O3 -Wall -Wextra -march=native)
endif()

set(SOURCES
    main.cpp
)

add_executable(pid_control_node ${SOURCES})

if(UNIX AND NOT APPLE)
    find_package(Threads REQUIRED)
    target_link_libraries(pid_control_node PRIVATE rt Threads::Threads)
    message(STATUS "[BUILD] Configured Linux POSIX Real-Time Shared Memory (librt)")
elseif(WIN32)
    message(STATUS "[BUILD] Configured Windows Win32 Shared Memory API")
endif()

target_include_directories(pid_control_node PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
