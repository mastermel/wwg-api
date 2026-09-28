using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Admin;

internal static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder admin)
    {
        var users = admin.MapGroup("/users");

        users.MapGet("", ListUsersAsync).WithName("ListUsers").ProducesValidationProblem();
        users.MapGet("/{id:guid}", GetUserAsync).WithName("GetUser");
        users
            .MapDelete("/{id:guid}", DeleteUserAsync)
            .WithName("DeleteUser")
            .ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    /// <summary>
    /// Users, sorted by last then first name, one page at a time. <c>search</c> matches any part of
    /// the first name, last name or email, ignoring case.
    /// </summary>
    internal static async Task<Ok<PagedResponse<UserSummary>>> ListUsersAsync(
        WwgDbContext db,
        CancellationToken cancellationToken,
        [StringLength(100)] string? search = null,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize
    )
    {
        var adminRoleId = await AdminRoleIdAsync(db, cancellationToken);
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Search.ContainsPattern(search.Trim());
            query = query.Where(u =>
                EF.Functions.Like(u.FirstName, pattern, Search.EscapeCharacter)
                || EF.Functions.Like(u.LastName, pattern, Search.EscapeCharacter)
                || EF.Functions.Like(u.Email!, pattern, Search.EscapeCharacter) // SQL: null never matches.
            );
        }

        var result = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.Id)
            .Select(u => new UserSummary(
                u.Id,
                u.Email ?? "",
                u.FirstName,
                u.LastName,
                db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId),
                u.CreatedAt
            ))
            .ToPagedAsync(page, pageSize, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>One user's details.</summary>
    internal static async Task<Results<Ok<UserDetails>, NotFound>> GetUserAsync(
        Guid id,
        WwgDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        var adminRoleId = await AdminRoleIdAsync(db, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var user = await db
            .Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.CreatedAt,
                u.LockoutEnd,
                IsAdmin = db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId),
                Campaigns = db
                    .CampaignMembers.Where(m => m.UserId == u.Id)
                    .OrderBy(m => m.Campaign.Name)
                    .ThenBy(m => m.CampaignId)
                    .Select(m => new UserCampaign(m.CampaignId, m.Campaign.Name, m.Role))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(
                new UserDetails(
                    user.Id,
                    user.Email ?? "",
                    user.FirstName,
                    user.LastName,
                    user.IsAdmin,
                    user.CreatedAt,
                    user.LockoutEnd > now ? user.LockoutEnd.Value.UtcDateTime : null,
                    user.Campaigns
                )
            );
    }

    /// <summary>
    /// Deletes a user and their sign-in data; their sessions end immediately. Admins can't delete
    /// their own account.
    /// </summary>
    internal static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteUserAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (
            string.Equals(
                userManager.GetUserId(principal),
                id.ToString(),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Can't delete yourself",
                detail: "Admins can't delete their own account."
            );
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        (await userManager.DeleteAsync(user)).ThrowIfFailed();
        return TypedResults.NoContent();
    }

    private static Task<Guid> AdminRoleIdAsync(
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        db
            .Roles.Where(r => r.Name == Roles.Admin)
            .Select(r => r.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
