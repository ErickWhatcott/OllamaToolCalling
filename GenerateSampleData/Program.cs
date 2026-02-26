using System.Globalization;
using CsvHelper;

const bool DEBUG = false;
const int LAWYERS = 8;
const int CLIENTS = 25;
const int BILLS = 100;

string outputPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Downloads",
    "output.csv"
);

OllamaFunc func = new(DEBUG);
var lawyers = await func.GenerateLawyerNames(count: LAWYERS);
var clients = await func.GenerateClients(count: CLIENTS);
var bills = await AsyncEnumerable.Range(0, count: BILLS).SelectAsync(async i =>
{
    Console.WriteLine($"Making bill {i}");
    return await func.GenerateBillable(lawyers, clients, DateTime.Today.AddDays(-7), DateTime.Today);
}).ToListAsync();

using var writer = new StreamWriter(outputPath, false);
using var csv = new CsvWriter(writer, CultureInfo.CurrentCulture);

// csv.WriteHeader<OllamaFunc.BillRecord>();
csv.WriteRecords(bills);
// foreach (var bill in bills)
//     csv.WriteRecord(bill);