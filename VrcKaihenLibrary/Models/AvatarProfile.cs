using System.Collections.Generic;

namespace VrcKaihenLibrary.Models;

public sealed record AvatarProfile(
    string RegistrationId,
    long? BoothItemId,
    string Name,
    string PrimaryIdentifier,
    IReadOnlyList<string> Identifiers,
    string? BaseBodyGroup,
    bool IsUnpurchased = false,
    string? BoothUrl = null,
    string? ShopName = null,
    string? ThumbnailUrl = null);
public sealed record CompatibilityMatch(string AvatarRegistrationId, string AvatarName, string Evidence, bool ThroughBaseBody);
