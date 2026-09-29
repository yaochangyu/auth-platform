Feature: 會員個人檔案檢視與變更密碼
  身為已登入的會員
  我希望能查看自己的個人檔案並在驗證舊密碼後安全地變更密碼
  以便掌握帳號資訊，且密碼異動時自動註銷其他裝置的登入會話

  Background:
    Given 初始化測試伺服器

  Scenario: 已登入會員成功取得個人檔案
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 呼叫取得個人檔案 API
    Then 回應狀態碼應為 200
    And 回應內容應包含會員的 Email、暱稱與狀態

  Scenario: 未登入時取得個人檔案失敗
    When 使用者未攜帶 Session Cookie 呼叫取得個人檔案 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 已登入會員成功變更密碼
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 以舊密碼 "P@ssw0rd2026!" 設定新密碼 "BrandN3wP@ss2026!" 並確認密碼 "BrandN3wP@ss2026!" 呼叫變更密碼 API
    Then 回應狀態碼應為 200
    And 回應標頭應包含重新發行的 Session Cookie
    And 該會員應可使用新密碼 "BrandN3wP@ss2026!" 成功登入

  Scenario: 舊密碼錯誤時變更密碼失敗
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 以舊密碼 "WrongOldPassw0rd!" 設定新密碼 "BrandN3wP@ss2026!" 並確認密碼 "BrandN3wP@ss2026!" 呼叫變更密碼 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario Outline: 新密碼驗證失敗時變更密碼失敗
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 以舊密碼 "P@ssw0rd2026!" 設定新密碼 "<newPassword>" 並確認密碼 "<confirmPassword>" 呼叫變更密碼 API
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details

    Examples:
      | newPassword        | confirmPassword |
      | weak               | weak             |
      | BrandN3wP@ss2026!  | Different1!      |

  Scenario: 未登入時呼叫變更密碼 API 失敗
    When 使用者未攜帶 Session Cookie 以舊密碼 "P@ssw0rd2026!" 設定新密碼 "BrandN3wP@ss2026!" 並確認密碼 "BrandN3wP@ss2026!" 呼叫變更密碼 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤

  Scenario: 變更密碼成功後其他裝置的舊 Session Cookie 立即失效，目前裝置的新 Cookie 仍可使用
    Given 使用者已於「裝置 A」成功登入並取得有效的 Session Cookie
    And 使用者已於「裝置 B」以相同帳號成功登入並取得另一組有效的 Session Cookie
    When 使用者攜帶「裝置 A」的 Session Cookie 以舊密碼 "P@ssw0rd2026!" 設定新密碼 "BrandN3wP@ss2026!" 並確認密碼 "BrandN3wP@ss2026!" 呼叫變更密碼 API
    Then 回應狀態碼應為 200
    And 使用者攜帶回應中重新發行的 Session Cookie 呼叫取得個人檔案 API 應成功
    And 使用者攜帶「裝置 B」的 Session Cookie 呼叫取得個人檔案 API 應回應 401
