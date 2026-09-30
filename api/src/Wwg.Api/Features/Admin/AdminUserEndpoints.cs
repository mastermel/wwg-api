using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Auth;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Admin;

internal static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder admin)
    {
        var users = admin.MapGroup("/users");

        users.MapGet("", ListUsersAsync).WithName("ListUsers").ProducesValidationProblem();
        users
            .MapGet("/{id:guid}", GetUserAsync)
            .WithName("GetUser")
            .ProducesProblem(StatusCodes.Status404NotFound);
        users
            .MapPost("/{id:guid}/masquerade", MasqueradeAsync)
            .WithName("Masquerade")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        users
            .MapPut("/{id:guid}/manager", SetManagerAsync)
            .WithName("SetManager")
            .ProducesProblem(StatusCodes.Status404NotFound);
        users
            .MapDelete("/{id:guid}", DeleteUserAsync)
            .WithName("DeleteUser")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    /// <summary>
    /// Starts a masquerade as the user (decision 0012): the session becomes theirs, with exactly
    /// their permissions, marked as the Admin's masquerade and ending after
    /// <c>Auth:MasqueradeLifetime</c>. Not as yourself, and not while already masquerading (409).
    /// </summary>
    internal static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> MasqueradeAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        TokenService tokens,
        IOptions<AuthOptions> authOptions,
        TimeProvider time,
        ILogger<Masquerade> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Masquerade.From(principal) is not null)
        {
            return MasqueradeConflict("Already masquerading", "End this masquerade first.");
        }

        if (id == principal.GetUserId())
        {
            return MasqueradeConflict("That's you", "You can't masquerade as yourself.");
        }

        var target = await userManager.FindByIdAsync(id.ToString());
        var admin = await userManager.GetUserAsync(principal);
        if (target is null || admin is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var masquerade = new Masquerade(
            admin.Id,
            $"{admin.FirstName} {admin.LastName}",
            admin.SecurityStamp ?? "",
            time.GetUtcNow() + authOptions.Value.MasqueradeLifetime
        );
        MasqueradeLog.Started(logger, admin.Id, target.Id, masquerade.Ends);
        return TypedResults.Ok(await tokens.IssueAsync(httpContext, target, masquerade));
    }

    private static ProblemHttpResult MasqueradeConflict(string title, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: title,
            detail: detail
        );

    /// <summary>
    /// Users, sorted by last then first name, one page at a time. <c>search</c> matches any part of
    /// the first name, last name or email, ignoring case.
    /// </summary>
    internal static async Task<Ok<PagedResponse<UserSummary>>> ListUsersAsync(
        WwgDbContext db,
        CancellationToken cancellationToken,
        [StringLength(100)] string? search = null,
        [Range(1, Paging.MaxPage)] int page = 1,
        [Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize
    )
    {
        var adminRoleId = await RoleIdAsync(db, Roles.Admin, cancellationToken);
        var managerRoleId = await RoleIdAsync(db, Roles.Manager, cancellationToken);
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
                db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == managerRoleId),
                u.CreatedAt
            ))
            .ToPagedAsync(page, pageSize, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>One user's details.</summary>
    internal static async Task<Results<Ok<UserDetails>, ProblemHttpResult>> GetUserAsync(
        Guid id,
        WwgDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        var adminRoleId = await RoleIdAsync(db, Roles.Admin, cancellationToken);
        var managerRoleId = await RoleIdAsync(db, Roles.Manager, cancellationToken);
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
                IsManager = db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == managerRoleId),
                Campaigns = db
                    .CampaignMembers.Where(m => m.UserId == u.Id)
                    .OrderBy(m => m.Campaign.Name)
                    .ThenBy(m => m.CampaignId)
                    .Select(m => new UserCampaign(m.CampaignId, m.Campaign.Name, m.Role))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(
                new UserDetails(
                    user.Id,
                    user.Email ?? "",
                    user.FirstName,
                    user.LastName,
                    user.IsAdmin,
                    user.IsManager,
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
    internal static async Task<Results<NoContent, ProblemHttpResult>> DeleteUserAsync(
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
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        (await userManager.DeleteAsync(user)).ThrowIfFailed();
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Makes a user a Manager, or not (decision 0015): they edit the library. It applies at once
    /// (the library's access rule reads the role from the database).
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> SetManagerAsync(
        Guid id,
        SetManagerRequest request,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var isManager = await userManager.IsInRoleAsync(user, Roles.Manager);
        if (request.Manager && !isManager)
        {
            (await userManager.AddToRoleAsync(user, Roles.Manager)).ThrowIfFailed();
        }
        else if (!request.Manager && isManager)
        {
            (await userManager.RemoveFromRoleAsync(user, Roles.Manager)).ThrowIfFailed();
        }

        return TypedResults.NoContent();
    }

    private static Task<Guid> RoleIdAsync(
        WwgDbContext db,
        string role,
        CancellationToken cancellationToken
    ) =>
        db
            .Roles.Where(r => r.Name == role)
            .Select(r => r.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
