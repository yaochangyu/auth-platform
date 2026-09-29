namespace MemberApi.Handlers;

public enum ResetPasswordOutcome
{
    Success,
    TokenNotFound,
    TokenExpiredOrUsed,
}
