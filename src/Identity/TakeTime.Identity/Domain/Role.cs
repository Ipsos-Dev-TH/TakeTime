using TakeTime.Core.Domain.Base;

namespace TakeTime.Identity.Domain;

public class Role : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = [];
    public bool IsSystemRole { get; set; }

    public static Role Create(string name, string? description = null, params string[] permissions)
    {
        return new Role
        {
            Name = name,
            Description = description,
            Permissions = [.. permissions]
        };
    }
}
