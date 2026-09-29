Feature: 被管理員停用的 Client（緊急斷路）
  身為平台管理員
  我希望停用某個應用程式後，它的 Client 立刻無法再取得授權或 Token
  以便在發現濫用或外洩時即時阻斷，並可在確認安全後恢復

  Background:
    Given 初始化 Auth Server 測試伺服器
    And 會員已登入且持有有效的主網域 Session Cookie

  Scenario: 被停用的 Client 無法發起授權
    Given Confidential Client "suspend-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "openid、profile"，且已被管理員停用
    When 以 Client "suspend-test-app" 及合法的 PKCE 參數並要求範疇 "openid、profile" 發起授權請求
    Then 回應狀態碼應為 400
    And 回應不應包含 Location 標頭

  Scenario: 被停用的 Client 無法以 Client Credentials 換票
    Given Confidential Client "suspend-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "openid、profile"，且已被管理員停用
    When 以 Client "suspend-test-app" 使用 client_credentials 換票，要求範疇 "profile"
    Then 回應狀態碼應為 400
    And 回應不應包含 access_token

  Scenario: 未被停用的同一個 Client 可以正常授權與換票
    Given Confidential Client "suspend-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "openid、profile"
    When 以 Client "suspend-test-app" 使用 client_credentials 換票，要求範疇 "profile"
    Then 回應狀態碼應為 200

  Scenario: 未被停用的同一個 Client 可以正常發起授權
    Given Confidential Client "suspend-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "openid、profile"
    When 以 Client "suspend-test-app" 及合法的 PKCE 參數並要求範疇 "openid、profile" 發起授權請求
    Then 回應狀態碼應為 302
    And 重定向網址應帶有 Authorization Code 與原始 state

  Scenario: 停用前取得的授權碼在 Client 被停用後無法換票
    Given Confidential Client "suspend-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "openid、profile"
    And 已以 Client "suspend-test-app" 取得僅含範疇 "openid、profile" 的 Authorization Code
    And Client "suspend-test-app" 此後被管理員停用
    When 以 Client "suspend-test-app" 使用其 Secret 及正確的 code_verifier 兌換 Token
    Then 回應狀態碼應為 400
    And 回應不應包含 access_token

