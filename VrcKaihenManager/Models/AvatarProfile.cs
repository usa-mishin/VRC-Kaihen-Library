using System.Collections.Generic;

namespace VrcKaihenManager.Models;

public sealed record AvatarProfile(
    string RegistrationId,
    long? BoothItemId,
    string Name,
    string PrimaryIdentifier,
    IReadOnlyList<string> Identifiers,
    string? BaseBodyGroup);
public sealed record CompatibilityMatch(string AvatarRegistrationId, string AvatarName, string Evidence, bool ThroughBaseBody);
