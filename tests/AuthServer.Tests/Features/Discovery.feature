Feature: 授權伺服器探索端點與 JWKS 公開金鑰
  身為串接 OAuth 2.1 / OIDC 的資源伺服器或用戶端
  我希望能查詢授權伺服器的中繼資料與公開簽章金鑰
  以便離線驗證授權伺服器簽發的 JWT

  Background:
    Given 初始化 Auth Server 測試伺服器

  Scenario: 查詢 OpenID Configuration 取得正確的中繼資料
    When 使用者呼叫 GET /.well-known/openid-configuration 端點
    Then 回應狀態碼應為 200
    And 回應內容的 issuer 欄位應與伺服器位址一致
    And 回應內容的 jwks_uri 欄位應為 "/.well-known/jwks.json" 的完整網址

  Scenario: 查詢 JWKS 端點取得非對稱公開金鑰集合
    When 使用者呼叫 GET /.well-known/jwks.json 端點
    Then 回應狀態碼應為 200
    And 回應內容的 keys 陣列應至少包含一組公開金鑰
    And 該公開金鑰的 kty 欄位應為 "RSA"

  Scenario: 服務重啟後仍使用相同的簽章金鑰
    Given Auth Server 已啟動並取得目前的 JWKS 公開金鑰
    When 重新啟動 Auth Server 測試伺服器
    And 使用者再次呼叫 GET /.well-known/jwks.json 端點
    Then 回應內容的公開金鑰應與重啟前完全相同
