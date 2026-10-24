cmake_minimum_required(VERSION 3.16)

project(Client VERSION 0.1 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(EXAMGUARDIAN_FLAVOR "development" CACHE STRING "Client environment: development or production")
set_property(CACHE EXAMGUARDIAN_FLAVOR PROPERTY STRINGS development production)
if(NOT EXAMGUARDIAN_FLAVOR MATCHES "^(development|production)$")
    message(FATAL_ERROR "EXAMGUARDIAN_FLAVOR must be development or production")
endif()

find_package(Qt6 REQUIRED COMPONENTS Quick Concurrent Multimedia Network)

qt_standard_project_setup(REQUIRES 6.8)

qt_add_executable(appClient
    src/main.cpp
)

set(client_main_qml "qml/Main.qml")
set(client_process_qml "qml/screens/ProcessCheckScreen.qml")
if(EXAMGUARDIAN_FLAVOR STREQUAL "production")
    # Produce resource variants with no development policy lookup or override branches.
    file(READ "${CMAKE_CURRENT_SOURCE_DIR}/qml/Main.qml" production_main)
    string(REGEX REPLACE "    function bypass[^\n]*\n" "" production_main "${production_main}")
    string(REPLACE " && !bypass(\"focusEnforcement\")" "" production_main "${production_main}")
    string(REPLACE " && !mainWindow.bypass(\"screenRequired\")" "" production_main "${production_main}")
    string(REPLACE "mainWindow.bypass(\"microphoneRequired\") ? null : examAudioInput" "examAudioInput" production_main "${production_main}")
    string(REPLACE "if (!bypass(\"screenRequired\")) screenRecorder.record(); else { screenStopped=true; screenVideoPath=\"\"; screenChunkIndex=0 }" "screenRecorder.record()" production_main "${production_main}")
    string(REPLACE "if (!bypass(\"cameraRequired\")) globalRecorder.record(); else { cameraStopped=true; currentVideoPath=\"\"; currentChunkIndex=0 }" "globalRecorder.record()" production_main "${production_main}")
    string(REPLACE "        if (!bypass(\"focusEnforcement\")) {\n            mainWindow.flags = Qt.Window | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint\n            mainWindow.showFullScreen()\n        }" "        mainWindow.flags = Qt.Window | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint\n        mainWindow.showFullScreen()" production_main "${production_main}")
    file(READ "${CMAKE_CURRENT_SOURCE_DIR}/qml/screens/ProcessCheckScreen.qml" production_process)
    string(REPLACE [=[text: (typeof DevVM !== "undefined" && DevVM.active.length) ? "DEV — blocking overridden. Review the detected findings above." : "Усі перевірки пройдено успішно! Ви можете перейти до перевірки камери."]=] [=[text: "Environment checks passed. Continue to camera verification."]=] production_process "${production_process}")
    if(production_main MATCHES "DevVM|bypass\\(" OR production_process MATCHES "DevVM")
        message(FATAL_ERROR "A development override remains in the production QML resource.")
    endif()
    file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/production-qml")
    set(client_main_qml "${CMAKE_CURRENT_BINARY_DIR}/production-qml/Main.qml")
    set(client_process_qml "${CMAKE_CURRENT_BINARY_DIR}/production-qml/ProcessCheckScreen.qml")
    file(WRITE "${client_main_qml}" "${production_main}")
    file(WRITE "${client_process_qml}" "${production_process}")
    set_source_files_properties("${client_main_qml}" PROPERTIES QT_RESOURCE_ALIAS "qml/Main.qml")
    set_source_files_properties("${client_process_qml}" PROPERTIES QT_RESOURCE_ALIAS "qml/screens/ProcessCheckScreen.qml")
    set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS qml/Main.qml qml/screens/ProcessCheckScreen.qml)
endif()

qt_add_qml_module(appClient
    URI Client
    VERSION 0.1
    QML_FILES
        ${client_main_qml}
        qml/screens/LoginScreen.qml
        qml/screens/ExamSelectionScreen.qml
        qml/screens/AgreementScreen.qml
        ${client_process_qml}
        qml/screens/MediaCheckScreen.qml
        qml/screens/ExamScreen.qml
    SOURCES
        src/core/audio_capture.h
        src/core/media_validator.h
        src/core/media_validator.cpp
        src/system/input_blocker.h
        src/system/input_blocker.cpp
        src/system/process_scanner.h
        src/system/process_scanner.cpp
        src/system/environment_detector.h
        src/system/environment_detector.cpp
        src/system/vm_windows.cpp
        src/system/vm_windows.h
        src/system/vm_policy.h
        src/network/auth_service.h
        src/network/auth_service.cpp
        src/network/ml_api_client.h
        src/network/ml_api_client.cpp
        src/network/core_api_client.h
        src/network/core_api_client.cpp
        src/viewmodels/process_viewmodel.h
        src/viewmodels/process_viewmodel.cpp
        src/viewmodels/media_viewmodel.h
        src/viewmodels/media_viewmodel.cpp
        src/viewmodels/exam_viewmodel.h
        src/viewmodels/exam_viewmodel.cpp
)

target_include_directories(appClient PRIVATE
    src
    src/core
    src/system
    src/network
    src/viewmodels
)

set_target_properties(appClient PROPERTIES
    MACOSX_BUNDLE_BUNDLE_VERSION ${PROJECT_VERSION}
    MACOSX_BUNDLE_SHORT_VERSION_STRING ${PROJECT_VERSION_MAJOR}.${PROJECT_VERSION_MINOR}
    MACOSX_BUNDLE TRUE
    WIN32_EXECUTABLE TRUE
)


target_link_libraries(appClient
    PRIVATE Qt6::Quick Qt6::Concurrent Qt6::Multimedia Qt6::Network
)

if(WIN32)
    target_link_libraries(appClient PRIVATE user32 psapi version setupapi advapi32)
endif()

include(GNUInstallDirs)
option(EXAMGUARDIAN_BUILD_TESTS "Build disk queue contract tests" OFF)
if(EXAMGUARDIAN_BUILD_TESTS)
    enable_testing()
    find_package(Qt6 REQUIRED COMPONENTS Test)
    qt_add_executable(upload_queue_test tests/upload_queue_test.cpp src/network/core_api_client.cpp src/network/core_api_client.h)
    target_include_directories(upload_queue_test PRIVATE src/network)
    target_link_libraries(upload_queue_test PRIVATE Qt6::Test Qt6::Network Qt6::Multimedia)
    add_test(NAME disk_queue_contract COMMAND upload_queue_test)
    qt_add_executable(vm_policy_test tests/vm_policy_test.cpp)
    target_include_directories(vm_policy_test PRIVATE src/system)
    target_link_libraries(vm_policy_test PRIVATE Qt6::Test)
    add_test(NAME vm_evidence_policy COMMAND vm_policy_test)
    qt_add_executable(verification_capture_test tests/verification_capture_test.cpp src/core/media_validator.cpp src/core/media_validator.h)
    target_include_directories(verification_capture_test PRIVATE src/core)
    target_link_libraries(verification_capture_test PRIVATE Qt6::Test Qt6::Multimedia)
    add_test(NAME automatic_verification_capture COMMAND verification_capture_test)
    qt_add_executable(catalog_preview tests/catalog_preview.cpp src/viewmodels/exam_viewmodel.cpp src/viewmodels/exam_viewmodel.h src/network/auth_service.cpp src/network/auth_service.h src/system/input_blocker.cpp src/system/input_blocker.h)
    target_include_directories(catalog_preview PRIVATE src/viewmodels src/network src/system)
    target_compile_definitions(catalog_preview PRIVATE CATALOG_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}/qml/screens")
    target_link_libraries(catalog_preview PRIVATE Qt6::Quick Qt6::Network)
    if(WIN32)
        target_link_libraries(catalog_preview PRIVATE user32)
    endif()
    qt_add_executable(native_core_contract tests/native_core_contract.cpp src/viewmodels/exam_viewmodel.cpp src/viewmodels/exam_viewmodel.h src/network/auth_service.cpp src/network/auth_service.h src/system/input_blocker.cpp src/system/input_blocker.h)
    target_include_directories(native_core_contract PRIVATE src/viewmodels src/network src/system)
    target_link_libraries(native_core_contract PRIVATE Qt6::Quick Qt6::Network)
    if(WIN32)
        target_link_libraries(native_core_contract PRIVATE user32)
    endif()
endif()
install(TARGETS appClient
    BUNDLE DESTINATION .
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)

if(EXAMGUARDIAN_FLAVOR STREQUAL "development")
    target_compile_definitions(appClient PRIVATE EXAMGUARDIAN_DEVELOPMENT)
    target_sources(appClient PRIVATE src/development/development_viewmodel.cpp src/development/development_viewmodel.h)
    target_include_directories(appClient PRIVATE src/development)
    qt_add_resources(appClient "development_qml" PREFIX "/development" FILES qml/development/DevelopmentWindow.qml)
    if(EXAMGUARDIAN_BUILD_TESTS)
        qt_add_executable(native_development_contract tests/native_development_contract.cpp src/development/development_viewmodel.cpp src/development/development_viewmodel.h src/viewmodels/exam_viewmodel.cpp src/viewmodels/exam_viewmodel.h src/network/auth_service.cpp src/network/auth_service.h src/system/input_blocker.cpp src/system/input_blocker.h)
        target_compile_definitions(native_development_contract PRIVATE EXAMGUARDIAN_DEVELOPMENT)
        target_include_directories(native_development_contract PRIVATE src/development src/viewmodels src/network src/system)
        target_link_libraries(native_development_contract PRIVATE Qt6::Quick Qt6::Network)
        add_test(NAME local_development_controls COMMAND native_development_contract)
        if(WIN32)
            target_link_libraries(native_development_contract PRIVATE user32)
        endif()
    endif()
endif()
