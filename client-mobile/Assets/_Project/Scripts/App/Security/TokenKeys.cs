namespace App.Security
{
    /// <summary>
    /// 토큰 저장/조회를 위한 통합 키 관리
    /// </summary>
    public static class TokenKeys
    {
        // 표준 키 (OIDC 기반 명명 규칙)
        public const string Refresh = "oidc_refresh_token";        // RefreshToken (서버에서 만료 관리)
        public const string Access = "oidc_access_token";          // AccessToken
        public const string Expiry = "oidc_token_expiry";          // AccessToken 만료 시간 (RefreshToken 아님)

        // 레거시 키 (이전 버전에서 마이그레이션용)
        public static readonly string[] LegacyRefresh = { "blokus_refresh_token" };
        public static readonly string[] LegacyAccess = { "blokus_access_token", "access_token" };
    }
}