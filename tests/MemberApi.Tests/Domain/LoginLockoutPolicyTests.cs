using MemberApi.Domain;

namespace MemberApi.Tests.Domain;

// 純記憶體單元測試：LoginLockoutPolicy 無任何外部依賴，不需要資料庫。
public class LoginLockoutPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 常數為五次與十五分鐘()
    {
        Assert.Equal(5, LoginLockoutPolicy.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), LoginLockoutPolicy.LockoutDuration);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void 未達上限時只累加不鎖定(int before, int after)
    {
        var state = LoginLockoutPolicy.RegisterFailure(new LoginLockoutState(before, null), Now);

        Assert.Equal(new LoginLockoutState(after, null), state);
        Assert.False(LoginLockoutPolicy.IsLocked(state, Now));
    }

    [Fact]
    public void 第五次失敗鎖定十五分鐘()
    {
        var state = LoginLockoutPolicy.RegisterFailure(new LoginLockoutState(4, null), Now);

        Assert.Equal(new LoginLockoutState(5, Now.AddMinutes(15)), state);
        Assert.True(LoginLockoutPolicy.IsLocked(state, Now));
    }

    [Fact]
    public void 鎖定期間內判定為鎖定且到期時刻即解除()
    {
        var state = new LoginLockoutState(5, Now.AddMinutes(15));

        Assert.True(LoginLockoutPolicy.IsLocked(state, Now.AddMinutes(14).AddSeconds(59)));
        Assert.False(LoginLockoutPolicy.IsLocked(state, Now.AddMinutes(15)));
    }

    [Fact]
    public void 未鎖定的狀態不是鎖定()
    {
        Assert.False(LoginLockoutPolicy.IsLocked(new LoginLockoutState(3, null), Now));
    }

    [Fact]
    public void 鎖定逾期後首次失敗重設為一且解除鎖定()
    {
        var expired = new LoginLockoutState(5, Now.AddSeconds(-1));

        Assert.Equal(new LoginLockoutState(1, null), LoginLockoutPolicy.RegisterFailure(expired, Now));
    }

    [Fact]
    public void 登入成功的初始狀態為零且未鎖定()
    {
        Assert.Equal(new LoginLockoutState(0, null), LoginLockoutPolicy.Cleared);
        Assert.False(LoginLockoutPolicy.IsLocked(LoginLockoutPolicy.Cleared, Now));
    }
}
