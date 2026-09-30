Feature: 個人檔案增量補填與欄位可變性策略防護
  身為會員與授權應用客戶端
  我希望能在後續生命週期中逐步增量補填最高學歷、通訊地址、職稱與生日
  並且生日受 Write-Once 原則保護，防止重複領取生日禮或竄改年齡

  Background:
    Given 初始化測試伺服器與授權中心 JWKS 金鑰環境
    And 資料庫中存在一名尚未填寫生日與擴充屬性之已啟用會員

  Scenario: 首次增量補填生日與自由流動欄位成功
    Given 授權中心已為該會員簽發包含 "profile" 範疇與合法 "sub" 之 RS256 Bearer Token
    When 客戶端攜帶該 Token 呼叫補填個人檔案 API，填寫生日 "1990-05-20"、學歷 "大學"、地址 "台北市"、職稱 "工程師"
    Then 回應狀態碼應為 200
    And 回應內容應包含更新後的生日 "1990-05-20"、學歷 "大學"、地址 "台北市"、職稱 "工程師"

  Scenario: 自由流動欄位可隨時再次更新覆寫成功
    Given 該會員已填寫學歷為 "大學" 與職稱 "工程師"
    And 授權中心已為該會員簽發包含 "profile" 範疇與合法 "sub" 之 RS256 Bearer Token
    When 客戶端攜帶該 Token 呼叫補填個人檔案 API，更新學歷為 "碩士"、職稱 "資深工程師"
    Then 回應狀態碼應為 200
    And 回應內容應包含更新後的學歷 "碩士" 與職稱 "資深工程師"

  Scenario: 生日已存在時嘗試覆寫不同生日應觸發 Write-Once 防護回傳 409
    Given 該會員資料庫中生日已存在且為 "1990-05-20"
    And 授權中心已為該會員簽發包含 "profile" 範疇與合法 "sub" 之 RS256 Bearer Token
    When 客戶端攜帶該 Token 呼叫補填個人檔案 API，嘗試覆寫生日為 "1995-10-10"
    Then 回應狀態碼應為 409
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "不允許"

  Scenario: 第一方瀏覽器以有效 Session Cookie 呼叫補填個人檔案成功
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 呼叫補填個人檔案 API，填寫最高學歷為 "博士"
    Then 回應狀態碼應為 200
    And 回應內容應包含更新後的學歷 "博士"

  Scenario: 攜帶缺少 profile 範疇的 Bearer Token 補填個人檔案時應被拒絕
    Given 授權中心已為該會員簽發僅包含 "openid" 但無 "profile" 範疇之 Bearer Token
    When 客戶端攜帶該 Token 呼叫補填個人檔案 API，填寫最高學歷為 "學士"
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤，標題包含 "權限不足"

  Scenario: 攜帶包含 profile:write 範疇之 Bearer Token 補填個人檔案成功
    Given 授權中心已為該會員簽發包含 "profile:write" 範疇與合法 "sub" 之 RS256 Bearer Token
    When 客戶端攜帶該 Token 呼叫補填個人檔案 API，填寫最高學歷為 "雙學士"
    Then 回應狀態碼應為 200
    And 回應內容應包含更新後的學歷 "雙學士"
