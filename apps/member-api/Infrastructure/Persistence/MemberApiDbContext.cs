using Microsoft.EntityFrameworkCore;

namespace MemberApi.Infrastructure.Persistence;

public class MemberApiDbContext(DbContextOptions<MemberApiDbContext> options) : DbContext(options)
{
}
