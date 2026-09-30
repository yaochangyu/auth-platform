Feature: 原生 App 公用客戶端 (Public Client) PKCE 授權與 Universal Links 驗證
  身為原生行動 App (Public Client)
  我希望透過 PKCE S256 與 HTTPS Universal Links 進行授權碼交換
  以便在不使用 Client Secret 的前提下安全換發 Token，並防止授權碼被惡意 URL Scheme 劫持

  Background:
    Given 初始化 Auth Server 測試伺服器
    And 已註冊原生 App 公用客戶端 "native-mobile-app"，其 Universal Link 為 "https://app.1111.com.tw/oauth/callback"

  Scenario: 原生 App 以 S256 PKCE 與 Universal Links 成功取得授權碼並免 secret 換發權杖
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 原生 App "native-mobile-app" 以合法的 S256 PKCE 與重定向網址發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為 "https://app.1111.com.tw/oauth/callback"
    And 重定向網址應帶有 Authorization Code
    When 原生 App "native-mobile-app" 僅以 code_verifier 換票且不帶 client_secret
    Then 回應狀態碼應為 200
    And 回應的 token_type 應為 "Bearer"
    And 回應應包含 Refresh Token
    And Access Token 的 sub 應為該會員

  Scenario Outline: 原生 App 缺少 code_challenge 或採用 plain 演算法時拒絕授權
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 原生 App "native-mobile-app" 發起授權請求且 <PKCE 情況>
    Then 回應狀態碼應為 400
    And 回應內容應包含錯誤碼 "invalid_request"
    And 回應不應包含 Location 標頭

    Examples:
      | PKCE 情況                     |
      | 未提供 code_challenge         |
      | code_challenge_method 為 plain |
      | 未提供 code_challenge_method  |

  Scenario: 原生 App 以不匹配的 code_verifier 換票時拒絕換票
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 原生 App "native-mobile-app" 已取得授權碼
    When 原生 App "native-mobile-app" 以錯誤的 code_verifier 換票且不帶 client_secret
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"

  Scenario: 原生 App 若試圖使用未登記的自訂 scheme 則拒絕重定向
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 原生 App "native-mobile-app" 以重定向網址 "myapp://oauth/callback" 發起授權請求
    Then 回應狀態碼應為 400
    And 回應不應包含 Location 標頭
