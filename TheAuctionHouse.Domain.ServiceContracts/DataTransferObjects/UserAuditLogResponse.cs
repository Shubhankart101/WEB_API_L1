public class UserAuditLogResponse
{
    public DateTime TimestampUtc { get; set; }
    public int ActorUserId { get; set; }
    public int? TargetUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
