Feature: 全平台應用程式審核與緊急斷路器
  身為平台管理員
  我希望檢視所有應用程式並在發現濫用或外洩時一鍵停用
  以便立即阻斷該應用程式的所有存取，且不影響其他應用程式

  Background:
    Given 初始化 Admin API 測試伺服器

  Scenario: 管理員檢視所有開發者建立的應用程式並依狀態篩選
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    And 平台上有應用專案 "B1"，擁有者 "Bob"，狀態 "PendingReview"
    When 管理員 "Root" 查詢全平台應用程式，狀態篩選 ""
    Then 回應狀態碼應為 200
    And 清單應包含專案 "A1、B1"
    When 管理員 "Root" 查詢全平台應用程式，狀態篩選 "PendingReview"
    Then 清單應包含專案 "B1" 且不包含專案 "A1"

  Scenario: 管理員檢視單一應用程式詳情
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    When 管理員 "Root" 查看專案 "A1" 的詳情
    Then 回應狀態碼應為 200
    And 詳情的擁有者應為 "Alice"

  Scenario: 查看不存在的應用程式回傳 404
    When 管理員 "Root" 查看不存在的應用程式
    Then 回應狀態碼應為 404

  Scenario: 審核通過待審核的應用程式
    Given 平台上有應用專案 "B1"，擁有者 "Bob"，狀態 "PendingReview"
    When 管理員 "Root" 將專案 "B1" 的狀態改為 "Active"，原因 ""
    Then 回應狀態碼應為 200
    And 回應的專案狀態應為 "Active"

  Scenario Outline: 狀態變更的欄位不合法時拒絕
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "<狀態>"，原因 "<原因>"
    Then 回應狀態碼應為 400
    And 回應的錯誤應指出欄位 "<欄位>"

    Examples:
      | 狀態          | 原因 | 欄位   |
      | Suspended     |      | reason |
      | PendingReview | 重審 | status |

  Scenario: 狀態沒有改變時回傳 409
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Active"，原因 ""
    Then 回應狀態碼應為 409

  Scenario: 停用應用程式時斷路器即時作廢所有 API Key、授權與 Token
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    And 專案 "A1" 有 2 把有效的 API Key
    And 專案 "A1" 已建立 OAuth Client，並有 3 筆授權與 4 個 Token
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Suspended"，原因 "涉嫌濫用"
    Then 回應狀態碼應為 200
    And 回應的專案狀態應為 "Suspended"
    And 斷路器回報作廢 2 把 API Key、3 筆授權、4 個 Token
    And 專案 "A1" 的所有 API Key 都已撤銷
    And 專案 "A1" 的所有授權與 Token 都已撤銷
    And 專案 "A1" 的 OAuth Client 已被標記為停用

  Scenario: 停用只影響該專案，不影響其他專案
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    And 平台上有應用專案 "B1"，擁有者 "Bob"，狀態 "Active"
    And 專案 "A1" 有 1 把有效的 API Key
    And 專案 "B1" 有 1 把有效的 API Key
    And 專案 "B1" 已建立 OAuth Client，並有 1 筆授權與 1 個 Token
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Suspended"，原因 "涉嫌濫用"
    Then 專案 "B1" 的 API Key、授權與 Token 都仍然有效
    And 專案 "B1" 的 OAuth Client 未被標記為停用

  Scenario: 尚未設定 OAuth Client 的應用程式也能停用
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    And 專案 "A1" 有 1 把有效的 API Key
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Suspended"，原因 "涉嫌濫用"
    Then 回應狀態碼應為 200
    And 斷路器回報作廢 1 把 API Key、0 筆授權、0 個 Token
    And 專案 "A1" 的所有 API Key 都已撤銷

  Scenario: 取消停用只解除 Client 的停用標記，已作廢的資料不會復原
    Given 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"
    And 專案 "A1" 有 1 把有效的 API Key
    And 專案 "A1" 已建立 OAuth Client，並有 1 筆授權與 1 個 Token
    And 管理員 "Root" 已將專案 "A1" 停用
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Active"，原因 "誤判，已確認安全"
    Then 回應狀態碼應為 200
    And 專案 "A1" 的 OAuth Client 未被標記為停用
    And 專案 "A1" 的所有 API Key 都已撤銷
    And 專案 "A1" 的所有授權與 Token 都已撤銷
