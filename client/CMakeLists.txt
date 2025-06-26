cmake_minimum_required(VERSION 3.24)

project(BlokusClient VERSION 1.0.0 LANGUAGES CXX)

# C++ 표준 설정
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Qt 설정
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# MSVC UTF-8 인코딩 강제 설정 (디버그 문제 해결)
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
    # 한글 콘솔 출력을 위한 설정
    add_compile_definitions(UNICODE _UNICODE)
endif()

# vcpkg 패키지 찾기
find_package(Qt5 REQUIRED COMPONENTS Core Widgets Gui Svg Network)
find_package(spdlog CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)

# 소스 파일들 (실제 존재하는 파일만)
set(SOURCES
    src/main.cpp
    src/ui/GameBoard.cpp
    src/ui/MainWindow.cpp
)

set(HEADERS
    include/ui/GameBoard.h
    include/ui/MainWindow.h
    include/common/Types.h
)

# 실행 파일 생성
add_executable(BlokusClient ${SOURCES} ${HEADERS})

# 포함 디렉토리
target_include_directories(BlokusClient PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# 라이브러리 링크
target_link_libraries(BlokusClient
    Qt5::Core
    Qt5::Widgets
    Qt5::Gui
    Qt5::Svg
    Qt5::Network
    spdlog::spdlog
    nlohmann_json::nlohmann_json
)

# Windows 특정 설정
if(WIN32)
    set_target_properties(BlokusClient PROPERTIES
        WIN32_EXECUTABLE TRUE
    )
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusClient PRIVATE /W4 /utf-8)
else()
    target_compile_options(BlokusClient PRIVATE -Wall -Wextra -pedantic)
endif()

# Qt5 DLL 자동 복사 (Windows용)
if(WIN32)
    # Qt5 DLL들 복사
    add_custom_command(TARGET BlokusClient POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:Qt5::Core>" "$<TARGET_FILE_DIR:BlokusClient>"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different  
        "$<TARGET_FILE:Qt5::Gui>" "$<TARGET_FILE_DIR:BlokusClient>"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:Qt5::Widgets>" "$<TARGET_FILE_DIR:BlokusClient>"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:Qt5::Svg>" "$<TARGET_FILE_DIR:BlokusClient>"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:Qt5::Network>" "$<TARGET_FILE_DIR:BlokusClient>"
    )
    
    # vcpkg 의존성 DLL들도 복사
    add_custom_command(TARGET BlokusClient POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${CMAKE_PREFIX_PATH}/bin" "$<TARGET_FILE_DIR:BlokusClient>"
    )
endif()