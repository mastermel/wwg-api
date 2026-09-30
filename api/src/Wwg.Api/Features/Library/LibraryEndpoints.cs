using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Library;

/// <summary>
/// The club's library of factions and units, shared by every campaign (decision 0015): everyone
/// signed in reads it; Managers and Admins change it.
/// </summary>
internal static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var factions = app.MapGroup("/api/factions").WithTags("Library");
        factions.MapGet("", ListFactionsAsync).WithName("ListFactions").RequireSignedIn();
        factions.MapPost("", CreateFactionAsync).WithName("CreateFaction").RequireLibraryEditor();

        var faction = factions.MapGroup("/{id:guid}");
        faction
            .MapGet("", GetFactionAsync)
            .WithName("GetFaction")
            .RequireSignedIn()
            .ProducesProblem(StatusCodes.Status404NotFound);
        faction
            .MapPut("", UpdateFactionAsync)
            .WithName("UpdateFaction")
            .RequireLibraryEditor()
            .ProducesProblem(StatusCodes.Status404NotFound);
        faction
            .MapDelete("", DeleteFactionAsync)
            .WithName("DeleteFaction")
            .RequireLibraryEditor()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        faction
            .MapPost("/units", CreateUnitAsync)
            .WithName("CreateUnit")
            .RequireLibraryEditor()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var unit = app.MapGroup("/api/units/{id:guid}").WithTags("Library");
        unit.MapPut("", UpdateUnitAsync)
            .WithName("UpdateUnit")
            .RequireLibraryEditor()
            .ProducesProblem(StatusCodes.Status404NotFound);
        unit.MapDelete("", DeleteUnitAsync)
            .WithName("DeleteUnit")
            .RequireLibraryEditor()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>The library's factions, sorted by name, with how many units each has.</summary>
    internal static async Task<Ok<List<FactionSummary>>> ListFactionsAsync(
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            await db
                .Factions.AsNoTracking()
                .OrderBy(f => f.Name)
                .ThenBy(f => f.Id)
                .Select(f => new FactionSummary(
                    f.Id,
                    f.Name,
                    f.Nation,
                    db.Units.Count(u => u.FactionId == f.Id)
                ))
                .ToListAsync(cancellationToken)
        );

    /// <summary>A faction, with its units sorted by name.</summary>
    internal static async Task<Results<Ok<FactionResponse>, NotFound>> GetFactionAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        await LoadAsync(db, id, cancellationToken) is { } faction
            ? TypedResults.Ok(faction)
            : TypedResults.NotFound();

    /// <summary>Adds a faction to the library (Manager or Admin).</summary>
    internal static async Task<Created<FactionResponse>> CreateFactionAsync(
        SaveFactionRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var faction = new Faction { Name = request.Name, Nation = request.Nation };
        db.Factions.Add(faction);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/factions/{faction.Id}",
            new FactionResponse(faction.Id, faction.Name, faction.Nation, [])
        );
    }

    /// <summary>Renames a faction, or changes its flag (Manager or Admin).</summary>
    internal static async Task<Results<Ok<FactionResponse>, NotFound>> UpdateFactionAsync(
        Guid id,
        SaveFactionRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var faction = await db.Factions.SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (faction is null)
        {
            return TypedResults.NotFound();
        }

        (faction.Name, faction.Nation) = (request.Name, request.Nation);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok((await LoadAsync(db, id, cancellationToken))!); // Just saved.
    }

    /// <summary>Deletes an empty faction (Manager or Admin). 409 while it has units.</summary>
    internal static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteFactionAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (!await db.Factions.AnyAsync(f => f.Id == id, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (await db.Units.AnyAsync(u => u.FactionId == id, cancellationToken))
        {
            return InUse("This faction has units: delete or move them first.");
        }

        await db.Factions.Where(f => f.Id == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Adds a unit to a faction (Manager or Admin).</summary>
    internal static async Task<Results<Created<UnitResponse>, NotFound>> CreateUnitAsync(
        Guid id,
        SaveUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (!await db.Factions.AnyAsync(f => f.Id == id, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var unit = new Unit { FactionId = id, Name = request.Name };
        Apply(unit, request);
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/units/{unit.Id}", ToResponse(unit));
    }

    /// <summary>Changes a library unit (Manager or Admin). Campaigns it's already in keep their copies.</summary>
    internal static async Task<Results<Ok<UnitResponse>, NotFound>> UpdateUnitAsync(
        Guid id,
        SaveUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var unit = await db.Units.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (unit is null)
        {
            return TypedResults.NotFound();
        }

        Apply(unit, request);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(unit));
    }

    /// <summary>Deletes a library unit (Manager or Admin).</summary>
    internal static async Task<Results<NoContent, NotFound>> DeleteUnitAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        await db.Units.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken) == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();

    private static void Apply(Unit unit, SaveUnitRequest request) =>
        (unit.Name, unit.Type, unit.FightingFactor, unit.Points) = (
            request.Name,
            request.Type,
            request.FightingFactor,
            request.Points
        );

    private static UnitResponse ToResponse(Unit unit) =>
        new(unit.Id, unit.FactionId, unit.Name, unit.Type, unit.FightingFactor, unit.Points);

    private static async Task<FactionResponse?> LoadAsync(
        WwgDbContext db,
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var faction = await db
            .Factions.AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Nation,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (faction is null)
        {
            return null;
        }

        var units = await db
            .Units.AsNoTracking()
            .Where(u => u.FactionId == id)
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Id)
            .Select(u => new UnitResponse(
                u.Id,
                u.FactionId,
                u.Name,
                u.Type,
                u.FightingFactor,
                u.Points
            ))
            .ToListAsync(cancellationToken);
        return new FactionResponse(faction.Id, faction.Name, faction.Nation, units);
    }

    private static ProblemHttpResult InUse(string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "In use",
            detail: detail
        );
}
