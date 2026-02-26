using System.Diagnostics;
using System.Drawing;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

public class OllamaFunc(bool debug)
{
    private const string DefaultModel = "qwen3:4b-instruct";
    private bool DEBUG = debug;

    public string SelectedModel
    {
        get => Client.SelectedModel;
        set => Client.SelectedModel = SelectedModel;
    }

    public OllamaApiClient Client { get; set; } = new("http://localhost:11434", DefaultModel);
    public RequestOptions RequestOptions { get; set; } = new() { NumCtx = 2048, Temperature = 3.0f };
    public string[] AcceptedTypes { get; set; } = ["Drafting prenuptial agreements", "Representing clients in custody hearings", "Mediating property division settlements", "Internal meeting"];
    public string[] BillPitfalls { get; set; } = ["incomplete, short, and vague", "very short, only a few words long", "confusing and contains multiple grammatical errors", "unprofessional and contains multiple typos"];

    public record BillRecord(DateOnly Date, string Type, string Description, string Matter, string User, double Quantity, double Rate, double NonBillable, double Billable);

    public async Task<string[]> GenerateLawyerNames(int count, int retries = 3)
    {
        return await RunWithRetries(async () =>
        {
            var chat = await ChatWithStream(NewRequest([
                new(ChatRole.System, "You are a data generator. Output ONLY raw CSV data. No chat, no explanations."),
                new(ChatRole.User, $"Generate {count} unique full names.")
            ]));

            ArgumentNullException.ThrowIfNull(chat);

            string[] str = chat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            ArgumentOutOfRangeException.ThrowIfNotEqual(count, str.Length, nameof(count));

            for (int i = 0; i < count; i++)
                str[i] = str[i].Trim('"');

            return str;
        }, retries);
    }

    public async Task<string[]> GenerateClients(int count, int retries = 3)
    {
        return await RunWithRetries(async () =>
        {
            var chat = await ChatWithStream(NewRequest([
                new(ChatRole.System, "You are a data-only generator. Your output must strictly follow the pattern: #####-Name (random 5 digits, a dash, and a last name)."),
                new(ChatRole.User, $"Generate exactly {count} case names in the format 'xxxxx-Name'. Separate each with a comma. Do not use spaces or newlines. Start immediately with the first name.")
            ]));

            ArgumentNullException.ThrowIfNull(chat);

            string[] str = chat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            ArgumentOutOfRangeException.ThrowIfNotEqual(str.Length, count, nameof(count));

            for (int i = 0; i < count; i++)
                str[i] = str[i].Trim('"');

            return str;
        }, retries);
    }

    public async Task<BillRecord> GenerateBillable(string[] lawyers, string[] clients, DateTime start, DateTime end, int retries = 3)
    {
        return await RunWithRetries(async () =>
        {
            var valid = Random.Shared.Next(2) == 1;
            var date = DateOnly.FromDateTime(start.AddDays((end - start).Days));
            var type = AcceptedTypes[Random.Shared.Next(AcceptedTypes.Length)];

            var desc = await ChatWithStream(NewRequest([
                new(ChatRole.System, "You are a legal billing assistant. You output ONLY the narrative text for a single invoice line item. No headers, no footers, no metadata, and no currency amounts."),
                new(ChatRole.User, $"Write a billing description for the legal task: '{type}'.\nThe description must be {(valid ? "one-paragraph, detailed, clear, and professional" : BillPitfalls[Random.Shared.Next(BillPitfalls.Length)])}.\nDo not include a bill header or invoice number. Start immediately with the description text. Only include the description text. {(valid ? "Keep it under 4 sentences." : "Keep it under 4 sentences. Ensure that it isn't overly bad, it should just be inadequate, lacking, or unprofessional.")}")
            ]));
            ArgumentNullException.ThrowIfNull(desc);

            var matter = $"{clients[Random.Shared.Next(clients.Length)]}\n\"{(valid ? "" : "Non-")}Billable\" Time Tracking";
            var user = lawyers[Random.Shared.Next(lawyers.Length)];
            var quantity = Random.Shared.NextDouble() * 2;
            var rate = 375.0;
            var nbi = valid ? 0 : quantity * rate;
            var ybi = valid ? quantity * rate : 0;

            return new BillRecord(date, type, desc, matter, user, quantity, rate, nbi, ybi);
        }, retries);
    }

    private async Task<T> RunWithRetries<T>(Func<Task<T>> action, int retries)
    {
    start:
        try
        {
            return await action();
        }
        catch
        {
            if (retries > 0)
            {
                retries--;
                goto start;
            }

            throw;
        }
    }

    private async Task<string> ChatWithStream(ChatRequest request)
    {
        request.Stream = true;

        var chat = Client.ChatAsync(request);
        StringBuilder sb = new();
        await foreach (var chunk in chat)
        {
            if (chunk is null)
                continue;

            if (DEBUG)
            { // Print current token from model
                var fg = Console.ForegroundColor;

                if (chunk.Message.Thinking is string thinking)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write(thinking);
                }

                if (!string.IsNullOrEmpty(chunk.Message.Content))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(chunk.Message.Content);
                }

                Console.ForegroundColor = fg;
            }

            if (!string.IsNullOrEmpty(chunk.Message.Content))
            {
                sb.Append(chunk.Message.Content);
            }
        }

        return sb.ToString();
    }

    private async Task<string?> ChatWithoutStream(ChatRequest request)
    {
        request.Stream = false;

        var chat = Client.ChatAsync(request);
        var chunk = await chat.FirstAsync();
        if (chunk is null)
            return null;

        if (DEBUG)
        { // Print current token from model
            var fg = Console.ForegroundColor;

            if (chunk.Message.Thinking is string thinking)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(thinking);
            }

            if (!string.IsNullOrEmpty(chunk.Message.Content))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(chunk.Message.Content);
            }

            Console.ForegroundColor = fg;
        }

        return chunk.Message.Content;

    }

    private ChatRequest NewRequest(IEnumerable<Message> messages, string? model = null, RequestOptions? options = null)
    {
        return new ChatRequest
        {
            Messages = messages,
            Model = model ?? SelectedModel,
            Options = options ?? RequestOptions
        };
    }
}