Feature: 會員登入、登出與 SSO 會話管理
  身為已註冊的會員
  我希望能以帳號密碼登入並在登出時徹底清除會話
  以便安全地使用會員中心服務並防範跨站轉址等攻擊

  Background:
    Given 初始化測試伺服器

  Scenario: 正常帳密登入成功並發放 Session Cookie
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "login-success@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "login-success@1111.com.tw" 密碼 "P@ssw0rd2026!" 呼叫登入 API
    Then 回應狀態碼應為 200
    And 回應標頭 Set-Cookie 應包含 ".AspNetCore.Cookies"
    And 回應標頭 Set-Cookie 應包含 "domain=.1111.com.tw"
    And 回應標頭 Set-Cookie 應包含 "httponly"
    And 回應標頭 Set-Cookie 應包含 "secure"
    And 回應標頭 Set-Cookie 應包含 "samesite=lax"

  Scenario Outline: 攜帶合法的 returnUrl 登入成功並於回應帶回該 returnUrl
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "login-returnurl@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "login-returnurl@1111.com.tw" 密碼 "P@ssw0rd2026!" 並攜帶 returnUrl "<returnUrl>" 呼叫登入 API
    Then 回應狀態碼應為 200
    And 回應內容的 returnUrl 應為 "<returnUrl>"

    Examples:
      | returnUrl                             |
      | /dashboard                            |
      | https://profile.1111.com.tw/dashboard |

  Scenario Outline: 攜帶非法外部 returnUrl 登入失敗
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "login-badurl@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "login-badurl@1111.com.tw" 密碼 "P@ssw0rd2026!" 並攜帶 returnUrl "<returnUrl>" 呼叫登入 API
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details
    And 回應不應包含 Set-Cookie 標頭

    Examples:
      | returnUrl          |
      | https://evil.com   |
      | //evil.com          |

  Scenario: 待驗證（Pending）會員嘗試登入遭阻擋
    Given 系統已存在一筆狀態為 Pending 的會員，Email 為 "login-pending@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "login-pending@1111.com.tw" 密碼 "P@ssw0rd2026!" 呼叫登入 API
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
    And 回應不應包含 Set-Cookie 標頭

  Scenario: 停權（Suspended）會員嘗試登入遭阻擋
    Given 系統已存在一筆狀態為 Suspended 的會員，Email 為 "login-suspended@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "login-suspended@1111.com.tw" 密碼 "P@ssw0rd2026!" 呼叫登入 API
    Then 回應狀態碼應為 403
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
    And 錯誤類型應為 "https://auth.1111.com.tw/errors/member-suspended"
    And 回應不應包含 Set-Cookie 標頭

  Scenario Outline: 密碼錯誤或會員不存在時登入失敗
    Given 系統已存在一筆狀態為 Active 的會員，Email 為 "login-wrongpassword@1111.com.tw"，密碼為 "P@ssw0rd2026!"
    When 使用者以 Email "<email>" 密碼 "<password>" 呼叫登入 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
    And 回應不應包含 Set-Cookie 標頭

    Examples:
      | email                                | password         |
      | login-wrongpassword@1111.com.tw      | WrongPassw0rd!   |
      | login-not-registered@1111.com.tw     | P@ssw0rd2026!    |

  Scenario Outline: 請求格式錯誤時登入失敗
    When 使用者以 Email "<email>" 密碼 "<password>" 呼叫登入 API
    Then 回應狀態碼應為 400
    And 回應內容應為符合 RFC 7807 的驗證錯誤 Problem Details

    Examples:
      | email        | password |
      | not-an-email | P@ssw0rd2026! |
      |              | P@ssw0rd2026! |
      | login-empty-password@1111.com.tw | |

  Scenario: 攜帶有效 Session Cookie 呼叫登出成功
    Given 使用者已成功登入並取得有效的 Session Cookie
    When 使用者攜帶該 Session Cookie 呼叫登出 API
    Then 回應狀態碼應為 200
    And 回應標頭 Set-Cookie 應包含過期時間 "Thu, 01 Jan 1970"

  Scenario: 未攜帶 Session Cookie 呼叫登出失敗
    When 使用者未攜帶 Session Cookie 呼叫登出 API
    Then 回應狀態碼應為 401
    And 回應內容應為符合 RFC 7807 的 Problem Details 錯誤
