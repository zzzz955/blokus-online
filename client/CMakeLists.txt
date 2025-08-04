cmake_minimum_required(VERSION 3.24)

# Qt 설정
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
endif()

# 클라이언트 소스 파일들
set(CLIENT_SOURCES
    main.cpp
    "src/GameBoard.cpp"
    "src/GameRoomWindow.cpp"
    "src/ImprovedBlockPalette.cpp"
    "src/LoginWindow.cpp"
    "src/LobbyWindow.cpp"
    "src/UserInfoDialog.cpp"
    "src/AfkNotificationDialog.cpp"
    "src/Block.cpp"
    "src/QtAdapter.cpp"
    "src/ClientUtils.cpp"
    "src/ResponsiveUI.cpp"
    "src/NetworkClient.cpp"
    "src/ClientConfigManager.cpp"
    "src/ClientVersion.cpp"
    "src/SoundManager.cpp"
)

set(CLIENT_HEADERS
    "include/GameBoard.h"
    "include/GameRoomWindow.h"
    "include/ImprovedBlockPalette.h"
    "include/LoginWindow.h"
    "include/LobbyWindow.h"
    "include/UserInfoDialog.h"
    "include/AfkNotificationDialog.h"
    "include/ClientTypes.h"
    "include/QtAdapter.h"
    "include/ClientBlock.h"
    "include/ClientLogic.h"
    "include/ResponsiveUI.h"
    "include/NetworkClient.h"
    "include/ClientConfigManager.h"
    "include/ClientVersion.h"
    "include/SoundManager.h"
)

# 실행 파일 생성
add_executable(BlokusClient 
    ${CLIENT_SOURCES} 
    ${CLIENT_HEADERS}
)

# 인클루드 디렉토리
target_include_directories(BlokusClient PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# 라이브러리 링크
target_link_libraries(BlokusClient PRIVATE
    BlokusCommon
    Qt5::Core
    Qt5::Widgets
    Qt5::Gui
    Qt5::Svg
    Qt5::Network
    Qt5::Multimedia
)

# Windows 설정
if(WIN32)
    # 빌드 타입 디버깅
    message(STATUS "Current CMAKE_BUILD_TYPE: '${CMAKE_BUILD_TYPE}'")
    message(STATUS "Debug flag check: '$<CONFIG:Debug>'")
    
    # 디버그 모드에서만 콘솔 표시
    if(CMAKE_BUILD_TYPE STREQUAL "Debug" OR 
       CMAKE_BUILD_TYPE STREQUAL "DEBUG" OR
       "${CMAKE_BUILD_TYPE}" MATCHES "Debug")
        
        message(STATUS "==> Debug build detected: Console window ENABLED")
        set_target_properties(BlokusClient PROPERTIES
            WIN32_EXECUTABLE FALSE  # 콘솔 애플리케이션으로 설정
        )
        
        # 추가 디버그 링커 플래그 및 매크로 정의
        if(MSVC)
            set_target_properties(BlokusClient PROPERTIES
                LINK_FLAGS "/SUBSYSTEM:CONSOLE"
            )
            # 디버그 매크로 정의
            target_compile_definitions(BlokusClient PRIVATE DEBUG _DEBUG)
        endif()
        
    else()
        message(STATUS "==> Release build detected: Console window DISABLED")
        set_target_properties(BlokusClient PROPERTIES
            WIN32_EXECUTABLE TRUE   # Windows 애플리케이션으로 설정
        )
    endif()
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusClient PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Client\\Headers" FILES ${CLIENT_HEADERS})
source_group("Client\\Sources" FILES ${CLIENT_SOURCES})

# ===========================================
# 서버 설정 (빌드 모드별 하드코딩)
# ===========================================

message(STATUS "Server configuration:")
message(STATUS "  Debug 모드: localhost:9999")
message(STATUS "  Release 모드: blokus-online.mooo.com:9999")
message(STATUS "  설정 방식: 빌드 모드별 하드코딩 (_DEBUG 매크로 사용)")