Feature: 管理員角色 claim 與 admin_api 範疇
  身為平台營運
  我希望只有管理後台的 Access Token 帶有會員的平台角色，且角色以換票當下的最新狀態為準
  以便 admin-api 只允許管理員存取，而降級的管理員不會因為舊的授權而保留權限

  Background:
    Given 初始化 Auth Server 測試伺服器
    And 會員已登入且持有有效的主網域 Session Cookie

  Scenario Outline: 管理後台的 Access Token 帶有會員的平台角色
    Given 該會員的角色為 "<角色>"
    And 已以 Client "admin-web" 取得僅含範疇 "openid、admin_api" 的 Access Token
    Then Access Token 的 claim "role" 應為 "<角色>"

    Examples:
      | 角色   |
      | admin  |
      | member |

  Scenario: 換票時採用當下最新的角色，而不是授權當時的角色
    Given 該會員的角色為 "admin"
    And 已以 Client "admin-web" 取得僅含範疇 "openid、admin_api" 的 Authorization Code
    And 該會員的角色為 "member"
    When 以 Client "admin-web" 及正確的 code_verifier 兌換 Token
    Then 回應狀態碼應為 200
    And Access Token 的 claim "role" 應為 "member"

  Scenario: 未要求 admin_api 範疇的 Access Token 不含角色資訊
    Given 該會員的角色為 "admin"
    And 已以 Client "developer-web" 取得僅含範疇 "openid、developer_api" 的 Access Token
    Then Access Token 不應包含 claim "role"

  Scenario: 一般 Client 不得要求 admin_api 範疇
    Given 該會員的角色為 "admin"
    When 以 Client "member-web-spa" 及合法的 PKCE 參數並要求範疇 "openid、admin_api" 發起授權請求
    Then 回應狀態碼應為 400
    And 回應內容應包含錯誤碼 "invalid_request"

  Scenario: 管理後台 Client 取得包含 email 範疇的 Access Token 並查詢 UserInfo 包含 email
    Given 該會員的角色為 "admin"
    And 已以 Client "admin-web" 取得僅含範疇 "openid、profile、email、admin_api" 的 Access Token
    When 以 Bearer Access Token 呼叫 GET /connect/userinfo
    Then 回應狀態碼應為 200
    And 回應的 email 應為會員的 Email
