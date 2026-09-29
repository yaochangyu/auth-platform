Feature: 第三方應用程式授權同意 (Consent) 與 Consent Ticket 防竄改
  身為已登入的會員
  我希望在第三方應用程式取得我的個人資料前，能明確同意或拒絕其請求的範疇
  以便掌控哪些應用程式可以存取我的資料，且授權參數無法被偽造或重放

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: 第三方 Client 尚未取得授權時導向同意頁並帶上 consent_id
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 以 Client "demo-third-party-app" 及合法的 PKCE 參數發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為同意頁 "https://member.1111.com.tw/oauth/consent"
    And 重定向網址應帶有 consent_id
    And 重定向網址不應帶有 Authorization Code

  Scenario: 前端以 consent_id 取得應用程式資訊與請求的範疇
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 前端以該 consent_id 取得同意資訊
    Then 回應狀態碼應為 200
    And 同意資訊的應用程式名稱應為 "示範第三方應用程式"
    And 同意資訊應列出範疇 "openid、profile、email"

  Scenario: 未登入者無法以 consent_id 取得同意資訊
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 未帶 Session Cookie 以該 consent_id 取得同意資訊
    Then 回應狀態碼應為 401

  Scenario: 會員同意後核發 Authorization Code 並回到第三方 Client
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 會員勾選範疇 "openid、profile、email" 並按下同意
    Then 回應狀態碼應為 302
    And 跟隨重定向後應重定向至第三方 Client 的 redirect_uri 並帶有 Authorization Code 與原始 state

  Scenario: 會員只同意部分範疇時授權紀錄僅包含被勾選的範疇
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 會員勾選範疇 "openid、profile" 並按下同意
    Then 跟隨重定向後應重定向至第三方 Client 的 redirect_uri 並帶有 Authorization Code 與原始 state
    And 授權紀錄應包含範疇 "profile"
    And 授權紀錄不應包含範疇 "email"

  Scenario: 竄改表單無法取得票證以外的範疇，也無法移除 openid
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 會員勾選範疇 "profile、admin" 並按下同意
    Then 跟隨重定向後應重定向至第三方 Client 的 redirect_uri 並帶有 Authorization Code 與原始 state
    And 授權紀錄應包含範疇 "openid"
    And 授權紀錄應包含範疇 "profile"
    And 授權紀錄不應包含範疇 "admin"

  Scenario: 尚未授權的第三方 Client 帶 prompt=none 時回報 consent_required 而不出現同意頁
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 以 Client "demo-third-party-app" 及合法的 PKCE 參數並帶 prompt=none 發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為該 Client 已登記的 redirect_uri
    And 重定向網址的 error 參數應為 "consent_required"
    And 重定向網址不應帶有 Authorization Code

  Scenario: 已同意過的第三方 Client 再次授權時不再詢問
    Given 會員已登入且已被導向第三方 Client 的同意頁
    And 會員勾選範疇 "openid、profile、email" 並按下同意
    When 再次以 Client "demo-third-party-app" 及合法的 PKCE 參數發起授權請求
    Then 回應狀態碼應為 302
    And 重定向目標應為該 Client 已登記的 redirect_uri
    And 重定向網址應帶有 Authorization Code 與原始 state

  Scenario: 會員拒絕時依 RFC 6749 回傳 access_denied
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 會員按下拒絕
    Then 回應狀態碼應為 302
    And 應重定向至第三方 Client 的 redirect_uri 且 error 為 "access_denied" 並帶有原始 state
    And 重定向網址不應帶有 Authorization Code

  Scenario: 偽造的 consent_id 無法換發 Authorization Code
    Given 會員已登入且持有有效的主網域 Session Cookie
    When 會員以偽造的 consent_id 提交同意
    Then 回應狀態碼應為 400
    And 回應不應包含 Location 標頭

  Scenario: 超過 5 分鐘的 consent_id 無法換發 Authorization Code
    Given 會員已登入且已被導向第三方 Client 的同意頁
    And 時間已經過了 6 分鐘
    When 會員勾選範疇 "openid、profile、email" 並按下同意
    Then 回應狀態碼應為 400
    And 回應不應包含 Location 標頭

  Scenario: consent_id 只能使用一次
    Given 會員已登入且已被導向第三方 Client 的同意頁
    And 會員勾選範疇 "openid、profile、email" 並按下同意
    When 會員勾選範疇 "openid、profile、email" 並按下同意
    Then 回應狀態碼應為 400
    And 回應不應包含 Location 標頭

  Scenario: 其他會員無法使用不屬於自己的 consent_id
    Given 會員已登入且已被導向第三方 Client 的同意頁
    When 另一位會員以該 consent_id 提交同意
    Then 回應狀態碼應為 403
    And 回應不應包含 Location 標頭
