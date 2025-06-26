cmake_minimum_required(VERSION 3.24)

project(BlokusClient VERSION 1.0.0 LANGUAGES CXX)

# C++ 표준 설정
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Qt 설정
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# vcpkg 패키지 찾기
find_package(Qt5 REQUIRED COMPONENTS Core Widgets Gui Svg Network)
find_package(spdlog CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)

# 소스 파일들 (실제 존재하는 파일만)
set(SOURCES
    src/main.cpp
)

# 실행 파일 생성
add_executable(BlokusClient ${SOURCES})

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