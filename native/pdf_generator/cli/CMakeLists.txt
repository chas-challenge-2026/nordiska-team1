add_executable(pdf_generator
    main.cpp
)

set_target_properties(pdf_generator PROPERTIES
    OUTPUT_NAME "pdf_generator"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
)

target_link_libraries(pdf_generator PRIVATE
    nordiska_document_core
    nordiska_document_input_json
)

# Custom target for convenient command-line or IDE execution/debugging
add_custom_target(run_cli
    COMMAND pdf_generator ${CMAKE_CURRENT_SOURCE_DIR}/../docs/golden_customer_batch_sample.json -o ${CMAKE_CURRENT_BINARY_DIR}/output -v
    DEPENDS pdf_generator
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/..
    COMMENT "Running pdf_generator CLI with sample customer batch in verbose mode"
)

install(TARGETS pdf_generator RUNTIME DESTINATION bin)

