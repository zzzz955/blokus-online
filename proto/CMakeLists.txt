cmake_minimum_required(VERSION 3.24)

# Protobuf 찾기 (vcpkg에서 설치된 것)
find_package(Protobuf REQUIRED)

# 프로토 파일들 수집
file(GLOB PROTO_FILES "${CMAKE_CURRENT_SOURCE_DIR}/*.proto")

# C++ 코드 생성
protobuf_generate_cpp(PROTO_SRCS PROTO_HDRS ${PROTO_FILES})

# 라이브러리 생성
add_library(BlokusProto STATIC ${PROTO_SRCS} ${PROTO_HDRS})

# 🔥 중요: 인클루드 디렉토리 설정
target_include_directories(BlokusProto PUBLIC
    ${CMAKE_CURRENT_BINARY_DIR}  # 생성된 .pb.h 파일들
    ${Protobuf_INCLUDE_DIRS}     # protobuf 헤더들
)

# 링크
target_link_libraries(BlokusProto PUBLIC 
    ${Protobuf_LIBRARIES}
)

# C++ 표준
target_compile_features(BlokusProto PUBLIC cxx_std_20)

# 생성된 파일들 표시 (디버깅용)
message(STATUS "Generated proto sources: ${PROTO_SRCS}")
message(STATUS "Generated proto headers: ${PROTO_HDRS}")