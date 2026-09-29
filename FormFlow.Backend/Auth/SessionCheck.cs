namespace FormFlow.Backend.Auth
{
    /// <summary>
    /// Sign-in tokens are checked against the account on every request, so a token stops working when
    /// its account is deleted or its password changes, instead of lasting until it expires.
    /// </summary>
    public static class SessionCheck
    {
        public static bool IsCurrent(AdminUser? user, DateTime issuedAtUtc)
        {
            if (user is null)
            {
                return false;
            }

            // Token times are whole seconds, so compare in whole seconds: the token handed out by a
            // password change in the same second stays valid.
            return user.PasswordChangedAt is not { } changed
                || issuedAtUtc >= changed.AddTicks(-(changed.Ticks % TimeSpan.TicksPerSecond));
        }
    }
}
