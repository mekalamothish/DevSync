using Microsoft.AspNetCore.Identity;

namespace DevSync.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.NewGuid();
}
