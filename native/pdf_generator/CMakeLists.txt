cmake_minimum_required(VERSION 3.20)

project(nordiska_pdf_generator LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_library(nordiska_pdf_core STATIC
    src/application/create_pdf.cpp
    src/adapters/json_report_reader.cpp
    src/adapters/minimal_pdf_renderer.cpp
)

target_include_directories(nordiska_pdf_core PUBLIC include)
target_compile_features(nordiska_pdf_core PUBLIC cxx_std_23)

add_executable(pdf_generator src/main.cpp)
target_link_libraries(pdf_generator PRIVATE nordiska_pdf_core)
