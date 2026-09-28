Feature: 健康檢查
  身為維運人員
  我希望能呼叫健康檢查端點
  以便確認會員中心 API 服務與其相依的 PostgreSQL 資料庫是否正常運作

  Scenario: 服務與資料庫皆正常運作時回傳健康狀態
    Given PostgreSQL 測試容器已啟動且可連線
    When 使用者呼叫 GET /health 端點
    Then 回應狀態碼應為 200
    And 回應內容的 status 欄位應為 "Healthy"
    And 回應內容應包含 timestamp 與 version 欄位
    And 回應內容的 checks 應包含名稱為 "PostgreSQL" 且 status 為 "Healthy" 的檢測項目
