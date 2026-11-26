using System.Text.Json;
using Staybnb.Models;

namespace Staybnb.Services;

public static class CheckInSteps
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<CheckInStep> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CheckInStep>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IEnumerable<CheckInStep> steps) => JsonSerializer.Serialize(steps, Json);

    public static List<CheckInStep> Defaults() =>
    [
        new() { Title = "Confirm your trip", Instructions = "Check that your dates and number of guests are correct." },
        new() { Title = "Verify your identity", Instructions = "Upload a clear photo or scan of your ID or passport. Your host will verify it before arrival.", RequiresDocument = true, DocumentType = "ID or Passport" },
        new() { Title = "House rules", Instructions = "No smoking indoors. No parties or events. Quiet hours are from 22:00 to 07:00. Please leave the property as you found it." }
    ];

    public static CheckInProcess CreateDefaultProcess() => new()
    {
        Title = "Standard check in",
        StepsJson = Serialize(Defaults())
    };
}
