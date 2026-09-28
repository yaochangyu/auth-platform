Feature: 會員註冊與 Email 驗證啟用
  身為新使用者
  我希望能透過 Email 註冊帳號並完成信箱驗證
  以便啟用會員身分並開始使用會員中心服務

  Background:
    Given 初始化測試伺服器

  Scenario: 正常註冊成功建立待驗證會員
    When 使用者以下列資料呼叫註冊 API
      | email               | password       | confirmPassword | displayName |
      | member@1111.com.tw  | P@ssw0rd2026!  | P@ssw0rd2026!    | 王大明       |
    Then 回應狀態碼應為 201
    And 回應內容的會員狀態應為 "Pending"
    And 系統應為該會員寫入一筆尚未使用的驗證權杖
    And 系統應為該會員寫入一筆待發送之驗證信 Outbox 訊息

  Scenario Outline: 註冊請求驗證失敗時回傳 400 錯誤
    When 使用者以下列資料呼叫註冊 API
      | email       | password     | confirmPassword     | displayName     |
      | <email>     | <password>   | <confirmPassword>   | <displayName>   |
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details
    And 錯誤內容應包含 "<invalidField>" 欄位的錯誤訊息

    Examples:
      | email               | password       | confirmPassword | displayName | invalidField    |
      | not-an-email        | P@ssw0rd2026!  | P@ssw0rd2026!    | 王大明       | email           |
      | member@1111.com.tw  | weak           | weak             | 王大明       | password        |
      | member@1111.com.tw  | P@ssw0rd2026!  | Different1!      | 王大明       | confirmPassword |

  Scenario: 同一 Email 於 Pending 狀態下重複註冊時靜默重寄驗證信且不覆蓋密碼
    Given 系統已存在一筆 Email 為 "duplicate-pending@1111.com.tw" 且狀態為 Pending 的會員
    When 使用者以相同 Email 但不同密碼再次呼叫註冊 API
    Then 回應狀態碼應為 201
    And 回應內容的會員狀態應為 "Pending"
    And 該會員的密碼雜湊應維持原始值未被覆蓋
    And 系統應為該會員派發一筆新的驗證信 Outbox 訊息

  Scenario: 攜帶有效驗證權杖時 Email 驗證成功並啟用會員
    Given 系統已存在一筆狀態為 Pending 的會員及其尚未使用的有效驗證權杖 "valid-token"
    When 使用者攜帶驗證權杖 "valid-token" 呼叫 Email 驗證 API
    Then 回應狀態碼應為 200
    And 回應內容的會員狀態應為 "Active"
    And 該驗證權杖應被標記為已使用

  Scenario: 驗證權杖格式錯誤時回傳 400
    When 使用者攜帶空白驗證權杖呼叫 Email 驗證 API
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details

  Scenario: 查無驗證權杖時回傳 404
    When 使用者攜帶驗證權杖 "unknown-token" 呼叫 Email 驗證 API
    Then 回應狀態碼應為 404
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 驗證權杖已過期或已使用時回傳 410
    Given 系統已存在一筆已過期或已使用的驗證權杖 "expired-token"
    When 使用者攜帶驗證權杖 "expired-token" 呼叫 Email 驗證 API
    Then 回應狀態碼應為 410
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 已啟用狀態的會員嘗試再次驗證 Email 時回傳 409
    Given 系統已存在一筆狀態為 Active 的會員及其尚未使用的有效驗證權杖 "active-member-token"
    When 使用者攜帶驗證權杖 "active-member-token" 呼叫 Email 驗證 API
    Then 回應狀態碼應為 409
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
