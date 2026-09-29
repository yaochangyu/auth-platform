Feature: 不可篡改的稽核紀錄
  身為平台管理員
  我希望每一次審核與斷路都留下無法被修改的紀錄
  以便事後追溯是誰、何時、從哪裡、對什麼做了什麼變更

  Background:
    Given 初始化 Admin API 測試伺服器
    And 平台上有應用專案 "A1"，擁有者 "Alice"，狀態 "Active"

  Scenario: 停用應用程式會寫入稽核紀錄，記下操作人、IP、動作、目標與前後內容
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Suspended"，原因 "涉嫌濫用"
    And 管理員 "Root" 查詢專案 "A1" 的稽核紀錄
    Then 回應狀態碼應為 200
    And 稽核紀錄應有 1 筆
    And 最新一筆稽核紀錄的操作人應為管理員 "Root"
    And 最新一筆稽核紀錄的動作應為 "application.suspend"，目標為該專案
    And 最新一筆稽核紀錄的客戶端 IP 應為 "203.0.113.9"
    And 最新一筆稽核紀錄的變更前後狀態應為 "Active" 與 "Suspended"
    And 最新一筆稽核紀錄的補充資訊應包含原因 "涉嫌濫用"

  Scenario: 經由受信任的反向代理操作時，稽核紀錄記錄真正的客戶端 IP
    When 管理員 "Root" 經由反向代理將專案 "A1" 的狀態改為 "Suspended"，原因 "涉嫌濫用"，客戶端 IP 為 "198.51.100.7"
    And 管理員 "Root" 查詢專案 "A1" 的稽核紀錄
    Then 最新一筆稽核紀錄的客戶端 IP 應為 "198.51.100.7"

  Scenario: 取消停用會寫入另一筆稽核紀錄
    Given 管理員 "Root" 已將專案 "A1" 停用
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "Active"，原因 "誤判，已確認安全"
    And 管理員 "Root" 查詢專案 "A1" 的稽核紀錄
    Then 稽核紀錄應有 2 筆
    And 最新一筆稽核紀錄的動作應為 "application.activate"，目標為該專案
    And 最新一筆稽核紀錄的變更前後狀態應為 "Suspended" 與 "Active"

  Scenario Outline: 失敗的操作不會寫入稽核紀錄
    When 管理員 "Root" 將專案 "A1" 的狀態改為 "<狀態>"，原因 "<原因>"
    And 管理員 "Root" 查詢專案 "A1" 的稽核紀錄
    Then 稽核紀錄應有 0 筆

    Examples:
      | 狀態      | 原因 |
      | Suspended |      |
      | Active    |      |

  Scenario: 依動作篩選並分頁查詢稽核紀錄
    Given 管理員 "Root" 已將專案 "A1" 停用
    And 管理員 "Root" 已將專案 "A1" 取消停用
    And 管理員 "Root" 已將專案 "A1" 停用
    When 管理員 "Root" 查詢專案 "A1" 的稽核紀錄，動作 "application.suspend"
    Then 稽核紀錄應有 2 筆
    When 管理員 "Root" 查詢專案 "A1" 的稽核紀錄，每頁 1 筆、第 2 頁
    Then 回應的總筆數應為 3
    And 目前頁面應有 1 筆稽核紀錄

  Scenario: 依操作人篩選稽核紀錄
    Given 管理員 "Root" 已將專案 "A1" 停用
    When 管理員 "Root" 查詢操作人為 "Root" 的稽核紀錄
    Then 所有稽核紀錄的操作人都應為管理員 "Root"
    When 管理員 "Root" 查詢操作人為 "Someone" 的稽核紀錄
    Then 目前頁面應有 0 筆稽核紀錄

  Scenario Outline: 稽核紀錄無法被修改、刪除或清空，連直接操作資料庫也不行
    Given 管理員 "Root" 已將專案 "A1" 停用
    When 直接對資料庫執行 <操作>
    Then 資料庫應拒絕該操作
    When 管理員 "Root" 查詢專案 "A1" 的稽核紀錄
    Then 稽核紀錄應有 1 筆

    Examples:
      | 操作     |
      | UPDATE   |
      | DELETE   |
      | TRUNCATE |
