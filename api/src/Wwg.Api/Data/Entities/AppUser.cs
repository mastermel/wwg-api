using Microsoft.AspNetCore.Identity;

namespace Wwg.Api.Data.Entities;

internal sealed class AppUser : IdentityUser<Guid>, IHasCreatedAt
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public DateTime CreatedAt { get; set; }
}
