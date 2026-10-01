cmake_minimum_required(VERSION 3.20)

project(nordiska_synthetic_generator LANGUAGES CXX)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_library(synthetic_generator_core STATIC
    src/generator.cpp
)
target_include_directories(synthetic_generator_core PUBLIC include)
target_compile_features(synthetic_generator_core PUBLIC cxx_std_23)

add_executable(nordiska-synthetic-gen
    src/main.cpp
)
target_include_directories(nordiska-synthetic-gen PRIVATE include)
target_link_libraries(nordiska-synthetic-gen PRIVATE synthetic_generator_core pthread)
target_compile_features(nordiska-synthetic-gen PRIVATE cxx_std_23)
