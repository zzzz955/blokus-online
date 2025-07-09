#pragma once
// TimestampUtils.h - 재사용 가능한 타임스탬프 유틸리티

#include <chrono>
#include <string>
#include <ctime>
#include <iomanip>
#include <sstream>

namespace Blokus {
    namespace Utils {

        /**
         * 🔥 타임스탬프 변환 유틸리티 클래스
         * - PostgreSQL과 C++ 간의 타임스탬프 변환을 담당
         * - 다양한 형식 지원으로 재사용성 극대화
         * - 기존 코드 호환성 유지
         */
        class TimestampConverter {
        public:
            using TimePoint = std::chrono::system_clock::time_point;
            using Duration = std::chrono::system_clock::duration;

            /**
             * PostgreSQL의 EXTRACT(EPOCH) 결과를 time_point로 변환
             * @param epoch_seconds Unix timestamp (초)
             * @return std::chrono::system_clock::time_point
             */
            static TimePoint fromEpochSeconds(int64_t epoch_seconds) {
                return std::chrono::system_clock::from_time_t(static_cast<time_t>(epoch_seconds));
            }

            /**
             * PostgreSQL의 EXTRACT(EPOCH) 결과를 time_point로 변환 (밀리초 포함)
             * @param epoch_milliseconds Unix timestamp (밀리초)
             * @return std::chrono::system_clock::time_point
             */
            static TimePoint fromEpochMilliseconds(int64_t epoch_milliseconds) {
                auto duration = std::chrono::milliseconds(epoch_milliseconds);
                return TimePoint(duration);
            }

            /**
             * time_point를 Unix timestamp (초)로 변환
             * @param tp time_point
             * @return Unix timestamp (초)
             */
            static int64_t toEpochSeconds(const TimePoint& tp) {
                auto duration = tp.time_since_epoch();
                return std::chrono::duration_cast<std::chrono::seconds>(duration).count();
            }

            /**
             * time_point를 Unix timestamp (밀리초)로 변환
             * @param tp time_point
             * @return Unix timestamp (밀리초)
             */
            static int64_t toEpochMilliseconds(const TimePoint& tp) {
                auto duration = tp.time_since_epoch();
                return std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();
            }

            /**
             * 🔥 PostgreSQL timestamp with timezone 문자열 파싱 (백업용)
             * 예: "2025-07-09 16:39:52.249161+09"
             *
             * 주의: 이 함수는 복잡하므로 가능한 EXTRACT(EPOCH) 사용 권장
             */
            static TimePoint parsePostgreSQLTimestamp(const std::string& timestamp_str) {
                try {
                    // 기본적인 파싱 시도
                    std::tm tm = {};
                    std::istringstream ss(timestamp_str);

                    // ISO 형식으로 변환 시도
                    std::string iso_str = timestamp_str;

                    // 공백을 T로 변경
                    size_t space_pos = iso_str.find(' ');
                    if (space_pos != std::string::npos) {
                        iso_str[space_pos] = 'T';
                    }

                    // 마이크로초와 타임존 정보 제거 (간단한 파싱을 위해)
                    size_t dot_pos = iso_str.find('.');
                    if (dot_pos != std::string::npos) {
                        size_t plus_pos = iso_str.find('+', dot_pos);
                        size_t minus_pos = iso_str.find('-', dot_pos);
                        size_t tz_pos = (plus_pos != std::string::npos) ? plus_pos : minus_pos;
                        if (tz_pos != std::string::npos) {
                            iso_str = iso_str.substr(0, dot_pos) + iso_str.substr(tz_pos);
                        }
                    }

                    // 기본 형식으로 파싱 시도: YYYY-MM-DDTHH:MM:SS
                    ss.str(iso_str.substr(0, 19)); // 초까지만
                    ss >> std::get_time(&tm, "%Y-%m-%dT%H:%M:%S");

                    if (ss.fail()) {
                        // 파싱 실패 시 현재 시간 반환
                        return std::chrono::system_clock::now();
                    }

                    return std::chrono::system_clock::from_time_t(std::mktime(&tm));

                }
                catch (const std::exception&) {
                    // 예외 발생 시 현재 시간 반환
                    return std::chrono::system_clock::now();
                }
            }

            /**
             * time_point를 사람이 읽기 쉬운 문자열로 변환
             * @param tp time_point
             * @param format 포맷 문자열 (기본: "%Y-%m-%d %H:%M:%S")
             * @return 포맷된 문자열
             */
            static std::string toString(const TimePoint& tp, const std::string& format = "%Y-%m-%d %H:%M:%S") {
                auto time_t_val = std::chrono::system_clock::to_time_t(tp);
                std::stringstream ss;
                ss << std::put_time(std::localtime(&time_t_val), format.c_str());
                return ss.str();
            }

            /**
             * 현재 시간을 Unix timestamp (초)로 반환
             */
            static int64_t nowEpochSeconds() {
                return toEpochSeconds(std::chrono::system_clock::now());
            }

            /**
             * 현재 시간을 Unix timestamp (밀리초)로 반환
             */
            static int64_t nowEpochMilliseconds() {
                return toEpochMilliseconds(std::chrono::system_clock::now());
            }

            /**
             * 🔥 기존 코드 호환성을 위한 time_t 변환
             */
            static time_t toTimeT(const TimePoint& tp) {
                return std::chrono::system_clock::to_time_t(tp);
            }

            static TimePoint fromTimeT(time_t t) {
                return std::chrono::system_clock::from_time_t(t);
            }
        };

        /**
         * 🔥 PostgreSQL 쿼리 헬퍼 - 타임스탬프 관련
         */
        class PostgreSQLTimeQueries {
        public:
            /**
             * 타임스탬프를 EPOCH로 변환하는 SELECT 구문 생성
             * @param column_name 컬럼명
             * @param alias 별칭 (기본: column_name + "_epoch")
             * @return SQL 구문
             */
            static std::string epochSelect(const std::string& column_name,
                const std::string& alias = "") {
                std::string actual_alias = alias.empty() ? (column_name + "_epoch") : alias;
                return "EXTRACT(EPOCH FROM " + column_name + ")::bigint as " + actual_alias;
            }

            /**
             * 현재 시간을 EPOCH로 가져오는 구문
             */
            static std::string nowEpoch() {
                return "EXTRACT(EPOCH FROM NOW())::bigint";
            }

            /**
             * 사용자 테이블 조회용 표준 쿼리 (타임스탬프 포함)
             */
            static std::string getUserQuery(const std::string& condition_column) {
                return
                    "SELECT u.user_id, u.username, u.email, u.password_hash, "
                    + epochSelect("u.created_at", "created_at_epoch") + ", "
                    + epochSelect("COALESCE(u.last_login_at, u.created_at)", "last_login_epoch") + ", "
                    "s.total_games, s.wins, s.losses "
                    "FROM users u "
                    "LEFT JOIN user_stats s ON u.user_id = s.user_id "
                    "WHERE u." + condition_column + " = $1";
            }
        };

        /**
         * 🔥 시간 계산 유틸리티
         */
        class TimeCalculator {
        public:
            /**
             * 두 시점 간의 차이를 초로 계산
             */
            static int64_t diffSeconds(const TimestampConverter::TimePoint& start,
                const TimestampConverter::TimePoint& end) {
                auto diff = end - start;
                return std::chrono::duration_cast<std::chrono::seconds>(diff).count();
            }

            /**
             * 현재 시간부터 주어진 시점까지의 차이를 초로 계산
             */
            static int64_t ageSeconds(const TimestampConverter::TimePoint& tp) {
                return diffSeconds(tp, std::chrono::system_clock::now());
            }

            /**
             * 시간을 사람이 읽기 쉬운 형태로 변환
             * 예: "2 days ago", "3 hours ago", "just now"
             */
            static std::string humanReadableAge(const TimestampConverter::TimePoint& tp) {
                int64_t seconds = ageSeconds(tp);

                if (seconds < 60) return "just now";
                if (seconds < 3600) return std::to_string(seconds / 60) + " minutes ago";
                if (seconds < 86400) return std::to_string(seconds / 3600) + " hours ago";
                if (seconds < 2592000) return std::to_string(seconds / 86400) + " days ago";

                return std::to_string(seconds / 2592000) + " months ago";
            }
        };

    } // namespace Utils
} // namespace Blokus