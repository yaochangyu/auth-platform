namespace AuthServer.Infrastructure;

public static class PublicUrl
{
    // 經 Proxy 時 Host 會是內部位址或會員中心網域，已設定對外 issuer 時以它為準組出絕對網址。
    public static string Absolute(HttpRequest request, IConfiguration configuration, string pathAndQuery)
    {
        var origin = configuration["Auth:Issuer"] is { Length: > 0 } issuer
            ? issuer.TrimEnd('/')
            : $"{request.Scheme}://{request.Host}";
        return origin + request.PathBase + pathAndQuery;
    }
}
