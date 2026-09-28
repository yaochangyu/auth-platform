# 會員中心領域模型上下文 (Member Center Context)

會員中心領域負責管理平台自然人之會員身分識別、驗證憑據、註冊生命週期、認證會話 (Session) 以及第三方應用程式之授權存取。

## 統一領域語言 (Ubiquitous Language)

**Member (會員)**:
在平台註冊且具備經驗證之 Email 憑據的自然人。
_避免使用_: User (使用者), Account (帳號), Client (客戶端), Customer (顧客)

**Pending Member (待驗證會員)**:
已送出註冊申請表單，但尚未完成 Email 所有權驗證確認之會員。
_避免使用_: UnverifiedUser (未驗證使用者), InactiveAccount (未啟用帳號)

**Member Status (會員狀態)**:
會員紀錄在系統中的生命週期階段（`Pending` 待驗證、`Active` 已啟用、`Suspended` 停權/鎖定）。
_避免使用_: AccountState (帳號狀態), UserStatus (使用者狀態)

**Credential (認證憑據)**:
綁定於會員身上、用於驗證身分真偽的安全機密（如加鹽密碼雜湊）。
_避免使用_: Secret (機密), Key (金鑰), AuthData (驗證資料)

**Verification Token (驗證權杖)**:
短效、單次使用的加密權杖，用於驗證信箱所有權或授權重設密碼。
_避免使用_: OTP (動態密碼), Code (代碼), ActivationKey (啟用金鑰), AuthToken (認證權杖)

**Session Cookie (會話 Cookie)**:
作用域限定於 `.1111.com.tw` 的 HttpOnly 安全 Cookie，代表會員在瀏覽器中的有效登入會話。
_避免使用_: BearerToken (持有者權杖), JWT, AccessToken (存取權杖)

**Security Stamp (安全戳記)**:
儲存於會員紀錄上之唯一識別碼，每當密碼變更或重設時立即更新，用於使既有歷史 Session Cookie 瞬間作廢。
_避免使用_: SessionId (會話ID), Salt (鹽值), TokenVersion (權杖版本)

**Lockout (暫時鎖定)**:
因連續憑據驗證失敗而對會員施加的暫時性安全限制，在鎖定期間內拒絕所有登入嘗試。
_避免使用_: Ban (封鎖), Block (封阻), Blacklist (黑名單)

**Outbox Message (發信交易任務)**:
在同一資料庫交易中持久化儲存、供背景背景服務非同步可靠派發的領域通知事件記錄。
_避免使用_: Job (工作), Task (任務), MailQueueItem (郵件佇列項目)

**Connected App (已連結的應用程式)**:
經由會員本人明確同意授權，得以代表該會員存取特定個人資源之第三方應用程式。
_避免使用_: Integration (整合), ThirdPartyClient (第三方客戶端), ExternalApp (外部應用程式)
