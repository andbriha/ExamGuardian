cmake_minimum_required(VERSION 3.16)

project(Client VERSION 0.1 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(Qt6 REQUIRED COMPONENTS Quick Concurrent Multimedia Network)

qt_standard_project_setup(REQUIRES 6.8)

qt_add_executable(appClient
    src/main.cpp
)

qt_add_qml_module(appClient
    URI Client
    VERSION 0.1
    QML_FILES
        qml/Main.qml
        qml/screens/LoginScreen.qml
        qml/screens/AgreementScreen.qml
        qml/screens/ProcessCheckScreen.qml
        qml/screens/MediaCheckScreen.qml
        qml/screens/ExamScreen.qml
    SOURCES
        src/core/media_validator.h
        src/core/media_validator.cpp
        src/system/input_blocker.h
        src/system/input_blocker.cpp
        src/system/process_scanner.h
        src/system/process_scanner.cpp
        src/system/environment_detector.h
        src/system/environment_detector.cpp
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
    target_link_libraries(appClient PRIVATE user32 psapi version)
endif()

include(GNUInstallDirs)
install(TARGETS appClient
    BUNDLE DESTINATION .
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)
