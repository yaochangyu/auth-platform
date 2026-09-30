using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace MemberApi.Sms;

public partial class MitakeSmsSender(
    HttpClient httpClient,
    IOptions<MitakeSmsOptions> options,
    ILogger<MitakeSmsSender> logger) : ISmsSender
{
    private readonly MitakeSmsOptions _options = options.Value;

    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["username"] = _options.Username,
            ["password"] = _options.Password,
            ["dstaddr"] = phoneNumber,
            ["smbody"] = message,
        };

        try
        {
            using var content = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync(_options.EndpointUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // 三竹標準回應檢核：statuscode=1 代表已成功寫入主機佇列排程發送，statuscode=0 代表已立即送出
            var match = StatusCodeRegex().Match(responseBody);
            if (match.Success)
            {
                var code = match.Groups[1].Value;
                if (code == "1" || code == "0")
                {
                    logger.LogInformation("成功透過三竹簡訊發送至 {PhoneNumber} (StatusCode: {Code})", phoneNumber, code);
                    return;
                }
            }

            logger.LogError("三竹簡訊發送至 {PhoneNumber} 失敗，回應內容：{ResponseBody}", phoneNumber, responseBody);
            throw new InvalidOperationException($"三竹簡訊發送失敗：{responseBody.Trim()}");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "調用三竹簡訊 API ({Endpoint}) 發送至 {PhoneNumber} 異常：{ErrorMessage}",
                _options.EndpointUrl, phoneNumber, ex.Message);
            throw;
        }
    }

    [GeneratedRegex(@"(?im)^statuscode\s*=\s*([0-9a-zA-Z]+)")]
    private static partial Regex StatusCodeRegex();
}
