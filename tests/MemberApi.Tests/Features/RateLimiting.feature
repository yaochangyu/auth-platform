Feature: 公開端點業務層限速 (Rate Limiting) 與防爆破防護
  身為系統安全架構師
  我希望針對公開開放的認證相關端點配置滑動視窗限速
  以防範惡意暴力破解、撞庫與郵件轟炸攻擊

  Background:
    Given 初始化測試伺服器

  Scenario: 連續多次呼叫註冊端點超出頻率限制時回傳 429 與 Retry-After
    Given 來自客戶端 IP "203.0.113.10"
    When 該客戶端快速連續呼叫註冊 API 5 次
    Then 所有前置請求皆應獲得非 429 的回應
    When 該客戶端再次呼叫註冊 API
    Then 回應狀態碼應為 429
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
    And 錯誤類型應為 "https://auth.1111.com.tw/errors/too-many-requests"
    And 回應標頭應包含 "Retry-After"

  Scenario: 連續多次呼叫忘記密碼端點超出頻率限制時回傳 429 與 Retry-After
    Given 來自客戶端 IP "203.0.113.11"
    When 該客戶端快速連續呼叫忘記密碼 API 5 次
    Then 所有前置請求皆應獲得非 429 的回應
    When 該客戶端再次呼叫忘記密碼 API
    Then 回應狀態碼應為 429
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
    And 回應標頭應包含 "Retry-After"

  Scenario: 不同客戶端 IP 擁有獨立的限速配額
    Given 來自客戶端 IP "203.0.113.20"
    When 該客戶端快速連續呼叫註冊 API 5 次
    Then 所有前置請求皆應獲得非 429 的回應
    When 另一來自客戶端 IP "203.0.113.21" 呼叫註冊 API
    Then 回應狀態碼應為非 429

  Scenario: 同一客戶端存取不同公開端點擁有獨立的限速配額
    Given 來自客戶端 IP "203.0.113.30"
    When 該客戶端快速連續呼叫註冊 API 5 次
    Then 所有前置請求皆應獲得非 429 的回應
    When 該客戶端呼叫忘記密碼 API
    Then 回應狀態碼應為非 429
