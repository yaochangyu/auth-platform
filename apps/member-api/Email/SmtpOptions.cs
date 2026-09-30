using MailKit.Security;

namespace MemberApi.Email;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "mailpit";
    public int Port { get; set; } = 1025;
    public string FromEmail { get; set; } = "no-reply@1111.com.tw";
    public string FromName { get; set; } = "1111 人力銀行";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;
}
