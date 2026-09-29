Feature: Client Secret 雙金鑰平滑輪替與作廢
  身為以 Client Secret 換票的 Confidential Client 開發者
  我希望輪替 Secret 時新舊兩組在過渡期內都能正常換票，且到期或作廢後立即失效
  以便線上服務在更新 Secret 期間不中斷，遇到洩漏時也能立刻阻斷舊金鑰

  Background:
    Given 初始化 Auth Server 測試伺服器
    And 會員已登入且持有有效的主網域 Session Cookie

  Scenario Outline: 輪替過渡期內新舊 Secret 都可以換票
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 已輪替發行第二組 Secret，第一組 Secret 進入 24 小時過渡期
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用<Secret> Secret 兌換 Token
    Then 回應狀態碼應為 200
    And 回應應包含 Refresh Token

    Examples:
      | Secret |
      | 第一組 |
      | 第二組 |

  Scenario: 過渡期結束後第一組 Secret 失效，第二組仍可換票
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 已輪替發行第二組 Secret，第一組 Secret 進入 24 小時過渡期
    And 時間已經過了 25 小時
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用第一組 Secret 兌換 Token
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

  Scenario: 過渡期結束後第二組 Secret 仍可換票
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 已輪替發行第二組 Secret，第一組 Secret 進入 24 小時過渡期
    And 時間已經過了 25 小時
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用第二組 Secret 兌換 Token
    Then 回應狀態碼應為 200

  Scenario: 過渡期尚未結束前手動作廢第一組 Secret 後立即失效
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 已輪替發行第二組 Secret，第一組 Secret 進入 24 小時過渡期
    And 第一組 Secret 已被手動作廢
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用第一組 Secret 兌換 Token
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

  Scenario: 作廢第一組 Secret 不影響第二組
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 已輪替發行第二組 Secret，第一組 Secret 進入 24 小時過渡期
    And 第一組 Secret 已被手動作廢
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用第二組 Secret 兌換 Token
    Then 回應狀態碼應為 200

  Scenario: 所有 Secret 都被作廢後無法換票
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 第一組 Secret 已被手動作廢
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用第一組 Secret 兌換 Token
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

  Scenario: 使用不存在的 Secret 無法換票
    Given Confidential Client "rotation-test-app" 已發行第一組 Secret
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用未發行過的 Secret 兌換 Token
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

  Scenario: Secret 集合為空時，OpenIddict 佔位用的 client_secret 也無法換票
    Given Confidential Client "rotation-test-app" 尚未發行任何 Secret
    And 已以 Client "rotation-test-app" 取得 Authorization Code
    When 以 Client "rotation-test-app" 使用佔位 Secret 兌換 Token
    Then 回應狀態碼應為 401
    And 回應的 error 欄位應為 "invalid_client"

