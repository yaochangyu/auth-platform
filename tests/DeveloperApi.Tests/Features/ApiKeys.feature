Feature: API Key 發行與 HMAC 請求簽章驗證（Server-to-Server）
  身為需要讓後端服務呼叫平台的開發者
  我希望發行綁定範疇與到期時間的 API Key，並以 HMAC 簽章呼叫
  以便在沒有固定 IP 的雲端環境中防止憑證外洩後被冒用、請求被竄改或重放

  Background:
    Given 初始化 Developer API 測試伺服器
    And 開發者 "Alice" 已建立專案 "Alice-1"

  Scenario: 發行正式環境 API Key，金鑰與簽章密鑰只顯示一次且資料庫只存雜湊
    When 開發者 "Alice" 為專案 "Alice-1" 建立 API Key，名稱 "後端服務"、環境 "Live"、範疇 "profile、email"、到期 ""
    Then 回應狀態碼應為 201
    And 回應的 API Key 應以 "ak_live_" 開頭
    And 回應的 API Secret 應以 "as_" 開頭
    And 回應應禁止快取
    And 資料庫中的 API Key 只存 SHA-256 雜湊而不含 API Key 與 API Secret 明文
    When 開發者 "Alice" 取得專案 "Alice-1" 的 API Key 清單
    Then 回應狀態碼應為 200
    And API Key 清單應有 1 把，且只顯示前綴而不含金鑰與密鑰明文

  Scenario: 發行測試環境 API Key 使用 ak_test_ 前綴
    When 開發者 "Alice" 為專案 "Alice-1" 建立 API Key，名稱 "測試機"、環境 "Test"、範疇 "profile"、到期 ""
    Then 回應狀態碼應為 201
    And 回應的 API Key 應以 "ak_test_" 開頭

  Scenario Outline: API Key 欄位不合法時拒絕建立
    When 開發者 "Alice" 為專案 "Alice-1" 建立 API Key，名稱 "<名稱>"、環境 "<環境>"、範疇 "<範疇>"、到期 "<到期>"
    Then 回應狀態碼應為 400
    And 回應的錯誤應指出欄位 "<欄位>"

    Examples:
      | 名稱     | 環境 | 範疇          | 到期    | 欄位        |
      |          | Live | profile       |         | name        |
      | 後端服務 | Live |               |         | scopes      |
      | 後端服務 | Live | developer_api |         | scopes      |
      | 後端服務 | Live | openid        |         | scopes      |
      | 後端服務 | Live | profile       | -1 天後 | expiresAt   |

  Scenario Outline: 開發者無法存取他人專案的 API Key
    When 開發者 "Bob" 嘗試對專案 "Alice-1" 的 API Key <動作>
    Then 回應狀態碼應為 404

    Examples:
      | 動作     |
      | 建立金鑰 |
      | 列出清單 |
      | 撤銷金鑰 |

  Scenario: 以正確簽章呼叫 M2M 連線檢查端點
    Given 開發者 "Alice" 已為專案 "Alice-1" 建立 API Key，範疇 "profile、email"，到期 ""
    When 以第 1 把 API Key 簽章呼叫 M2M 連線檢查端點
    Then 回應狀態碼應為 200
    And M2M 回應的 clientId 應為專案 "Alice-1" 的 ClientId
    And M2M 回應的範疇應為 "profile、email"

  Scenario: 時間戳記在容許範圍內仍可通過
    Given 開發者 "Alice" 已為專案 "Alice-1" 建立 API Key，範疇 "profile"，到期 ""
    When 以第 1 把 API Key 簽章呼叫 M2M 連線檢查端點，時間戳記為 4 分鐘前
    Then 回應狀態碼應為 200

  Scenario Outline: 簽章、時間戳記或金鑰不合法時拒絕
    Given 開發者 "Alice" 已為專案 "Alice-1" 建立 API Key，範疇 "profile"，到期 ""
    When 以 <情況> 呼叫 M2M 連線檢查端點
    Then 回應狀態碼應為 401
    And 回應應為 Problem Details

    Examples:
      | 情況                                 |
      | 簽章後被篡改 Body 的請求             |
      | 簽章後被更動查詢字串的請求           |
      | 時間戳記為 6 分鐘前的請求            |
      | 時間戳記為 6 分鐘後的請求            |
      | 以錯誤的 API Secret 簽章的請求       |
      | 使用不存在的 API Key 的請求          |
      | 缺少簽章標頭的請求                   |
      | 簽章格式不是 16 進位的請求           |
      | 簽章後 Body 超過 1 MB 的請求         |
      | 時間戳記為極端數值的請求             |
      | API Secret 無法解密的 API Key 請求   |

  Scenario: 已過期的 API Key 無法通過驗證
    Given 開發者 "Alice" 已為專案 "Alice-1" 建立 API Key，範疇 "profile"，到期 "1 天後"
    And 時間已經過了 48 小時
    When 以第 1 把 API Key 簽章呼叫 M2M 連線檢查端點
    Then 回應狀態碼應為 401

  Scenario: 已撤銷的 API Key 立即無法通過驗證
    Given 開發者 "Alice" 已為專案 "Alice-1" 建立 API Key，範疇 "profile"，到期 ""
    When 開發者 "Alice" 撤銷專案 "Alice-1" 的第 1 把 API Key
    Then 回應狀態碼應為 204
    When 以第 1 把 API Key 簽章呼叫 M2M 連線檢查端點
    Then 回應狀態碼應為 401

  Scenario: API Key 缺少所需範疇時回傳 403
    Given 開發者 "Alice" 已為專案 "Alice-1" 建立 API Key，範疇 "email"，到期 ""
    When 以第 1 把 API Key 簽章呼叫 M2M 連線檢查端點
    Then 回應狀態碼應為 403
