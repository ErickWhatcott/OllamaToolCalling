using System.Diagnostics;
using System.Drawing;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

public class OllamaFunc(bool debug)
{
    // The default model.
    private const string DefaultModel = "qwen3:4b-instruct";
    
    // Whether to print out the LLM responses.
    private bool DEBUG = debug;

    // The model used by any function.
    public string SelectedModel
    {
        get => Client.SelectedModel;
        set => Client.SelectedModel = SelectedModel;
    }

    // The Ollama wrapper. This handles networking, interfacing with Ollama, and default configurations.
    public OllamaApiClient Client { get; set; } = new("http://localhost:11434", DefaultModel);

    // Default request options used when querying ollama.
    public RequestOptions RequestOptions { get; set; } = new() { NumCtx = 2048, Temperature = 3.0f };

    // The different types of bills that can exist.
    // i.e. the types of work that the lawyers will do.
    public string[] AcceptedTypes { get; set; } = ["Drafting prenuptial agreements", "Representing clients in custody hearings", "Mediating property division settlements", "Internal meeting"];

    // Defines a list of different 'pitfalls' that the LLM will use when generating bad bills.
    // A pitfall is chosen at random and injected into the prompt, so it will generate a bill description specifically tailored to the pitfall.
    public string[] BillPitfalls { get; set; } = ["incomplete, short, and vague", "very short, only a few words long", "confusing and contains multiple grammatical errors", "unprofessional and contains multiple typos"];

    // A data structure to hold all bill record
    public record BillRecord(DateOnly Date, string Type, string Description, string Matter, string User, double Quantity, double Rate, double NonBillable, double Billable);

    // Uses the LLM to generate a list of lawyer names
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

    // Uses the LLM to generate a CSV of client names.
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

    // Generates a bill record based on the specified parameters.
    // It will be assigned to one of the lawyers and one of the clients specified.
    // It will take class between the start and end date.
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


    // Due to the high temperature (3.0), the models can often return wrong information or return data in a bad format.
    // This retries the action up to the specified number of times, properly accounting for this.
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

    // Streams the response from ollama.
    // Streaming is when the application recieves every token one at a time,
    // rather than it all being buffered. This helps with debugging because you can see it think,
    // and it's useful for long responses because it every token resets the patience of the client.
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

    // Simple method to save code when using the default request options and model
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