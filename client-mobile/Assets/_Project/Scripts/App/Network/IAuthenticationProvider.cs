using System.Threading.Tasks;

namespace App.Network
{
    /// <summary>
    /// 인증 결과
    /// </summary>
    public class AuthResult
    {
        public bool Success { get; set; }
        public string AuthCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 인증 제공자 인터페이스
    /// Strategy Pattern을 사용하여 다양한 인증 방식(Google Play Games, Editor 테스트 등)을 지원합니다.
    /// </summary>
    public interface IAuthenticationProvider
    {
        /// <summary>
        /// 인증 수행
        /// </summary>
        /// <returns>인증 결과 (Success, AuthCode, ErrorMessage)</returns>
        Task<AuthResult> AuthenticateAsync();

        /// <summary>
        /// 제공자 이름 반환 (로깅용)
        /// </summary>
        string GetProviderName();

        /// <summary>
        /// 현재 플랫폼에서 사용 가능한지 확인
        /// </summary>
        bool IsAvailable();
    }
}