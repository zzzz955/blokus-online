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
    "main.cpp"
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
    "src/SoundManager.cpp"
    "src/BGMManager.cpp"
    "resource.qrc"
    "app_icon.rc"
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
    "include/SoundManager.h"
    "include/BGMManager.h"
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

# SDL2 설정
find_package(SDL2 CONFIG REQUIRED)
find_package(SDL2_mixer_ext CONFIG REQUIRED)

# 라이브러리 링크
target_link_libraries(BlokusClient PRIVATE
    BlokusCommon
    Qt5::Core
    Qt5::Widgets
    Qt5::Gui
    Qt5::Svg
    Qt5::Network
    Qt5::Multimedia
    SDL2::SDL2
    SDL2_mixer_ext::SDL2_mixer_ext
)

# Windows 설정 - 표준 Qt 애플리케이션
if(WIN32)
    # 항상 WIN32 애플리케이션으로 설정 (Qt 표준)
    set_target_properties(BlokusClient PROPERTIES
        WIN32_EXECUTABLE TRUE
    )
    
    # Debug 매크로 정의
    target_compile_definitions(BlokusClient PRIVATE
        $<$<CONFIG:Debug>:DEBUG _DEBUG>
    )
    
    # Qt5::WinMain 링크 제거 - 직접 WinMain 구현
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusClient PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Client\\Headers" FILES ${CLIENT_HEADERS})
source_group("Client\\Sources" FILES ${CLIENT_SOURCES})

# 리소스 파일 복사
add_custom_command(TARGET BlokusClient POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory $<TARGET_FILE_DIR:BlokusClient>/resource
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_CURRENT_SOURCE_DIR}/resource/my_turn.wav
        $<TARGET_FILE_DIR:BlokusClient>/resource/my_turn.wav
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_CURRENT_SOURCE_DIR}/resource/time_out.wav
        $<TARGET_FILE_DIR:BlokusClient>/resource/time_out.wav
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_CURRENT_SOURCE_DIR}/resource/countdown.wav
        $<TARGET_FILE_DIR:BlokusClient>/resource/countdown.wav
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        ${CMAKE_CURRENT_SOURCE_DIR}/resource/login_title.svg
        $<TARGET_FILE_DIR:BlokusClient>/resource/login_title.svg
    COMMAND ${CMAKE_COMMAND} -E make_directory $<TARGET_FILE_DIR:BlokusClient>/resource/lobby
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_CURRENT_SOURCE_DIR}/resource/lobby
        $<TARGET_FILE_DIR:BlokusClient>/resource/lobby
    COMMAND ${CMAKE_COMMAND} -E make_directory $<TARGET_FILE_DIR:BlokusClient>/resource/gameroom
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_CURRENT_SOURCE_DIR}/resource/gameroom
        $<TARGET_FILE_DIR:BlokusClient>/resource/gameroom
    COMMENT "Copying sound, image, and BGM resource files to output directory"
)

# Visual C++ Runtime DLL 자동 복사
if(MSVC)
    # resource 폴더 위치 (CMakeLists.txt 위치 기준 상대경로)
    set(RESOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/resource")

    # 복사할 DLL 목록
    set(VC_DLLS
        vcruntime140.dll
        vcruntime140_1.dll
        msvcp140.dll
    )

    foreach(DLL_NAME IN LISTS VC_DLLS)
        set(DLL_PATH "${RESOURCE_DIR}/${DLL_NAME}")
        if(EXISTS "${DLL_PATH}")
            add_custom_command(TARGET BlokusClient POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                    "${DLL_PATH}"
                    "$<TARGET_FILE_DIR:BlokusClient>/${DLL_NAME}"
                COMMENT "Copying ${DLL_NAME} from resource directory to output directory"
            )
            message(STATUS "Will copy ${DLL_NAME} from resource directory")
        else()
            message(WARNING "DLL not found in resource directory: ${DLL_PATH}")
        endif()
    endforeach()
endif()

# ===========================================
# 서버 설정 (빌드 모드별 하드코딩)
# ===========================================

message(STATUS "Server configuration:")
message(STATUS "  Debug 모드: localhost:9999")
message(STATUS "  Release 모드: blokus-online.mooo.com:9999")
message(STATUS "  설정 방식: 빌드 모드별 하드코딩 (_DEBUG 매크로 사용)")