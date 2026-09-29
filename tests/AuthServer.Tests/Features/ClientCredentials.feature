Feature: Client Credentials 換發 M2M Access Token
  身為沒有人介入的後端服務（Server-to-Server）
  我希望以自己的 client_id 與 client_secret 直接向授權伺服器換發短效 Access Token
  以便在沒有會員登入的情境下向資源伺服器證明身分

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: Confidential Client 以 client_id 與 client_secret 換發 M2M Access Token
    Given Confidential Client "m2m-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "profile、email"
    When 以 Client "m2m-test-app" 使用 client_credentials 換票，要求範疇 "profile"
    Then 回應狀態碼應為 200
    And 回應的 token_type 應為 "Bearer"
    And Access Token 應為 RS256 簽章且可用 JWKS 公開金鑰驗證
    And Access Token 的有效期間應為 15 分鐘
    And Access Token 的 sub 應為 Client "m2m-test-app"
    And Access Token 的 scope 應包含 "profile"
    And 回應不應包含 Refresh Token
    And 回應不應包含 ID Token

  Scenario: 換票時 client_secret 錯誤會被拒絕
    Given Confidential Client "m2m-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "profile、email"
    When 以 Client "m2m-test-app" 使用錯誤的 client_secret 以 client_credentials 換票
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

  Scenario: 要求身分類範疇 openid 時被拒絕，因為 M2M 沒有會員
    Given Confidential Client "m2m-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "openid、profile"
    When 以 Client "m2m-test-app" 使用 client_credentials 換票，要求範疇 "openid"
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_scope"

  Scenario: 要求未被授權的範疇時被拒絕
    Given Confidential Client "m2m-test-app" 已發行 Secret，允許 Client Credentials，範疇為 "profile"
    When 以 Client "m2m-test-app" 使用 client_credentials 換票，要求範疇 "email"
    Then 回應狀態碼應為 400

  Scenario: 未被授權 Client Credentials 的 Client 不能使用此授權方式
    Given Confidential Client "m2m-test-app" 已發行 Secret，不允許 Client Credentials，範疇為 "profile"
    When 以 Client "m2m-test-app" 使用 client_credentials 換票，要求範疇 "profile"
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "unauthorized_client"

  Scenario: Public Client 不能使用 Client Credentials
    Given Public Client "m2m-public-app" 已建立
    When 以 Public Client "m2m-public-app" 使用 client_credentials 換票
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_request"
