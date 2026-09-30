Feature: 會員目錄快照
  身為 Auth Server
  我希望以單一查詢取得會員換票與 UserInfo 所需的資料
  以便兩處共用同一個讀取 Seam

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: 取得完整的會員快照
    Given 資料庫中有一位已驗證 Email 的管理員會員
    When 以會員目錄查詢該會員
    Then 快照應包含該會員的 Security Stamp、角色、Email、顯示名稱與驗證狀態
    And 快照的更新時間應為 Email 驗證時間

  Scenario: 尚未驗證 Email 的會員以建立時間作為更新時間
    Given 資料庫中有一位尚未驗證 Email 的一般會員
    When 以會員目錄查詢該會員
    Then 快照的 EmailVerified 應為 false
    And 快照的更新時間應為建立時間

  Scenario: 會員編輯過個人檔案時以編輯時間作為更新時間
    Given 資料庫中有一位已驗證 Email 且之後編輯過個人檔案的會員
    When 以會員目錄查詢該會員
    Then 快照的更新時間應為個人檔案編輯時間

  Scenario: 查詢不存在的會員
    When 以會員目錄查詢一位不存在的會員
    Then 應回傳空值
