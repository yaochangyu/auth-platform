namespace AuthShared;

// developer-api 建立、admin-api 審核，兩邊共用同一份定義，避免資料庫整數值對不上。
public enum ApplicationStatus
{
    Active = 0,
    PendingReview = 1,
    Suspended = 2,
}
