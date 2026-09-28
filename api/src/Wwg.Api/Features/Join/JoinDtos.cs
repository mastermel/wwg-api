using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Join;

/// <summary>What a join link shows before joining. Public: anyone with the code can see it.</summary>
/// <param name="CampaignName">The campaign's name.</param>
/// <param name="UmpireName">The Umpire's name, or null if the campaign has none.</param>
public sealed record JoinPreviewResponse(string CampaignName, string? UmpireName);

/// <summary>The campaign the caller is now in.</summary>
/// <param name="CampaignId">The campaign's ID.</param>
/// <param name="MyRole">The caller's role: Player, or their existing role if already a member.</param>
public sealed record JoinCampaignResponse(Guid CampaignId, CampaignRole MyRole);
