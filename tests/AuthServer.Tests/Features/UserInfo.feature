Feature: OIDC UserInfo 端點與 Scope 欄位過濾
  身為持有 Access Token 的 OAuth Client
  我希望以 Bearer Access Token 查詢會員的標準 OIDC Claims
  以便只取得會員授權範疇內的欄位，且無效的 Token 一律被拒絕

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: 僅授權 openid 時只回傳 sub
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid" 的 Access Token
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 200
    And 回應應只包含欄位 "sub"
    And 回應的 sub 應為該會員

  Scenario: 授權 email 範疇時回傳 email 與 email_verified
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid、email" 的 Access Token
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 200
    And 回應應只包含欄位 "sub、email、email_verified"
    And 回應的 email 應為會員的 Email
    And 回應的 email_verified 應為 true

  Scenario: 授權 profile 範疇時回傳 nickname 與以 Unix 秒數表示的 updated_at
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid、profile" 的 Access Token
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 200
    And 回應應只包含欄位 "sub、nickname、updated_at"
    And 回應的 nickname 應為會員的顯示名稱
    And 回應的 updated_at 應為會員資料最後更新時間的 Unix 秒數
    And 回應應帶有 Cache-Control 為 "no-store"

  Scenario: UserInfo 端點同時支援 POST 方法
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid" 的 Access Token
    When 以 Bearer Access Token 呼叫 POST /connect/userinfo
    Then 回應狀態碼應為 200
    And 回應應只包含欄位 "sub"

  Scenario: 授權全部範疇時回傳完整的標準 Claims
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid、profile、email" 的 Access Token
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 200
    And 回應應只包含欄位 "sub、email、email_verified、nickname、updated_at"

  Scenario: Access Token 未包含 openid 範疇時拒絕存取
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "profile" 的 Access Token
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 403

  Scenario: 未提供 Access Token 時回傳 401
    When 未帶 Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 401
    And 回應應包含 WWW-Authenticate 標頭

  Scenario: 偽造的 Access Token 回傳 401
    When 以偽造的 Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 401
    And 回應的 WWW-Authenticate 應包含錯誤碼 "invalid_token"

  Scenario: 簽章被竄改的 Access Token 回傳 401
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid、profile、email" 的 Access Token
    When 以簽章被竄改的 Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 401

  Scenario: 已過期的 Access Token 回傳 401
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid、profile、email" 的 Access Token
    And 時間已經過了 16 分鐘
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 401

  Scenario: 會員帳號已被刪除時回傳 401
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 取得僅含範疇 "openid、profile、email" 的 Access Token
    And 該會員帳號已被刪除
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 401

  Scenario: 授權被撤銷後先前核發的 Access Token 無法取得 UserInfo
    Given 會員已登入且持有有效的主網域 Session Cookie
    And 已以 Client "member-web-spa" 兌換取得 Token
    And 已以 Refresh Token 續約
    And 重複使用已被輪替的舊 Refresh Token 續約
    When 以最初取得的 Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 401
