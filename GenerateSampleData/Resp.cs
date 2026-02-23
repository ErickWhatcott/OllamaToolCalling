using System.Text.Json.Serialization;
using BillRecord = OllamaFunc.BillRecord;

public class DataResponse
{
    [JsonPropertyName("lawyers")]
    public required string[] Lawyers { get; init; }

    [JsonPropertyName("clients")]
    public required string[] Clients { get; init; }

    [JsonPropertyName("bills")]
    public required List<BillRecord> Bills { get; init; }
}