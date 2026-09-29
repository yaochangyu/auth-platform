Feature: 登入防爆破與帳號漸進式暫時鎖定
  身為平台的安全維運人員
  我希望系統能自動偵測並暫時鎖定連續密碼錯誤的帳號
  以便防止惡意自動化腳本對會員密碼進行字典攻擊

  Background:
    Given 初始化測試伺服器

  Scenario Outline: 連續密碼錯誤未達鎖定門檻時回傳 401 Unauthorized
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "lockout-partial@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以錯誤密碼連續呼叫登入 API "<次數>" 次
    Then 每次回應狀態碼皆為 401
    And 該會員的 failedLoginAttempts 應為 <次數>
    And 該會員不應處於鎖定狀態

    Examples:
      | 次數 |
      | 1    |
      | 2    |
      | 3    |
      | 4    |

  Scenario: 連續密碼錯誤第 5 次觸發 15 分鐘漸進式鎖定
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "lockout-trigger@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    And 該會員已連續輸入錯誤密碼 4 次
    When 使用者以錯誤密碼呼叫登入 API
    Then 回應狀態碼應為 423
    And 回應內容應為符合 RFC 7807 的 LockoutProblemDetails
    And 錯誤類型應為 "https://auth.1111.com.tw/errors/account-locked"
    And 回應內容的 failedLoginAttempts 應為 5
    And 回應內容的 lockoutEndAt 應為 15 分鐘後的 UTC 時間戳記
    And 回應不應包含 Set-Cookie 標頭

  Scenario: 帳號鎖定期間即使輸入正確密碼仍直接拒絕登入
    Given 系統已存在一筆狀態為 Active 且目前處於鎖定中的會員，Email 為 "lockout-active@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以正確密碼呼叫登入 API
    Then 回應狀態碼應為 423
    And 回應內容應為符合 RFC 7807 的 LockoutProblemDetails
    And 回應不應包含 Set-Cookie 標頭

  Scenario: 鎖定時效超過 15 分鐘後輸入正確密碼登入成功並重設鎖定狀態
    Given 系統已存在一筆狀態為 Active 且鎖定時效已過期的會員，Email 為 "lockout-expired@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以正確密碼呼叫登入 API
    Then 回應狀態碼應為 200
    And 回應標頭 Set-Cookie 應包含 ".AspNetCore.Cookies"
    And 該會員的 failedLoginAttempts 應為 0
    And 該會員的 lockoutEndAt 應為空

  Scenario: 密碼錯誤未達鎖定門檻前輸入正確密碼登入成功並重設錯誤計數
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "lockout-recover@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    And 該會員已連續輸入錯誤密碼 3 次
    When 使用者以正確密碼呼叫登入 API
    Then 回應狀態碼應為 200
    And 該會員的 failedLoginAttempts 應為 0

  Scenario: 不存在的 Email 登入失敗維持 401 且不觸發或洩漏鎖定資訊
    When 使用者以 Email "lockout-not-registered@1111.com.tw" 密碼 "AnyPassw0rd!" 呼叫登入 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
    And 回應內容不應包含 failedLoginAttempts 或 lockoutEndAt 欄位
    And 回應不應包含 Set-Cookie 標頭

  Scenario: 並行送出 10 次錯誤密碼
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "lockout-concurrent@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者並行送出 10 次錯誤密碼呼叫登入 API
    Then 該會員的 failedLoginAttempts 應為 10
    And 該會員應處於鎖定狀態

  Scenario: 鎖定時效過期後輸入錯誤密碼
    Given 系統已存在一筆狀態為 Active 且鎖定時效已過期的會員，Email 為 "lockout-expired-wrongpassword@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以錯誤密碼呼叫登入 API
    Then 回應狀態碼應為 401
    And 該會員的 failedLoginAttempts 應為 1
    And 該會員的 lockoutEndAt 應為空

  Scenario: 鎖定已過期的會員被並行送出 10 次錯誤密碼
    Given 系統已存在一筆狀態為 Active 且鎖定時效已過期的會員，Email 為 "lockout-expired-concurrent@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者並行送出 10 次錯誤密碼呼叫登入 API
    Then 該會員的 failedLoginAttempts 應為 10
    And 該會員應處於鎖定狀態
