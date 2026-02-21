using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using OllamaSharp.Models.Chat;

namespace OllamaToolCalling;

public static class ToolExtensions
{
    private static List<Delegate> _tools = [];
    public static Task<string> InvokeToolAsync(Message.ToolCall call)
    {
        foreach (var del in _tools)
        {
            var tool = del.Method;
            if (tool.Name != call.Function?.Name)
                continue;

            List<object?> args = [];
            var para = tool.GetParameters();
            foreach (var v in para)
            {
                if (call.Function.Arguments!.TryGetValue(v.Name!, out var arg))
                {
                    if (v.ParameterType.IsEnum)
                        args.Add(Enum.Parse(v.ParameterType, arg!.ToString()!));
                    else if (v.ParameterType == typeof(string))
                        args.Add(arg!.ToString());
                    else if (v.ParameterType == typeof(int))
                        args.Add(int.Parse(arg!.ToString()!));
                    else if (v.ParameterType == typeof(bool))
                        args.Add(bool.Parse(arg!.ToString()!));
                    else if (v.ParameterType == typeof(int[]))
                    {
                        try
                        {
                            args.Add(JsonSerializer.Deserialize<int[]>(arg!.ToString()!));
                        }
                        catch
                        {
                            args.Add(JsonSerializer.Deserialize<string[]>(arg!.ToString()!)!.Select(int.Parse).ToArray());
                        }
                    }
                    else
                    {
                        args.Add(JsonSerializer.Deserialize(arg!.ToString()!, v.ParameterType));
                    }
                }
                else if (v.IsOptional)
                    args.Add(v.DefaultValue);
                else
                    throw new Exception($"Missing parameter {v.Name}");
            }

            object? result;
            try
            {
                result = tool.Invoke(null, args.ToArray());
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException ?? tie;
            }

            if (result is Task<string> task)
                return task;
            else
                return Task.FromResult(result!.ToString())!;
        }

        throw new Exception("Missing tool.");
    }

    public static Tool ToToolCall(Delegate del)
    {
        _tools.Add(del);

        var tool = del.Method;
        var para = tool.GetParameters();
        return new Tool
        {
            Type = "function",
            Function = new Function
            {
                Name = tool.Name,
                Description = tool.GetCustomAttribute<DescriptionAttribute>()!.Description,
                Parameters = new Parameters
                {
                    Type = "object",
                    Properties = para.Select(a => (a.Name!, new Property
                    {
                        Type = a.ParameterType.Name,
                        Description = a.GetCustomAttribute<DescriptionAttribute>()!.Description,
                        Enum = a.ParameterType.IsEnum ? GetEnumValues(a.ParameterType) : null,
                    })).ToDictionary(),
                    Required = para.Where(a => !a.IsOptional).Select(a => a.Name!)
                }
            }
        };
    }

    private static IEnumerable<string> GetEnumValues(Type t)
    {
        foreach (var v in Enum.GetValues(t))
            yield return v.ToString()!;
    }
}