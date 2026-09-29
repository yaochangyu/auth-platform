Feature: 管理後台存取控制
  身為平台營運
  我希望只有管理員能使用管理端點
  以便一般會員與第三方應用程式即使拿到有效的 Access Token 也無法審核應用程式或查看稽核紀錄

  Background:
    Given 初始化 Admin API 測試伺服器

  Scenario Outline: 只有具備 admin_api 範疇且角色為 admin 的 Access Token 才能存取
    When 以 <Token 情況> 查詢全平台應用程式
    Then 回應狀態碼應為 <狀態碼>
    And 回應應為 Problem Details

    Examples:
      | Token 情況                                        | 狀態碼 |
      | 未帶 Access Token                                 | 401    |
      | 偽造的 Access Token                               | 401    |
      | 已過期的 Access Token                             | 401    |
      | 使用未知金鑰簽署的 Access Token                   | 401    |
      | 一般會員（role 為 member）的 Access Token         | 403    |
      | 沒有 role 的 Access Token                         | 403    |
      | 有 admin 角色但缺少 admin_api 範疇的 Access Token | 403    |
      | sub 不是有效會員識別碼的 Access Token             | 403    |

  Scenario: 管理員可以存取
    When 管理員 "Root" 查詢全平台應用程式，狀態篩選 ""
    Then 回應狀態碼應為 200

  Scenario Outline: 一般會員無法使用任何管理端點
    When 一般會員 "Mallory" 嘗試<動作>
    Then 回應狀態碼應為 403

    Examples:
      | 動作         |
      | 查詢應用程式 |
      | 查看應用程式詳情 |
      | 停用應用程式 |
      | 查詢稽核紀錄 |
