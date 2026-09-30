Feature: RFC 7009 權杖撤銷 (Token Revocation) 與密碼變更安全連動
  身為 OAuth 客戶端或登出的會員
  我希望撤銷特定 Refresh Token 或在密碼變更時全鏈路失效
  以確保遭廢止或遭竊取的 Refresh Token 無法繼續換取新的 Access Token

  Background:
    Given 初始化 Auth Server 測試伺服器
    And 已註冊原生 App 公用客戶端 "native-mobile-app"，其 Universal Link 為 "https://app.1111.com.tw/oauth/callback"

  Scenario: 原生 App (Public Client) 成功撤銷 Refresh Token
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 原生 App "native-mobile-app" 已取得授權碼
    And 原生 App "native-mobile-app" 僅以 code_verifier 換票且不帶 client_secret
    When 原生 App "native-mobile-app" 撤銷剛剛取得的 Refresh Token
    Then 回應狀態碼應為 200
    When 原生 App "native-mobile-app" 嘗試以已被撤銷的 Refresh Token 續約
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"

  Scenario: 撤銷不存在或已無效的 Token 時依 RFC 7009 仍回傳 200
    When 原生 App "native-mobile-app" 撤銷無效的 Token "invalid-non-existent-token"
    Then 回應狀態碼應為 200

  Scenario: 會員變更密碼後舊 Refresh Token 立即被作廢且既有 Cookie 會話失效
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 原生 App "native-mobile-app" 已取得授權碼
    And 原生 App "native-mobile-app" 僅以 code_verifier 換票且不帶 client_secret
    When 會員變更密碼並刷新安全戳記
    Then 該會員名下的所有有效 Refresh Token 皆已被標記為作廢
    When 原生 App "native-mobile-app" 嘗試以已被撤銷的 Refresh Token 續約
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"
    When 該會員嘗試以先前的 Session Cookie 發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為會員登入頁 "https://member.1111.com.tw/login"
