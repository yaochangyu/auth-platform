Feature: 忘記密碼與重設密碼流程
  身為忘記密碼的會員
  我希望能透過 Email 申請重設密碼並設定新密碼
  以便在不洩漏帳號是否存在的前提下安全地恢復帳號存取權，並使所有舊裝置的登入立即失效

  Background:
    Given 初始化測試伺服器

  Scenario: 已註冊會員申請忘記密碼成功
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "forgot-existing@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "forgot-existing@1111.com.tw" 呼叫忘記密碼 API
    Then 回應狀態碼應為 200
    And 系統應為該會員寫入一筆尚未使用的重設密碼驗證權杖
    And 系統應為該會員寫入一筆待發送之重設密碼 Outbox 訊息

  Scenario: 未註冊的 Email 申請忘記密碼仍回傳一致的成功回應
    When 使用者以 Email "forgot-not-registered@1111.com.tw" 呼叫忘記密碼 API
    Then 回應狀態碼應為 200
    And 回應內容的 message 應與已註冊會員申請成功時的回應內容完全相同

  Scenario: Email 格式錯誤時申請忘記密碼失敗
    When 使用者以 Email "not-an-email" 呼叫忘記密碼 API
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details

  Scenario: 60 秒冷卻時間內重複申請忘記密碼遭拒絕
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "forgot-cooldown@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    And 該會員已於 30 秒前申請過忘記密碼
    When 使用者以 Email "forgot-cooldown@1111.com.tw" 呼叫忘記密碼 API
    Then 回應狀態碼應為 429
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 超過 60 秒冷卻時間後可再次申請忘記密碼
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "forgot-recooldown@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    And 該會員已於 61 秒前申請過忘記密碼
    When 使用者以 Email "forgot-recooldown@1111.com.tw" 呼叫忘記密碼 API
    Then 回應狀態碼應為 200

  Scenario: 再次申請忘記密碼時作廢先前尚未使用的重設權杖
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "forgot-invalidate@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    And 該會員已於 61 秒前申請過忘記密碼並取得驗證權杖 "old-reset-token"
    When 使用者以 Email "forgot-invalidate@1111.com.tw" 呼叫忘記密碼 API
    Then 回應狀態碼應為 200
    And 驗證權杖 "old-reset-token" 應已失效

  Scenario: 攜帶有效權杖重設密碼成功
    Given 系統已存在一筆狀態為 Active 的會員及其尚未使用的有效重設密碼權杖 "valid-reset-token"，Email 為 "reset-success@1111.com.tw"
    When 使用者攜帶驗證權杖 "valid-reset-token" 設定新密碼 "N3wP@ssw0rd2026!" 並確認密碼 "N3wP@ssw0rd2026!" 呼叫重設密碼 API
    Then 回應狀態碼應為 200
    And 該會員應可使用新密碼 "N3wP@ssw0rd2026!" 成功登入
    And 該驗證權杖應被標記為已使用

  Scenario Outline: 新密碼驗證失敗時重設密碼失敗
    Given 系統已存在一筆狀態為 Active 的會員及其尚未使用的有效重設密碼權杖 "invalid-input-token"，Email 為 "reset-invalid@1111.com.tw"
    When 使用者攜帶驗證權杖 "invalid-input-token" 設定新密碼 "<newPassword>" 並確認密碼 "<confirmPassword>" 呼叫重設密碼 API
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details

    Examples:
      | newPassword       | confirmPassword |
      | weak              | weak             |
      | N3wP@ssw0rd2026!  | Different1!      |

  Scenario: 查無驗證權杖時重設密碼失敗
    When 使用者攜帶驗證權杖 "unknown-reset-token" 設定新密碼 "N3wP@ssw0rd2026!" 並確認密碼 "N3wP@ssw0rd2026!" 呼叫重設密碼 API
    Then 回應狀態碼應為 404
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 驗證權杖已過期或已使用時重設密碼失敗
    Given 系統已存在一筆狀態為 Active 的會員及其已過期或已使用的重設密碼權杖 "expired-reset-token"，Email 為 "reset-expired@1111.com.tw"
    When 使用者攜帶驗證權杖 "expired-reset-token" 設定新密碼 "N3wP@ssw0rd2026!" 並確認密碼 "N3wP@ssw0rd2026!" 呼叫重設密碼 API
    Then 回應狀態碼應為 410
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 密碼重設成功後使會員所有歷史裝置的 Session Cookie 立即失效
    Given 使用者已成功登入並取得有效的 Session Cookie
    And 該會員已取得尚未使用的有效重設密碼權杖 "revoke-session-token"
    When 使用者攜帶驗證權杖 "revoke-session-token" 設定新密碼 "N3wP@ssw0rd2026!" 並確認密碼 "N3wP@ssw0rd2026!" 呼叫重設密碼 API
    Then 回應狀態碼應為 200
    When 使用者攜帶重設密碼前的 Session Cookie 呼叫登出 API
    Then 回應狀態碼應為 401
