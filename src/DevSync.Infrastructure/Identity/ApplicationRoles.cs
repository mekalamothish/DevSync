namespace DevSync.Infrastructure.Identity;

public static class ApplicationRoles
{
    public const string Member = "Member";
    public const string Moderator = "Moderator";
    public const string Administrator = "Administrator";
    public static readonly string[] All = [Member, Moderator, Administrator];
}
