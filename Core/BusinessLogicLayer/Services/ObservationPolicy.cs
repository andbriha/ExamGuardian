using System.Text.Json;

namespace BusinessLogicLayer.Services;

public static class ObservationPolicy
{
    public static bool IsCleared(string decision) => decision is "Dismissed" or "NoViolation";
    public static bool ValidDecision(string decision) => decision is "Confirmed" or "Dismissed" or "NoViolation" or "InsufficientEvidence";
    public static string Priority(string type) => type switch
    {
        "identity_uncertain" or "analysis_unavailable" => "Intervention",
        "cell_phone" or "book" or "laptop" or "multiple_faces" or "extra_person" or "face_missing" or "prohibited_app" or "voice_activity" => "Warning",
        _ => "Observation"
    };
    public static int EpisodeRevision(string? metadata)
    {
        try {
            using var document = JsonDocument.Parse(metadata ?? "{}");
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("episode", out var episode) &&
                   episode.ValueKind == JsonValueKind.Object && episode.TryGetProperty("revision", out var revision) &&
                   revision.ValueKind == JsonValueKind.Number && revision.TryGetInt32(out var value) ? Math.Max(0, value) : 0;
        } catch (JsonException) { return 0; }
    }
}
