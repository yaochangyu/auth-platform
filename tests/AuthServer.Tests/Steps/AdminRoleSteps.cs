using AuthServer.Tests.Support;
using Npgsql;
using Reqnroll;

namespace AuthServer.Tests.Steps;

[Binding]
public class AdminRoleSteps(AuthorizeSteps authorize)
{
    [Given("該會員的角色為 \"(.*)\"")]
    public async Task Given會員角色(string role)
    {
        await using var connection = new NpgsqlConnection(TestRunHooks.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "update members set role = @role where id = @id";
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("id", authorize.MemberId);
        await command.ExecuteNonQueryAsync();
    }
}
