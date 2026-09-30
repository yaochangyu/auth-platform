using MemberApi.Entities;

namespace MemberApi.Contracts;

public record SendSmsOtpRequest(string PhoneNumber, SmsOtpPurpose Purpose = SmsOtpPurpose.PhoneVerification);
