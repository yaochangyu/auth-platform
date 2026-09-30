namespace MemberApi.Sms;

public class MitakeSmsOptions
{
    public const string SectionName = "MitakeSms";

    public string EndpointUrl { get; set; } = "http://smspit:8026/api/mtk/SmSend";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}
