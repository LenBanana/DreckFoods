using System.Runtime.CompilerServices;
using FoodDbAPI.Models.Settings;
using FoodDbAPI.Services.AI.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;

#pragma warning disable OPENAI001 // Responses API is experimental in this SDK version

namespace FoodDbAPI.Services.AI.Providers.OpenAI;

/// <summary>
/// OpenAI Responses API (<c>/v1/responses</c>) implementation of <see cref="IAIProvider"/>.
/// Supports <c>reasoning_effort</c> together with function tools, unlike the Chat Completions endpoint.
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly ResponsesClient _client;
    private readonly OpenAIProviderSettings _settings;

    public OpenAIProvider(IOptions<AISettings> options)
    {
        _settings = options.Value.OpenAI;
        _client = new OpenAIClient(new ApiKeyCredential(_settings.ApiKey))
            .GetResponsesClient();
    }

    public async IAsyncEnumerable<AIStreamEvent> StreamAsync(
        IList<AIMessage> messages,
        IList<AIToolDefinition>? tools = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // System message maps to Instructions; all others become input items.
        string? instructions = null;
        var inputItems = new List<ResponseItem>();

        foreach (var msg in messages)
        {
            if (msg.Role == AIRole.System)
            {
                instructions = msg.Content;
                continue;
            }

            foreach (var item in MapToResponseItems(msg))
                inputItems.Add(item);
        }

        var opts = BuildOptions(instructions, tools);
        foreach (var item in inputItems)
            opts.InputItems.Add(item);

        AsyncCollectionResult<StreamingResponseUpdate> stream;
        try
        {
            stream = _client.CreateResponseStreamingAsync(opts, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new AIProviderException("Failed to start OpenAI Responses streaming request.", ex);
        }

        var doneEmitted = false;
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
                        $"OpenAI Responses streaming failed: {ex.Message}", ex);
                }

                if (!hasNext) break;

                var update = enumerator.Current;

                if (update is StreamingResponseOutputTextDeltaUpdate textDelta
                    && !string.IsNullOrEmpty(textDelta.Delta))
                {
                    yield return new TextChunkEvent(textDelta.Delta);
                }
                else if (update is StreamingResponseCompletedUpdate completed)
                {
                    // Emit one ToolCallCompletedEvent per function call in the output.
                    foreach (var item in completed.Response.OutputItems)
                    {
                        if (item is FunctionCallResponseItem fc)
                        {
                            yield return new ToolCallCompletedEvent(
                                fc.CallId,
                                fc.FunctionName,
                                fc.FunctionArguments.ToString());
                        }
                    }

                    yield return new StreamDoneEvent();
                    doneEmitted = true;
                }
                else if (update is StreamingResponseFailedUpdate failed)
                {
                    throw new AIProviderException(
                        $"OpenAI Responses failed: {failed.Response.Error?.Message ?? "Unknown error"}");
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (!doneEmitted)
            yield return new StreamDoneEvent();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static IEnumerable<ResponseItem> MapToResponseItems(AIMessage msg)
    {
        switch (msg.Role)
        {
            case AIRole.User:
                yield return ResponseItem.CreateUserMessageItem(msg.Content ?? string.Empty);
                break;

            case AIRole.Assistant when msg.ToolCalls is { Count: > 0 }:
                // An assistant turn that produced tool calls may also have text content.
                // In the Responses API, text and function calls are separate output items.
                if (!string.IsNullOrEmpty(msg.Content))
                    yield return ResponseItem.CreateAssistantMessageItem(msg.Content);

                foreach (var tc in msg.ToolCalls)
                    yield return ResponseItem.CreateFunctionCallItem(
                        tc.Id,
                        tc.FunctionName,
                        BinaryData.FromString(tc.ArgumentsJson));
                break;

            case AIRole.Assistant:
                yield return ResponseItem.CreateAssistantMessageItem(msg.Content ?? string.Empty);
                break;

            case AIRole.Tool:
                yield return ResponseItem.CreateFunctionCallOutputItem(
                    msg.ToolCallId!,
                    msg.Content ?? string.Empty);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(msg), $"Unsupported AIRole: {msg.Role}");
        }
    }

    private CreateResponseOptions BuildOptions(string? instructions, IList<AIToolDefinition>? tools)
    {
        var opts = new CreateResponseOptions
        {
            Model = _settings.Model,
            MaxOutputTokenCount = _settings.MaxTokens
        };

        if (instructions is not null)
            opts.Instructions = instructions;

        if (!string.IsNullOrWhiteSpace(_settings.ReasoningEffort))
        {
            var level = _settings.ReasoningEffort.ToLowerInvariant() switch
            {
                "low"    => ResponseReasoningEffortLevel.Low,
                "medium" => ResponseReasoningEffortLevel.Medium,
                "high"   => ResponseReasoningEffortLevel.High,
                _        => (ResponseReasoningEffortLevel?)null
            };

            if (level is not null)
                opts.ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = level.Value
                };
        }
        else
        {
            opts.Temperature = _settings.Temperature;
        }

        if (tools is { Count: > 0 })
        {
            foreach (var tool in tools)
            {
                opts.Tools.Add(ResponseTool.CreateFunctionTool(
                    functionName: tool.Name,
                    functionDescription: tool.Description,
                    functionParameters: BinaryData.FromString(tool.ParametersJsonSchema.GetRawText()),
                    strictModeEnabled: false));
            }
        }

        return opts;
    }
}

#pragma warning restore OPENAI001

