namespace App.Security
{
    /// <summary>
    /// Unified token key management for consistent token storage/retrieval
    /// </summary>
    public static class TokenKeys
    {
        // Standard keys (OIDC-based naming for consistency)
        public const string Refresh = "oidc_refresh_token";
        public const string Access = "oidc_access_token";
        public const string Expiry = "oidc_token_expiry";

        // Legacy keys for migration (from previous versions)
        public static readonly string[] LegacyRefresh = { "blokus_refresh_token" };
        public static readonly string[] LegacyAccess = { "blokus_access_token", "access_token" };
    }
}