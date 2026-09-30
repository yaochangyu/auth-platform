Feature: 客戶端信任分級與高敏感帳號治理端點隔離防護
  身為系統架構與資安工程師
  我希望會員中心之修改密碼與已連結應用程式管理等高敏感端點僅限第一方瀏覽器會話存取
  以便防止第三方或未受信任客戶端持 Bearer Token 越權操作高敏感帳號治理功能

  Background:
    Given 初始化測試伺服器與授權中心 JWKS 金鑰環境
    And 資料庫中存在一名已啟用之會員
    And 授權中心已為該會員簽發包含 "profile" 範疇與合法 "sub" 之 RS256 Bearer Token

  Scenario: 攜帶有效 Bearer Token 嘗試修改密碼應被阻擋並回應 403
    When 客戶端於 Authorization 標頭攜帶該 Bearer Token 嘗試呼叫修改密碼 API
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "權限不足"

  Scenario: 攜帶有效 Bearer Token 嘗試查詢已連結應用程式清單應被阻擋並回應 403
    When 客戶端於 Authorization 標頭攜帶該 Bearer Token 嘗試呼叫查詢已連結應用程式清單 API
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "權限不足"

  Scenario: 攜帶有效 Bearer Token 嘗試撤銷已連結應用程式應被阻擋並回應 403
    When 客戶端於 Authorization 標頭攜帶該 Bearer Token 嘗試呼叫撤銷已連結應用程式 API
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "權限不足"

  Scenario: 第一方瀏覽器以有效 Session Cookie 查詢已連結應用程式清單正常存取
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 呼叫取得已連結應用程式清單 API
    Then 回應狀態碼應為 200
