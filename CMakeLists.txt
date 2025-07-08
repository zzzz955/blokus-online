cmake_minimum_required(VERSION 3.24)
project(BlokusOnline VERSION 1.0.0 LANGUAGES CXX)

# C++ 표준 설정
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8 /W4 /MP)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

# 패키지 찾기
find_package(protobuf CONFIG REQUIRED)
find_package(Qt5 REQUIRED COMPONENTS Core Widgets Gui Svg Network)
find_package(spdlog CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(Boost REQUIRED COMPONENTS system)

# 디렉토리 추가
add_subdirectory(proto)
add_subdirectory(common)
add_subdirectory(client)
add_subdirectory(server)

# 빌드 정보 출력
message(STATUS "=== Blokus Online Build ===")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Protobuf found: ${protobuf_FOUND}")
message(STATUS "Qt5 found: ${Qt5_FOUND}")
message(STATUS "Boost found: ${Boost_FOUND}")
message(STATUS "=============================")