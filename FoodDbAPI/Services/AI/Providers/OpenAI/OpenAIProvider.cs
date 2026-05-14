using System.Runtime.CompilerServices;
using System.Text;
using FoodDbAPI.Models.Settings;
using FoodDbAPI.Services.AI.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace FoodDbAPI.Services.AI.Providers.OpenAI;

/// <summary>
/// OpenAI implementation of <see cref="IAIProvider"/>.
/// Uses the official OpenAI .NET SDK (v2.x) internally — tool-call argument
/// accumulation across streaming chunks is handled by the SDK automatically.
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly ChatClient _client;
    private readonly OpenAIProviderSettings _settings;

    public OpenAIProvider(IOptions<AISettings> options)
    {
        _settings = options.Value.OpenAI;
        _client = new ChatClient(
            model: _settings.Model,
            credential: new ApiKeyCredential(_settings.ApiKey));
    }

    public async IAsyncEnumerable<AIStreamEvent> StreamAsync(
        IList<AIMessage> messages,
        IList<AIToolDefinition>? tools = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatMessages = messages.Select(MapToChatMessage).ToList();
        var options = BuildOptions(tools);

        // Per-tool-call state: keyed by stream index
        var toolCallIds   = new Dictionary<int, string>();
        var toolCallNames = new Dictionary<int, string>();
        var toolCallArgs  = new Dictionary<int, StringBuilder>();
        var doneEmitted   = false;

        AsyncCollectionResult<StreamingChatCompletionUpdate> stream;
        try
        {
            stream = _client.CompleteChatStreamingAsync(chatMessages, options, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new AIProviderException("Failed to start OpenAI streaming request.", ex);
        }

        // Use manual enumerator so we can wrap MoveNextAsync() in try/catch.
        // C# iterators forbid yield inside try/catch, but the yield statements
        // below are outside the inner catch — only the MoveNextAsync call is guarded.
        var enumerator = stream.WithCancellation(cancellationToken).GetAsyncEnumerator();
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (ClientResultException ex)
                {
                    throw new AIProviderException(
                        $"OpenAI API error (HTTP {ex.Status}): {ex.Message}", ex);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new AIProviderException(
                        $"OpenAI streaming failed: {ex.Message}", ex);
                }

                if (!hasNext) break;

                var update = enumerator.Current;

                // ── Text content ─────────────────────────────────────────────────
                foreach (var part in update.ContentUpdate)
                {
                    if (part.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrEmpty(part.Text))
                        yield return new TextChunkEvent(part.Text);
                }

                // ── Tool call fragments ───────────────────────────────────────────
                foreach (var tcUpdate in update.ToolCallUpdates)
                {
                    var idx = tcUpdate.Index;

                    if (!string.IsNullOrEmpty(tcUpdate.ToolCallId))
                        toolCallIds[idx] = tcUpdate.ToolCallId;

                    if (!string.IsNullOrEmpty(tcUpdate.FunctionName))
                        toolCallNames[idx] = tcUpdate.FunctionName;

                    if (!toolCallArgs.ContainsKey(idx))
                        toolCallArgs[idx] = new StringBuilder();

                    if (tcUpdate.FunctionArgumentsUpdate is not null)
                        toolCallArgs[idx].Append(tcUpdate.FunctionArgumentsUpdate);
                }

                // ── Finish reasons ────────────────────────────────────────────────
                if (update.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Emit one completed event per tool call, ordered by index
                    foreach (var idx in toolCallIds.Keys.OrderBy(i => i))
                    {
                        var id   = toolCallIds.TryGetValue(idx, out var tid) ? tid : string.Empty;
                        var name = toolCallNames.TryGetValue(idx, out var tn) ? tn : string.Empty;
                        var args = toolCallArgs.TryGetValue(idx, out var ta) ? ta.ToString() : "{}";
                        yield return new ToolCallCompletedEvent(id, name, args);
                    }

                    toolCallIds.Clear();
                    toolCallNames.Clear();
                    toolCallArgs.Clear();

                    yield return new StreamDoneEvent();
                    doneEmitted = true;
                }
                else if (update.FinishReason == ChatFinishReason.Stop)
                {
                    yield return new StreamDoneEvent();
                    doneEmitted = true;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        // Defensive: ensure StreamDoneEvent is always emitted even if the
        // stream ends without an explicit finish_reason (e.g. truncated response).
        if (!doneEmitted)
            yield return new StreamDoneEvent();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static ChatMessage MapToChatMessage(AIMessage msg) => msg.Role switch
    {
        AIRole.System => new SystemChatMessage(msg.Content ?? string.Empty),

        AIRole.User => new UserChatMessage(msg.Content ?? string.Empty),

        AIRole.Assistant when msg.ToolCalls is { Count: > 0 } =>
            new AssistantChatMessage(
                msg.ToolCalls.Select(tc =>
                    ChatToolCall.CreateFunctionToolCall(
                        tc.Id,
                        tc.FunctionName,
                        BinaryData.FromString(tc.ArgumentsJson)))),

        AIRole.Assistant => new AssistantChatMessage(msg.Content ?? string.Empty),

        AIRole.Tool => new ToolChatMessage(msg.ToolCallId!, msg.Content ?? string.Empty),

        _ => throw new ArgumentOutOfRangeException(nameof(msg), $"Unsupported AIRole: {msg.Role}")
    };

    private ChatCompletionOptions BuildOptions(IList<AIToolDefinition>? tools)
    {
        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _settings.MaxTokens,
            Temperature = _settings.Temperature
        };

        if (tools is not { Count: > 0 })
            return options;

        foreach (var tool in tools)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                functionName: tool.Name,
                functionDescription: tool.Description,
                functionParameters: BinaryData.FromString(tool.ParametersJsonSchema.GetRawText())));
        }

        return options;
    }
}
