public static class GlobalConfig
{
    // The URL Ollama should use to run the model.
    // Local host runs it on your machine. With the right config you can run it using a website address
    // or the IP address of a computer you control.
    public const string BaseAddress = "http://localhost:11434";

    // The model you want to run. Download this model first either using the command line
    // 'ollama pull {SelectedModel}' or via the Ollama UI (select the model, then run any message on it)
    // You only need to download it once.
    public const string SelectedModel = "qwen3-vl:4b";

    // Whether to allow the model to call tools.
    // Only some models support this, if it is set to true with a model
    // that doesn't support tools, it will throw an exception.
    public const bool EnableTools = true;

    // Whether to return one token at a time, or wait until the entire reponse is completed.
    // Generally streaming is better for applications that are client facing (like the UI that shows you the tokens as they come in)
    // While not streaming reduces the load on the client application, but increases load for the server.
    // (Client is whatever is calling ollama, and server is what's running it. In the case of using localhost these are the same machine,
    // so performance isn't much of an issue.)
    public const bool StreamResponse = false;

    // The temperature of the model. Higher temperature leads to more unpredictable responses.
    public const float Temperature = 0.0f;

    // The maximum context size before information is cut off.
    // Larger context sizes requires more VRAM, and can lead to
    // more reliance on your CPU. Keep it low like this (or maybe lower)
    // unless you have good hardware
    public const int ContextSize = 4096;
}