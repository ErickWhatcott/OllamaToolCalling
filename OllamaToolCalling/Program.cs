// See https://aka.ms/new-console-template for more information
using System.ComponentModel;
using System.Reflection;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using OllamaToolCalling;

public static partial class Program
{
    // The client that handles routing and network.
    public static OllamaApiClient client = new(new HttpClient
    {
        BaseAddress = new(GlobalConfig.BaseAddress),
        Timeout = TimeSpan.FromMinutes(10),
    }, GlobalConfig.SelectedModel);

    // The parameters used to modify the behavior of the model.
    public static RequestOptions requestOptions = new()
    {
        Temperature = GlobalConfig.Temperature,
        NumCtx = GlobalConfig.ContextSize,
    };

    // A list of tools avaliable to the model.
    // Only some models can use these.
    public static IEnumerable<object> Tools = [
        ToolExtensions.ToToolCall(GetCountiesInState),
        ToolExtensions.ToToolCall(GetCurrentTemperature),
    ];

    public static async Task Main()
    {
        try { Console.Clear(); } catch { }


    start:
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("What is your next question?");
        Console.ForegroundColor = ConsoleColor.Cyan;
        string user_prompt = Console.ReadLine()!;
        Console.ForegroundColor = ConsoleColor.Green;
        if (string.IsNullOrWhiteSpace(user_prompt))
        {
            Console.WriteLine("The prompt cannot be empty. Please try again.");
            goto start;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Running the prompt: {user_prompt}");
        Console.ForegroundColor = ConsoleColor.White;

        List<Message> messages = [
            new(ChatRole.User, user_prompt)
        ];

        var req = new ChatRequest
        {
            Messages = messages,
            Tools = GlobalConfig.EnableTools ? Tools : null,
            Stream = GlobalConfig.StreamResponse,
            Options = requestOptions
        };

    loop:
        var chat = client.ChatAsync(req);
        StringBuilder thoughts = new();
        StringBuilder content = new();
        List<Message.ToolCall> tools = [];

        bool thinking = false;
        await foreach (var slice in chat)
        {
            if (slice is null)
                continue;

            if (!string.IsNullOrEmpty(slice.Message.Thinking))
            {
                if (!thinking)
                {
                    thinking = true;
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("<think>");
                }

                Console.Write(slice.Message.Thinking);
                thoughts.Append(slice.Message.Thinking);
            }

            if (!string.IsNullOrEmpty(slice.Message.Content))
            {
                if (thinking)
                {
                    thinking = false;
                    Console.WriteLine("</think>");
                    Console.ForegroundColor = ConsoleColor.White;
                }

                Console.Write(slice.Message.Content);
                content.Append(slice.Message.Content);
            }

            if (slice.Message.ToolCalls is IEnumerable<Message.ToolCall> toolCalls)
            {
                if (thinking)
                {
                    thinking = false;
                    Console.WriteLine("</think>");
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                foreach (var tc in toolCalls)
                {
                    if (tc.Function is null)
                        continue;

                    Console.WriteLine($"{tc.Function.Name}: ({string.Join(", ", tc.Function.Arguments!.Select(a => $"({a.Key}: {a.Value})"))})");
                    tools.Add(tc);
                }
                Console.ForegroundColor = ConsoleColor.White;
            }

            if (slice is ChatDoneResponseStream doneSlice)
            {
                // Now you have access to the exact token counts!
                int promptTokens = doneSlice.PromptEvalCount;
                int generatedTokens = doneSlice.EvalCount;
                var total = promptTokens + generatedTokens;
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n{total} / {requestOptions.NumCtx} tokens used. {promptTokens} were in the prompt.");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        if (tools.Count > 0)
        {
            messages.Add(new()
            {
                Content = content.ToString(),
                Thinking = await SummarizeThoughts(thoughts.ToString(), tools),
                ToolCalls = tools,
                Role = ChatRole.Assistant
            });
        }

        foreach (var tc in tools)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{tc.Function!.Name}: ({string.Join(", ", tc.Function.Arguments!.Select(a => $"({a.Key}: {a.Value})"))}) -> ");
            Console.ForegroundColor = ConsoleColor.White;

            try
            {
                var result = await ToolExtensions.InvokeToolAsync(tc);
                Console.WriteLine($"{result}");
                messages.Add(new()
                {
                    Role = ChatRole.Tool,
                    ToolName = tc.Function.Name,
                    Content = result,
                });
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to invoke tool: {e.Message}");
                Console.WriteLine(e.ToString());
                messages.Add(new()
                {
                    Role = ChatRole.Tool,
                    ToolName = tc.Function.Name,
                    Content = $"Failed to invoke: {e.Message}",
                });
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
        }

        if (tools.Count > 0)
            goto loop;

        Console.WriteLine();
        goto start;
    }

    private static async Task<string> SummarizeThoughts(string thoughts, List<Message.ToolCall> tools)
    {
        List<Message> messages = [
            new(ChatRole.System, "You will be given the train of thought of an LLM. Your job is to summarize it, keeping only the important and relevant thoughts around, discarding all other information. You show frame it as \"I did this, and these are what I will do next\". Be specific on exactly what methods you invoked, and give a numbered list of the future steps, if present."),
            new(ChatRole.User, @$"
<thoughts>
{thoughts}
</thoughts>

The LLM invoked the following tools. Please ensure that the summary is done in context of that:
<tools>
{string.Join("\n", tools.Select(a => $"<tool><name>{a.Function?.Name}</name><arguments>{string.Join("\n", a.Function!.Arguments!.Select(a => $"<argument><arg>{a.Key}</arg><value>{a.Value}</value></argument>")!)}</arguments></tool>"))}
</tools>")
        ];

        var req = new ChatRequest
        {
            Messages = messages,
            Stream = GlobalConfig.StreamResponse,
            Options = requestOptions
        };
        var chat = client.ChatAsync(req);

        Console.WriteLine("<summary>");

        StringBuilder content = new();
        await foreach (var slice in chat)
        {
            if (slice is null)
                continue;
            if (!string.IsNullOrEmpty(slice.Message.Content))
            {
                Console.Write(slice.Message.Content);
                content.Append(slice.Message.Content);
            }
        }

        Console.WriteLine("\n</summary>");
        return content.ToString();
    }
}