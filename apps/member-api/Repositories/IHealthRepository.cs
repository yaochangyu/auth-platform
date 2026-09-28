namespace MemberApi.Repositories;

public interface IHealthRepository
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
