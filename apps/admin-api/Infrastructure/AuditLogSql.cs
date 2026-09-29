namespace AdminApi.Infrastructure;

public static class AuditLogSql
{
    // 資料庫層級的不可篡改：即使應用程式有 bug 或被入侵，也無法更新、刪除或清空稽核紀錄
    // （仍能被有 DDL 權限的人移除觸發器，正式環境需另外以權限與外部備份把關）。
    public const string CreateImmutabilityTriggers = """
        create function audit_logs_immutable() returns trigger language plpgsql as $$
        begin
            raise exception 'audit_logs 為不可篡改的稽核紀錄，禁止 % 操作', tg_op using errcode = 'restrict_violation';
        end
        $$;

        create trigger audit_logs_no_update_delete before update or delete on audit_logs
            for each row execute function audit_logs_immutable();

        create trigger audit_logs_no_truncate before truncate on audit_logs
            for each statement execute function audit_logs_immutable();
        """;

    public const string DropImmutabilityTriggers = """
        drop trigger if exists audit_logs_no_truncate on audit_logs;
        drop trigger if exists audit_logs_no_update_delete on audit_logs;
        drop function if exists audit_logs_immutable();
        """;
}
