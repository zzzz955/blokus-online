cmake_minimum_required(VERSION 3.24)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
endif()

# 서버 소스 파일들 (실제로 구현된 파일만)
set(SERVER_SOURCES
    main.cpp
    # src/core/GameServer.cpp
    # src/core/Session.cpp
    # src/core/EventSystem.cpp
    # src/game/GameRoom.cpp
    # src/handler/MessageHandler.cpp
    # src/manager/NetworkManager.cpp
    # src/manager/RoomManager.cpp
    # src/manager/DatabaseManager.cpp
    # src/manager/ConfigManager.cpp
    # src/service/AuthenticationService.cpp
    # src/util/ServerUtils.cpp
    # src/util/Logger.cpp
    # src/monitor/MetricsCollector.cpp
    # src/security/RateLimiter.cpp
)

set(SERVER_HEADERS
    include/common/ServerTypes.h
    include/core/GameServer.h
    include/core/Session.h
    include/core/EventSystem.h
    include/game/GameRoom.h
    include/handler/MessageHandler.h
    include/manager/NetworkManager.h
    include/manager/RoomManager.h
    include/manager/DatabaseManager.h
    include/manager/ConfigManager.h
    include/service/AuthenticationService.h
    include/util/Logger.h
)

# 실행 파일 생성
add_executable(BlokusServer 
    ${SERVER_SOURCES} 
    ${SERVER_HEADERS}
)

# 인클루드 디렉토리
target_include_directories(BlokusServer PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

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
    target_compile_options(BlokusServer PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Server\\Headers" FILES ${SERVER_HEADERS})
source_group("Server\\Sources" FILES ${SERVER_SOURCES})