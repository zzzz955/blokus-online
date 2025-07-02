cmake_minimum_required(VERSION 3.24)

project(BlokusClient VERSION 1.0.0 LANGUAGES CXX)

# C++ 표준 설정
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Qt 설정
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# MSVC UTF-8 인코딩 강제 설정 (디버깅 문제 해결)
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

# ========================================
# Common 라이브러리 소스 파일들
# ========================================
set(COMMON_SOURCES
    ../common/src/Utils.cpp
    ../common/src/Block.cpp
    ../common/src/GameLogic.cpp
)

set(COMMON_HEADERS
    ../common/include/common/Types.h
    ../common/include/common/Utils.h
    ../common/include/common/Block.h
    ../common/include/common/GameLogic.h
)

# ========================================
# 클라이언트 소스 파일들
# ========================================
set(CLIENT_SOURCES
    src/main.cpp
    src/ui/GameBoard.cpp
    src/ui/GameRoomWindow.cpp
    src/ui/ImprovedBlockPalette.cpp
    src/ui/LoginWindow.cpp
    src/ui/LobbyWindow.cpp
    src/game/Block.cpp                     # Qt 래퍼
    src/common/QtAdapter.cpp               # Qt 어댑터
)

set(CLIENT_HEADERS
    include/ui/GameBoard.h
    include/ui/GameRoomWindow.h
    include/ui/ImprovedBlockPalette.h
    include/ui/LoginWindow.h
    include/ui/LobbyWindow.h
    include/common/Types.h                 # Qt 버전
    include/common/QtAdapter.h             # Qt 어댑터
    include/game/Block.h                   # Qt 래퍼
    include/game/GameLogic.h               # Qt 래퍼
)

# ========================================
# 실행 파일 생성
# ========================================
add_executable(BlokusClient 
    ${CLIENT_SOURCES} 
    ${CLIENT_HEADERS}
    ${COMMON_SOURCES}
    ${COMMON_HEADERS}
)

# ========================================
# 포함 디렉토리
# ========================================
target_include_directories(BlokusClient PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include     # 클라이언트 헤더 (우선순위)
    ${CMAKE_CURRENT_SOURCE_DIR}/../common/include  # Common 라이브러리 헤더
)

# ========================================
# 라이브러리 링크
# ========================================
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
    message(STATUS "디버그 모드: Common 라이브러리 통합 + Qt 래퍼 시스템")
endif()

# ========================================
# 소스 그룹 설정 (IDE에서 폴더 구조 정리)
# ========================================
source_group("Common\\Headers" FILES ${COMMON_HEADERS})
source_group("Common\\Sources" FILES ${COMMON_SOURCES})
source_group("Client\\Headers" FILES ${CLIENT_HEADERS})
source_group("Client\\Sources" FILES ${CLIENT_SOURCES})

# 빌드 정보 출력
message(STATUS "=== 블로커스 클라이언트 빌드 정보 ===")
message(STATUS "버전: ${PROJECT_VERSION}")
message(STATUS "Common 라이브러리: 통합됨")
message(STATUS "Qt 어댑터: 활성화")
message(STATUS "서버 호환성: 준비됨")
message(STATUS "로그인 시스템: 활성화")
message(STATUS "로비 시스템: 활성화")
message(STATUS "게임 룸 시스템: 활성화")
message(STATUS "AI 플레이어 지원: 활성화")
message(STATUS "블록 시스템: Common + Qt 래퍼")
message(STATUS "Common 소스 파일: ${COMMON_SOURCES}")
message(STATUS "클라이언트 소스 파일: ${CLIENT_SOURCES}")
message(STATUS "========================================")