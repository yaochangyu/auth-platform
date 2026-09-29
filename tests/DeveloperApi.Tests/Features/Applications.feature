Feature: 應用專案管理與 Application Ownership 資料隔離
  身為以統一會員帳號登入的開發者
  我希望建立並管理自己的應用專案
  以便串接統一身分與授權平台，且他人無法檢視或修改我的專案

  Background:
    Given 初始化 Developer API 測試伺服器

  Scenario: 開發者建立應用專案
    When 開發者 "Alice" 建立應用專案，名稱 "我的應用"、簡介 "測試用"、聯絡 Email "dev@example.com"
    Then 回應狀態碼應為 201
    And 回應的專案名稱應為 "我的應用"
    And 回應的專案應有系統產生的 clientId
    And 回應的專案狀態應為 "Active"
    And 開發者 "Alice" 的專案清單應為 "我的應用"

  Scenario: 建立應用專案時可附上 Logo 與官網
    When 開發者 "Alice" 建立應用專案，名稱 "我的應用"、簡介 "測試用"、聯絡 Email "dev@example.com"，Logo "https://cdn.example.com/logo.png"、官網 "https://app.example.com"
    Then 回應狀態碼應為 201
    And 回應的專案 Logo 應為 "https://cdn.example.com/logo.png"
    And 回應的專案官網應為 "https://app.example.com"

  Scenario Outline: 應用專案欄位驗證失敗時拒絕建立
    When 開發者 "Alice" 建立應用專案，名稱 "<名稱>"、簡介 "<簡介>"、聯絡 Email "<Email>"，Logo "<Logo>"、官網 ""
    Then 回應狀態碼應為 400
    And 回應的錯誤應指出欄位 "<欄位>"
    And 開發者 "Alice" 的專案清單應為 ""

    Examples:
      | 名稱 | 簡介 | Email           | Logo                | 欄位         |
      |      | 簡介 | dev@example.com |                     | name         |
      | 名稱 |      | dev@example.com |                     | description  |
      | 名稱 | 簡介 | not-an-email    |                     | contactEmail |
      | 名稱 | 簡介 | dev@example.com | javascript:alert(1) | logoUrl      |

  Scenario: 專案清單只包含自己建立的專案
    Given 開發者 "Alice" 已建立專案 "Alice-1"
    And 開發者 "Alice" 已建立專案 "Alice-2"
    And 開發者 "Bob" 已建立專案 "Bob-1"
    Then 開發者 "Alice" 的專案清單應為 "Alice-2、Alice-1"
    And 開發者 "Bob" 的專案清單應為 "Bob-1"

  Scenario: 開發者可檢視並更新自己的專案
    Given 開發者 "Alice" 已建立專案 "Alice-1"
    When 開發者 "Alice" 將專案 "Alice-1" 更新為名稱 "Alice 改名"
    Then 回應狀態碼應為 200
    And 回應的專案名稱應為 "Alice 改名"
    And 開發者 "Alice" 檢視專案 "Alice-1" 的名稱應為 "Alice 改名"

  Scenario: 開發者無法檢視他人的專案
    Given 開發者 "Alice" 已建立專案 "Alice-1"
    When 開發者 "Bob" 檢視專案 "Alice-1"
    Then 回應狀態碼應為 404
    And 回應應為 Problem Details

  Scenario: 開發者無法修改他人的專案
    Given 開發者 "Alice" 已建立專案 "Alice-1"
    When 開發者 "Bob" 將專案 "Alice-1" 更新為名稱 "被入侵"
    Then 回應狀態碼應為 404
    And 開發者 "Alice" 檢視專案 "Alice-1" 的名稱應為 "Alice-1"

  Scenario Outline: 無效的 Access Token 無法存取專案清單
    When 以 <Token 情況> 取得專案清單
    Then 回應狀態碼應為 <狀態碼>
    And 回應應為 Problem Details

    Examples:
      | Token 情況                          | 狀態碼 |
      | 未帶 Access Token                   | 401    |
      | 偽造的 Access Token                 | 401    |
      | 已過期的 Access Token               | 401    |
      | 使用未知金鑰簽署的 Access Token     | 401    |
      | 缺少 developer_api 範疇的 Access Token | 403 |
      | sub 不是有效會員識別碼的 Access Token | 403 |
