Feature: Token 換發與 Refresh Token 輪替
  身為已取得 Authorization Code 的 OAuth Client
  我希望以 Authorization Code 與 code_verifier 兌換 Access Token、ID Token 與 Refresh Token
  以便代表會員存取資源，且權杖被竊用時能被偵測並撤銷

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: 以正確的 code_verifier 兌換 Authorization Code 取得 Token
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得 Authorization Code
    When 以 Client "member-web-spa" 及正確的 code_verifier 兌換 Token
    Then 回應狀態碼應為 200
    And 回應的 token_type 應為 "Bearer"
    And Access Token 應為 RS256 簽章且可用 JWKS 公開金鑰驗證
    And Access Token 的 sub 應為該會員
    And Access Token 的有效期間應為 15 分鐘
    And Access Token 不應包含 security_stamp
    And 回應應包含 aud 為 "member-web-spa" 的 ID Token
    And 回應應包含 Refresh Token

  Scenario: 授權請求未包含 offline_access 時不核發 Refresh Token
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得不含 offline_access 的 Authorization Code
    When 以 Client "member-web-spa" 及正確的 code_verifier 兌換 Token
    Then 回應狀態碼應為 200
    And 回應不應包含 Refresh Token

  Scenario: Confidential Client 以正確的 client_secret 兌換 Token
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "demo-confidential-app" 取得 Authorization Code
    When 以 Client "demo-confidential-app" 及正確的 code_verifier 與 client_secret 兌換 Token
    Then 回應狀態碼應為 200
    And 回應應包含 Refresh Token

  Scenario Outline: Confidential Client 的 client_secret 錯誤或缺少時拒絕換票
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "demo-confidential-app" 取得 Authorization Code
    When 以 Client "demo-confidential-app" 兌換 Token 且 <Secret 情況>
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

    Examples:
      | Secret 情況                |
      | 提供錯誤的 client_secret   |
      | 未提供 client_secret       |

  Scenario Outline: code_verifier 與原 code_challenge 不符時拒絕換票
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得 Authorization Code
    When 以 Client "member-web-spa" 兌換 Token 且 <Verifier 情況>
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "<錯誤碼>"
    And 回應不應包含 access_token

    Examples:
      | Verifier 情況        | 錯誤碼          |
      | code_verifier 不符   | invalid_grant   |
      | 未提供 code_verifier | invalid_request |

  Scenario: redirect_uri 與授權請求不一致時拒絕換票
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得 Authorization Code
    When 以 Client "member-web-spa" 兌換 Token 且 redirect_uri 與授權請求不一致
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"

  Scenario: Authorization Code 只能使用一次
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得 Authorization Code
    And 以 Client "member-web-spa" 及正確的 code_verifier 兌換 Token
    When 以 Client "member-web-spa" 及正確的 code_verifier 兌換 Token
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"

  Scenario: 超過 1 分鐘的 Authorization Code 無法換票
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得 Authorization Code
    And 時間已經過了 2 分鐘
    When 以 Client "member-web-spa" 及正確的 code_verifier 兌換 Token
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"

  Scenario: 以 Refresh Token 續約時換發新的 Refresh Token
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 兌換取得 Token
    When 以 Refresh Token 續約
    Then 回應狀態碼應為 200
    And 回應應包含新的 Access Token
    And 回應的新 Refresh Token 應與舊的不同

  Scenario: 重複使用已被輪替的 Refresh Token 時撤銷整個授權
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 兌換取得 Token
    And 已以 Refresh Token 續約
    When 重複使用已被輪替的舊 Refresh Token 續約
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"
    When 使用輪替後取得的新 Refresh Token 續約
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"

  Scenario: 會員變更密碼後先前核發的 Refresh Token 失效
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 兌換取得 Token
    And 該會員的 Security Stamp 已被更新
    When 以 Refresh Token 續約
    Then 回應狀態碼應為 400
    And 回應的 error 欄位應為 "invalid_grant"
