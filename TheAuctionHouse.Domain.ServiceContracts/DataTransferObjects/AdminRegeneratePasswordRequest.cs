public class AdminRegeneratePasswordRequest
{
    public int TargetUserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
