Feature: OAuth 2.1 授權碼模式 (PKCE) 與主網域 Session Cookie SSO 橋接
  身為串接統一登入的應用程式
  我希望以 Authorization Code + PKCE 向授權伺服器發起授權
  以便已登入的會員無感取得 Authorization Code，未登入者被導向會員登入頁

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: 已登入會員透過第一方 Client 免同意取得 Authorization Code
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 以 Client "member-web-spa" 及合法的 PKCE 參數發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為該 Client 已登記的 redirect_uri
    And 重定向網址應帶有 Authorization Code 與原始 state

  Scenario: 未登入時重定向至會員登入頁並帶上 returnUrl
    When 以 Client "member-web-spa" 及合法的 PKCE 參數發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為會員登入頁 "https://member.1111.com.tw/login"
    And returnUrl 應為原始的授權請求網址

  Scenario: 未登入且 prompt=none 時回報 login_required 而不出現登入畫面
    When 以 Client "member-web-spa" 及合法的 PKCE 參數並帶 prompt=none 發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為該 Client 已登記的 redirect_uri
    And 重定向網址的 error 參數應為 "login_required"

  Scenario: 密碼變更後的舊 Session Cookie 不再視為已登入
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 該會員的 Security Stamp 已被更新
    When 以 Client "member-web-spa" 及合法的 PKCE 參數發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為會員登入頁 "https://member.1111.com.tw/login"

  # PKCE 驗證早於 redirect_uri 比對，此時不可信任 redirect_uri，依 RFC 6749 §4.1.2.1 直接回應錯誤而不重定向。
  Scenario Outline: 缺少或非 S256 的 PKCE 參數一律拒絕
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 以 Client "member-web-spa" 發起授權請求且 <PKCE 情況>
    Then 回應狀態碼應為 400
    And 回應內容應包含錯誤碼 "invalid_request"
    And 回應不應包含 Location 標頭

    Examples:
      | PKCE 情況                     |
      | 未提供 code_challenge         |
      | code_challenge_method 為 plain |
      | 未提供 code_challenge_method  |

  Scenario: 未登記的 redirect_uri 不得被重定向
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 以 Client "member-web-spa" 發起授權請求且 redirect_uri 為未登記的網址
    Then 回應狀態碼應為 400
    And 回應不應包含 Location 標頭

  Scenario: 第三方 Client 尚未取得 Consent 前不自動核發 Authorization Code
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 以 Client "demo-third-party-app" 及合法的 PKCE 參數發起授權請求
    Then 回應狀態碼應為 302
    And 重定向網址的 error 參數應為 "consent_required"
    And 重定向網址不應帶有 Authorization Code
