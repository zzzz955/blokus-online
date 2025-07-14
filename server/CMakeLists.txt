cmake_minimum_required(VERSION 3.24)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
    add_compile_definitions(SPDLOG_WCHAR_TO_UTF8_SUPPORT)
endif()

# 서버 소스 파일들
set(SERVER_SOURCES
    main.cpp
    src/ConfigManager.cpp
    src/DatabaseManager.cpp
    src/GameServer.cpp
    src/Session.cpp
    src/GameRoom.cpp
    src/MessageHandler.cpp
    src/RoomManager.cpp
    src/AuthenticationService.cpp
    src/ServerTypes.cpp
)

# 핵심 헤더 파일들
set(SERVER_HEADERS
    include/ServerTypes.h
    include/GameServer.h
    include/Session.h
    include/MessageHandler.h
    include/NetworkManager.h
    include/GameRoom.h
    include/RoomManager.h
    include/DatabaseManager.h
    include/ConfigManager.h
    include/AuthenticationService.h
)

# 실행 파일 생성
add_executable(BlokusServer 
    ${SERVER_SOURCES} 
    ${SERVER_HEADERS}
)

# C++ 표준 설정 (호환성을 위해 17로 변경)
set_property(TARGET BlokusServer PROPERTY CXX_STANDARD 17)
set_property(TARGET BlokusServer PROPERTY CXX_STANDARD_REQUIRED ON)

# 인클루드 디렉토리
target_include_directories(BlokusServer PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# PostgreSQL 및 기타 라이브러리 찾기
find_package(libpqxx CONFIG REQUIRED)
find_package(OpenSSL REQUIRED)

# 라이브러리 링크
target_link_libraries(BlokusServer PRIVATE
    BlokusCommon
    BlokusProto
    spdlog::spdlog
    nlohmann_json::nlohmann_json
    Boost::system
    libpqxx::pqxx
    OpenSSL::SSL
    OpenSSL::Crypto
)

# Windows 설정
if(WIN32)
    set_target_properties(BlokusServer PROPERTIES
        WIN32_EXECUTABLE FALSE  # 콘솔 애플리케이션
    )
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusServer PRIVATE 
        /W4 
        /permissive-
        /Zc:preprocessor  # 전처리기 호환성
        /D_SILENCE_CXX17_ITERATOR_BASE_CLASS_DEPRECATION_WARNING
    )
    target_compile_definitions(BlokusServer PRIVATE
        NOMINMAX
        WIN32_LEAN_AND_MEAN
        _WIN32_WINNT=0x0601  # Windows 7+
    )
endif()

# 소스 그룹
source_group("Server\\Headers" FILES ${SERVER_HEADERS})
source_group("Server\\Sources" FILES ${SERVER_SOURCES})