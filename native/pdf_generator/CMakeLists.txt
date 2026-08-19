cmake_minimum_required(VERSION 3.20)

project(nordiska_pdf_generator LANGUAGES CXX)

include(CTest)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(unofficial-libharu CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(CAIRO REQUIRED IMPORTED_TARGET cairo)

add_library(nordiska_pdf_io STATIC
    src/io/byte_sink.cpp
)
target_include_directories(nordiska_pdf_io PUBLIC include)
target_compile_features(nordiska_pdf_io PUBLIC cxx_std_23)

add_library(nordiska_pdf_application STATIC
    src/application/batch_create_pdf.cpp
    src/application/create_pdf.cpp
    src/adapters/json_input_adapter.cpp
)
target_include_directories(nordiska_pdf_application PUBLIC include)
target_compile_features(nordiska_pdf_application PUBLIC cxx_std_23)
target_link_libraries(nordiska_pdf_application PRIVATE nordiska_pdf_io nlohmann_json::nlohmann_json)

add_library(nordiska_pdf_renderer_haru STATIC
    src/adapters/libharu_pdf_renderer.cpp
)
target_include_directories(nordiska_pdf_renderer_haru PUBLIC include)
target_compile_features(nordiska_pdf_renderer_haru PUBLIC cxx_std_23)
target_link_libraries(nordiska_pdf_renderer_haru PRIVATE unofficial::libharu::hpdf)

add_library(nordiska_pdf_renderer_cairo STATIC
    src/adapters/cairo_pdf_renderer.cpp
)
target_include_directories(nordiska_pdf_renderer_cairo PUBLIC include)
target_compile_features(nordiska_pdf_renderer_cairo PUBLIC cxx_std_23)
target_link_libraries(nordiska_pdf_renderer_cairo PRIVATE PkgConfig::CAIRO)

add_executable(pdf_generator_single src/main.cpp)
target_link_libraries(pdf_generator_single PRIVATE
    nordiska_pdf_application
    nordiska_pdf_renderer_haru
    nordiska_pdf_renderer_cairo
)

add_executable(pdf_generator_batch src/main_batch.cpp)
target_link_libraries(pdf_generator_batch PRIVATE
    nordiska_pdf_application
    nordiska_pdf_renderer_haru
    nordiska_pdf_renderer_cairo
)

add_library(nordiska_pdf_benchmark_metrics STATIC
    src/benchmark/benchmark_metrics.cpp
)
target_include_directories(nordiska_pdf_benchmark_metrics PUBLIC include)
target_compile_features(nordiska_pdf_benchmark_metrics PUBLIC cxx_std_23)

add_executable(pdf_generator_benchmark src/benchmark/main.cpp)
target_link_libraries(pdf_generator_benchmark PRIVATE
    nordiska_pdf_application
    nordiska_pdf_renderer_haru
    nordiska_pdf_benchmark_metrics
)

if(BUILD_TESTING)
    add_executable(nordiska_pdf_application_tests tests/application_tests.cpp)
    target_link_libraries(nordiska_pdf_application_tests PRIVATE nordiska_pdf_application)
    add_test(NAME nordiska_pdf_application_tests COMMAND nordiska_pdf_application_tests)

    add_executable(nordiska_json_input_adapter_tests tests/json_input_adapter_tests.cpp)
    target_link_libraries(nordiska_json_input_adapter_tests PRIVATE nordiska_pdf_application)
    add_test(NAME nordiska_json_input_adapter_tests COMMAND nordiska_json_input_adapter_tests)

    add_executable(nordiska_byte_sink_tests tests/byte_sink_tests.cpp)
    target_link_libraries(nordiska_byte_sink_tests PRIVATE nordiska_pdf_io)
    add_test(NAME nordiska_byte_sink_tests COMMAND nordiska_byte_sink_tests)

    add_executable(nordiska_benchmark_metrics_tests tests/benchmark_metrics_tests.cpp)
    target_link_libraries(nordiska_benchmark_metrics_tests PRIVATE nordiska_pdf_benchmark_metrics)
    add_test(NAME nordiska_benchmark_metrics_tests COMMAND nordiska_benchmark_metrics_tests)
endif()
