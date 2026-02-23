using System.Text.Json;
using BillRecord = OllamaFunc.BillRecord;

OllamaFunc func = new();
var lawyers = await func.GenerateLawyerNames(count: 2);
var clients = await func.GenerateClients(count: 3);

List<BillRecord> bills = [];

for(int i = 0; i < 10; i++)
{
    var billable = await func.GenerateBillable(lawyers, clients, DateTime.Today.AddDays(7), DateTime.Today);
    bills.Add(billable);
}

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