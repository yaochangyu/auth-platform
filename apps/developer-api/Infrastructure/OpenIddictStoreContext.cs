using AuthShared;
using Microsoft.EntityFrameworkCore;

namespace DeveloperApi.Infrastructure;

// auth-server 擁有並 migrate 的 OpenIddict 資料表；developer-api 只透過 OpenIddict 管理器讀寫 Client 設定。
public class OpenIddictStoreContext(DbContextOptions<OpenIddictStoreContext> options) : OpenIddictStoreDbContext(options);
