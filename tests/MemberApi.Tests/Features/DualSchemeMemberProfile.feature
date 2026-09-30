Feature: 會員個人檔案雙軌鑑權 (Cookie 與 Bearer JWT)
  身為會員、原生 App 或授權客戶端
  我希望能透過瀏覽器會話 Cookie 或持有的 Bearer Access Token 呼叫取得個人檔案端點
  以便在不同端點環境（Web 或 App/API）下安全取得個人檔案資料

  Background:
    Given 初始化測試伺服器與授權中心 JWKS 金鑰環境

  Scenario: 第一方 Web 攜帶有效 Session Cookie 成功取得個人檔案 (既有相容性保證)
    Given 資料庫中存在一名已啟用之會員
    And 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 呼叫取得個人檔案 API
    Then 回應狀態碼應為 200
    And 回應內容應包含會員的 Email、暱稱與狀態

  Scenario: 行動端或授權客戶端攜帶合法 Bearer Token 成功取得個人檔案
    Given 資料庫中存在一名已啟用之會員
    And 授權中心已為該會員簽發包含 "profile" 範疇與合法 "sub" 之 RS256 Bearer Token
    When 客戶端於 Authorization 標頭攜帶該 Bearer Token 呼叫取得個人檔案 API
    Then 回應狀態碼應為 200
    And 回應內容應包含該會員的 Email、暱稱與狀態

  Scenario: 攜帶缺少 profile 範疇的 Bearer Token 存取時應被拒絕
    Given 資料庫中存在一名已啟用之會員
    And 授權中心已為該會員簽發僅包含 "openid" 但無 "profile" 範疇之 Bearer Token
    When 客戶端於 Authorization 標頭攜帶該 Bearer Token 呼叫取得個人檔案 API
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "權限不足"

  Scenario: 攜帶過期或無效簽章的 Bearer Token 存取時應回應未授權
    When 客戶端於 Authorization 標頭攜帶偽造簽章或已過期之 Bearer Token 呼叫取得個人檔案 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "未授權"

  Scenario: 同時未提供 Cookie 與 Bearer Token 時應回應未授權
    When 客戶端未攜帶任何憑據呼叫取得個人檔案 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: Bearer Token 中的 sub 非有效會員識別碼時應回應未授權或找不到
    Given 授權中心簽發了一組 sub 為不存在之會員 GUID 的合法 Bearer Token
    When 客戶端攜帶該 Token 呼叫取得個人檔案 API
    Then 回應狀態碼應為 404
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
