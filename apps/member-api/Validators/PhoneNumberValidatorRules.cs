namespace MemberApi.Validators;

public static class PhoneNumberValidatorRules
{
    public const string Pattern = @"^(09\d{8}|\+[1-9]\d{1,14})$";
    public const string ErrorMessage = "手機號碼格式無效，請輸入正確的台灣手機號碼（例如：0912345678）或國際格式。";
}
