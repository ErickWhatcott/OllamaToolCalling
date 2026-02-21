using System.ComponentModel;

public static partial class Program
{
    [Description("Retrieves a JSON array of all counties in the given state")]
    private static string GetCountiesInState(
    [Description("The state to get the counties for")] string state)
    {
        if(!state.Equals("utah", StringComparison.CurrentCultureIgnoreCase))
            return $"Unrecognized state. Please ensure that it is a valid state in the United States.";

        return $"The counties in {state} are \"Salt Lake\", \"Utah\", and \"St George\"";
    }

    [Description("Gets the current weather at the given location. Returns a number describing the current temperature.")]
    private static string GetCurrentTemperature(
        [Description("The location to get the weather for")] string location,
        [Description("The unit to measure the temperature in")] Unit unit = Unit.Fahrenheit)
            => $"The temperature in {location} is {(unit is Unit.Celsius ? Random.Shared.Next(-5, 50) : Random.Shared.Next(0, 95))} degrees {unit}.";

    private enum Unit
    {
        Celsius,
        Fahrenheit
    }
}