cmake_minimum_required(VERSION 3.20)

project(nordiska_benchmark LANGUAGES CXX)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(nordiska-benchmark
    src/main.cpp
    src/database.cpp
    src/benchmark.cpp
)

target_include_directories(nordiska-benchmark PRIVATE include)
target_compile_features(nordiska-benchmark PRIVATE cxx_std_23)
target_link_libraries(nordiska-benchmark PRIVATE pthread)
