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
    add_compile_options(/wd4828)  # C4828 경고 무시
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
    # 한글 콘솔 출력을 위한 설정
    add_compile_definitions(UNICODE _UNICODE)
endif()

# vcpkg 패키지 찾기
find_package(Qt5 REQUIRED COMPONENTS Core Widgets Gui Svg Network)
find_package(spdlog CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)

# 소스 파일들
set(SOURCES
    src/main.cpp
    src/ui/GameBoard.cpp
    src/ui/MainWindow.cpp
    src/ui/ImprovedBlockPalette.cpp    # 개선된 4방향 블록 팔레트
    src/ui/LoginWindow.cpp             # 🆕 로그인 화면
    src/ui/LobbyWindow.cpp             # 🆕 로비 화면
    src/game/Block.cpp                 # 블록 시스템
    src/game/GameLogic.cpp             # 게임 로직 및 규칙
)

# 헤더 파일들
set(HEADERS
    include/ui/GameBoard.h
    include/ui/MainWindow.h
    include/ui/ImprovedBlockPalette.h  # 개선된 블록 팔레트 헤더
    include/ui/LoginWindow.h           # 🆕 로그인 화면 헤더
    include/ui/LobbyWindow.h           # 🆕 로비 화면 헤더
    include/common/Types.h
    include/game/Block.h               # 블록 헤더
    include/game/GameLogic.h           # 게임 로직 헤더
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
    
    # 콘솔 창도 함께 표시 (디버깅용)
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set_target_properties(BlokusClient PROPERTIES
            LINK_FLAGS "/SUBSYSTEM:CONSOLE"
        )
    endif()
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusClient PRIVATE 
        /W4        # 경고 레벨 4
        /utf-8     # UTF-8 인코딩 강제
        /permissive-  # 표준 준수 모드
    )
else()
    target_compile_options(BlokusClient PRIVATE 
        -Wall -Wextra -pedantic
        -finput-charset=UTF-8 
        -fexec-charset=UTF-8
    )
endif()

# 디버그 정보 추가
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    target_compile_definitions(BlokusClient PRIVATE DEBUG _DEBUG)
    message(STATUS "디버그 모드: 로그인 시스템 + 블록 렌더링 시스템 포함")
endif()

# 빌드 정보 출력
message(STATUS "=== 블로커스 클라이언트 빌드 정보 ===")
message(STATUS "버전: ${PROJECT_VERSION}")
message(STATUS "로그인 시스템: 활성화")
message(STATUS "로비 시스템: 활성화")
message(STATUS "블록 시스템: 활성화")
message(STATUS "총 소스 파일: ${SOURCES}")
message(STATUS "========================================")