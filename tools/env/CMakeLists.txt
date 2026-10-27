cmake_minimum_required(VERSION 3.20)

project(nordiska_env LANGUAGES CXX)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(nordiska-env
    src/main.cpp
    src/env_file.cpp
    src/secret_generator.cpp
    src/menu.cpp
)

target_include_directories(nordiska-env PRIVATE include)
target_compile_features(nordiska-env PRIVATE cxx_std_23)
target_link_libraries(nordiska-env PRIVATE pthread)
