namespace Huamishu.Api.Data.Entities;

/// <summary>用户。Plan/PlanExpires 为 V2 收费预留。</summary>
public sealed class User
{
    public long Id { get; set; }
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Nickname { get; set; }
    public string Plan { get; set; } = "free";
    public DateTime? PlanExpires { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Tag> Tags { get; set; } = [];
}