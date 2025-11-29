namespace EgyptOnline.Utilities
{
    public enum TokensTypes
    {
        AccessToken,
        RefreshToken
    }
    static class TokenPeriod
    {
        public const int REFRESH_TOKEN_DAYS = 14; // 1 minute for testing
        public const int ACCESS_TOKEN_MINS = 30;  // 1 minute for testing
    }

}