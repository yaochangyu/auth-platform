Feature: OAuth Client 設定與 Client Secret 雙金鑰平滑輪替
  身為開發者
  我希望設定自己應用專案的 OAuth Client（類型、網址白名單、範疇），並安全地發行與輪替 Client Secret
  以便串接統一登入，且更換 Secret 期間線上服務不中斷

  Background:
    Given 初始化 Developer API 測試伺服器
    And 開發者 "Alice" 已建立專案 "Alice-1"

  Scenario: 尚未設定時回傳預設的 OAuth 設定
    When 開發者 "Alice" 取得專案 "Alice-1" 的 OAuth 設定
    Then 回應狀態碼應為 200
    And 回應的客戶端類型應為 "Public"
    And 回應的 clientId 應與專案 "Alice-1" 相同
    And 回應的 Secret 清單應有 0 組

  Scenario: 設定 Public Client 的網址白名單與範疇並同步到 Auth Server
    When 開發者 "Alice" 設定專案 "Alice-1" 的 OAuth Client：類型 "Public"、Redirect URIs "https://app.example.com/callback、http://localhost:3000/callback"、Post Logout URIs "https://app.example.com/signed-out"、範疇 "openid、profile"
    Then 回應狀態碼應為 200
    And 回應的 Redirect URIs 應為 "https://app.example.com/callback、http://localhost:3000/callback"
    And 回應的 Post Logout URIs 應為 "https://app.example.com/signed-out"
    And 回應的範疇應為 "openid、profile"
    And Auth Server 中專案 "Alice-1" 的 Client 應為 Public、需要會員同意、強制 PKCE，範疇為 "openid、profile"
    And Auth Server 中專案 "Alice-1" 的 Client 顯示名稱應為 "Alice-1"

  Scenario Outline: 網址或範疇不合法時拒絕設定
    When 開發者 "Alice" 設定專案 "Alice-1" 的 OAuth Client：類型 "Public"、Redirect URIs "<Redirect URIs>"、Post Logout URIs ""、範疇 "<範疇>"
    Then 回應狀態碼應為 400
    And 回應的錯誤應指出欄位 "<欄位>"

    Examples:
      | Redirect URIs                                       | 範疇          | 欄位         |
      | http://evil.example.com/callback                    | openid        | redirectUris |
      | https://app.example.com/callback#fragment           | openid        | redirectUris |
      | https://app.example.com/callback#                   | openid        | redirectUris |
      | https://user:pass@app.example.com/callback          | openid        | redirectUris |
      | /relative/path                                      | openid        | redirectUris |
      | javascript:alert(1)                                 | openid        | redirectUris |
      | https://*.example.com/callback                      | openid        | redirectUris |
      | https://a.example.com/cb、https://a.example.com/cb  | openid        | redirectUris |
      | https://app.example.com/callback                    | developer_api | scopes       |
      | https://app.example.com/callback                    | unknown       | scopes       |
      | https://app.example.com/callback                    |               | scopes       |

  Scenario Outline: 開發者無法存取他人專案的 OAuth 設定與 Secret
    When 開發者 "Bob" 對專案 "Alice-1" 執行 <動作>
    Then 回應狀態碼應為 404

    Examples:
      | 動作                |
      | 取得 OAuth 設定     |
      | 更新 OAuth 設定     |
      | 產生 Client Secret  |
      | 作廢 Client Secret  |

  Scenario Outline: 只有 Confidential Client 同步後具備 Client Credentials 授權方式
    When 開發者 "Alice" 設定專案 "Alice-1" 的 OAuth Client：類型 "<類型>"、Redirect URIs "https://app.example.com/callback"、Post Logout URIs ""、範疇 "openid、profile"
    Then Auth Server 中專案 "Alice-1" 的 Client 是否允許 Client Credentials 應為 "<允許>"

    Examples:
      | 類型         | 允許 |
      | Public       | 否   |
      | Confidential | 是   |

  Scenario: Public Client 不能發行 Client Secret
    When 開發者 "Alice" 為專案 "Alice-1" 產生 Client Secret
    Then 回應狀態碼應為 409

  Scenario: Confidential Client 發行 Secret 時明文只顯示一次且資料庫只存 SHA-256 雜湊
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    When 開發者 "Alice" 為專案 "Alice-1" 產生 Client Secret
    Then 回應狀態碼應為 201
    And 回應的 Secret 明文應以 "cs_" 開頭
    And 回應應禁止快取
    And 資料庫中專案 "Alice-1" 的 Secret 只存 SHA-256 雜湊而不含明文
    When 開發者 "Alice" 取得專案 "Alice-1" 的 OAuth 設定
    Then 回應的 Secret 清單應有 1 組
    And 回應內容不應含有先前發行的 Secret 明文

  Scenario: 輪替發行第二組 Secret 時第一組進入 24 小時過渡期
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    When 開發者 "Alice" 為專案 "Alice-1" 產生 Client Secret
    Then 回應狀態碼應為 201
    When 開發者 "Alice" 取得專案 "Alice-1" 的 OAuth 設定
    Then 回應的 Secret 清單應有 2 組
    And 第 1 組 Secret 的狀態應為 "Expiring"，並於 24 小時後到期
    And 第 2 組 Secret 的狀態應為 "Active"

  Scenario: 同時有效的 Secret 已達兩組時不能再發行
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    When 開發者 "Alice" 為專案 "Alice-1" 產生 Client Secret
    Then 回應狀態碼應為 409

  Scenario: 過渡期結束後舊 Secret 到期，可再發行新的 Secret
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    And 時間已經過了 25 小時
    When 開發者 "Alice" 取得專案 "Alice-1" 的 OAuth 設定
    Then 第 1 組 Secret 的狀態應為 "Expired"
    When 開發者 "Alice" 為專案 "Alice-1" 產生 Client Secret
    Then 回應狀態碼應為 201

  Scenario: 手動作廢第一組 Secret 後立即標示為已作廢，且不影響第二組
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    When 開發者 "Alice" 作廢專案 "Alice-1" 的第 1 組 Secret
    Then 回應狀態碼應為 204
    When 開發者 "Alice" 取得專案 "Alice-1" 的 OAuth 設定
    Then 第 1 組 Secret 的狀態應為 "Revoked"
    And 第 2 組 Secret 的狀態應為 "Active"

  Scenario: 作廢不存在或已作廢的 Secret 回傳 404
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    And 開發者 "Alice" 已作廢專案 "Alice-1" 的第 1 組 Secret
    When 開發者 "Alice" 作廢專案 "Alice-1" 的第 1 組 Secret
    Then 回應狀態碼應為 404

  Scenario: 改回 Public Client 後既有的 Secret 一併清除
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    And 開發者 "Alice" 已為專案 "Alice-1" 產生 Client Secret
    When 開發者 "Alice" 設定專案 "Alice-1" 的 OAuth Client：類型 "Public"、Redirect URIs "https://app.example.com/callback"、Post Logout URIs ""、範疇 "openid"
    Then 回應的 Secret 清單應有 0 組
    And Auth Server 中專案 "Alice-1" 的 Client 應為 Public、需要會員同意、強制 PKCE，範疇為 "openid"

  Scenario: 專案改名時同步 Auth Server 上 Client 的顯示名稱
    Given 開發者 "Alice" 已將專案 "Alice-1" 設為 Confidential Client
    When 開發者 "Alice" 將專案 "Alice-1" 更新為名稱 "新名稱"
    Then Auth Server 中專案 "Alice-1" 的 Client 顯示名稱應為 "新名稱"
