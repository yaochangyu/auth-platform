Feature: 第三方授權應用程式清單與權限撤銷
  身為已登入的會員
  我希望能檢視已授權存取我資料的第三方應用程式清單並隨時撤銷授權
  以便掌控我的個人資料存取範圍

  Background:
    Given 初始化測試伺服器

  Scenario: 已登入會員檢視已連結應用程式清單成功
    Given 使用者已成功登入並取得有效的 Session Cookie
    And 該會員已授權應用程式「履歷快易通」
    When 使用者攜帶該 Session Cookie 呼叫取得已連結應用程式清單 API
    Then 回應狀態碼應為 200
    And 回應內容應包含一筆應用程式「履歷快易通」的授權紀錄
    And 回應內容的 totalCount 應為 1

  Scenario: 尚未授權任何應用程式時清單為空
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 呼叫取得已連結應用程式清單 API
    Then 回應狀態碼應為 200
    And 回應內容的 totalCount 應為 0

  Scenario: 未登入時查詢已連結應用程式清單失敗
    When 使用者未攜帶 Session Cookie 呼叫取得已連結應用程式清單 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 撤銷已連結應用程式授權成功
    Given 使用者已成功登入並取得有效的 Session Cookie
    And 該會員已授權應用程式「履歷快易通」
    When 使用者攜帶該 Session Cookie 撤銷該應用程式的授權
    Then 回應狀態碼應為 204
    And 再次查詢已連結應用程式清單應不再包含「履歷快易通」
    And 該筆授權紀錄應被標記為已撤銷

  Scenario: 未登入時撤銷已連結應用程式授權失敗
    When 使用者未攜帶 Session Cookie 撤銷編號為隨機 UUID 的應用程式授權
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 撤銷不存在的應用程式授權失敗
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 撤銷編號為不存在的應用程式授權
    Then 回應狀態碼應為 404
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 撤銷非本人的應用程式授權失敗
    Given 使用者已成功登入並取得有效的 Session Cookie
    And 另一位會員已授權應用程式「其他人的應用程式」
    When 使用者攜帶該 Session Cookie 撤銷「其他人的應用程式」的授權
    Then 回應狀態碼應為 404
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
