using System.Text.Json;
using BillRecord = OllamaFunc.BillRecord;

const int LAWYERS = 8;
const int CLIENTS = 25;
const int BILLS = 100;

OllamaFunc func = new();
var lawyers = await func.GenerateLawyerNames(count: LAWYERS);
var clients = await func.GenerateClients(count: CLIENTS);
var bills = await AsyncEnumerable.Range(0, count: BILLS).SelectAsync(async i => await func.GenerateBillable(lawyers, clients, DateTime.Today.AddDays(7), DateTime.Today)).ToListAsync();

var response = new DataResponse
{
    Lawyers = lawyers,
    Clients = clients,
    Bills = bills,
};

Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions
{
    WriteIndented = true,
}));