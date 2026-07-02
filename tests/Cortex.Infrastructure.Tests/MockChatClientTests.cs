using System.ComponentModel;
using Cortex.Infrastructure.Ai;
using Microsoft.Extensions.AI;

namespace Cortex.Infrastructure.Tests;

/// <summary>
/// Locks in the behavior the chatbot depends on when no real AI provider is configured: the mock client
/// must stream non-empty assistant text and report token usage, so the whole chat pipeline (streaming,
/// usage tracking, AG-UI) works out of the box.
/// </summary>
public sealed class MockChatClientTests
{
    [Description("Summarize spending.")]
    private static string SummarizeSpending() => "ok";

    [Description("Record a transaction.")]
    private static string RecordTransaction(string description, decimal amount) => $"recorded {description} {amount}";

    [Description("List tasks.")]
    private static string ListTasks() => "ok";

    [Description("Add a task.")]
    private static string AddTask(string title) => $"added {title}";

    private static readonly ChatMessage[] Conversation =
        [new(ChatRole.User, "How much did I spend on groceries?")];

    [Fact]
    public async Task Streaming_ProducesAssistantText_AndUsage()
    {
        var client = new MockChatClient();

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(Conversation))
        {
            updates.Add(update);
        }

        var text = string.Concat(updates.Select(u => u.Text));
        Assert.False(string.IsNullOrWhiteSpace(text));
        // The reply echoes the user's question so it is visibly contextual.
        Assert.Contains("groceries", text, StringComparison.OrdinalIgnoreCase);

        var usage = updates
            .SelectMany(u => u.Contents)
            .OfType<UsageContent>()
            .Single()
            .Details;
        Assert.True(usage.TotalTokenCount > 0);
        Assert.True(usage.InputTokenCount > 0);
        Assert.True(usage.OutputTokenCount > 0);
    }

    [Fact]
    public async Task Streaming_ListsAvailableTools()
    {
        var client = new MockChatClient();
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(SummarizeSpending)] };

        var text = string.Empty;
        await foreach (var update in client.GetStreamingResponseAsync(Conversation, options))
        {
            text += update.Text;
        }

        Assert.Contains(nameof(SummarizeSpending), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponse_ReturnsTextAndUsage()
    {
        var client = new MockChatClient();

        var response = await client.GetResponseAsync(Conversation);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.NotNull(response.Usage);
        Assert.True(response.Usage!.TotalTokenCount > 0);
    }

    [Fact]
    public async Task Streaming_EmitsToolCall_WhenUserAsksToUseATool()
    {
        var client = new MockChatClient();
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(SummarizeSpending)] };
        var messages = new[] { new ChatMessage(ChatRole.User, "Please use a tool to help me.") };

        var calls = new List<FunctionCallContent>();
        await foreach (var update in client.GetStreamingResponseAsync(messages, options))
        {
            calls.AddRange(update.Contents.OfType<FunctionCallContent>());
        }

        // The mock actually drives a tool call through the real pipeline — not just listing names.
        var call = Assert.Single(calls);
        Assert.Equal(nameof(SummarizeSpending), call.Name);
    }

    [Fact]
    public async Task Streaming_SynthesizesRequiredArguments_FromToolSchema()
    {
        var client = new MockChatClient();
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(RecordTransaction, name: "record_transaction")] };
        // "record" shares a token with the tool name, so it is selected.
        var messages = new[] { new ChatMessage(ChatRole.User, "record this for me") };

        FunctionCallContent? call = null;
        await foreach (var update in client.GetStreamingResponseAsync(messages, options))
        {
            call ??= update.Contents.OfType<FunctionCallContent>().FirstOrDefault();
        }

        Assert.NotNull(call);
        Assert.Equal("record_transaction", call!.Name);
        Assert.NotNull(call.Arguments);
        Assert.True(call.Arguments!.ContainsKey("description"));
        Assert.True(call.Arguments!.ContainsKey("amount"));
    }

    [Fact]
    public async Task Streaming_FillsRequiredNumberArgument_FromTheMessage()
    {
        var client = new MockChatClient();
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(RecordTransaction, name: "record_transaction")] };
        var messages = new[] { new ChatMessage(ChatRole.User, "Record a 250 MXN dinner expense") };

        FunctionCallContent? call = null;
        await foreach (var update in client.GetStreamingResponseAsync(messages, options))
        {
            call ??= update.Contents.OfType<FunctionCallContent>().FirstOrDefault();
        }

        Assert.NotNull(call);
        // The first required string gets the message; the required number gets the number from it (not a 1 placeholder).
        Assert.Equal("Record a 250 MXN dinner expense", call!.Arguments!["description"]);
        Assert.Equal(250d, (double)call.Arguments!["amount"]!);
    }

    [Fact]
    public async Task Streaming_DistinguishesSimilarlyNamedTools_ByThePrompt()
    {
        var client = new MockChatClient();
        // The Tasks template's two tools share the "task"/"tasks" stem; the mock must still pick the right
        // one for each suggested prompt, or the build-a-module tutorial's "see it work" demo would call the
        // wrong tool. (list_tasks scores on "list"+"tasks"; add_task on "task" — singular vs plural separates them.)
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(ListTasks, name: "list_tasks"),
                AIFunctionFactory.Create(AddTask, name: "add_task"),
            ],
        };

        Assert.Equal("list_tasks", await FirstToolCallNameAsync(client, options, "List my tasks"));
        Assert.Equal("add_task", await FirstToolCallNameAsync(client, options, "Add a task to buy groceries"));
    }

    private static async Task<string?> FirstToolCallNameAsync(MockChatClient client, ChatOptions options, string message)
    {
        var messages = new[] { new ChatMessage(ChatRole.User, message) };
        await foreach (var update in client.GetStreamingResponseAsync(messages, options))
        {
            var call = update.Contents.OfType<FunctionCallContent>().FirstOrDefault();
            if (call is not null)
            {
                return call.Name;
            }
        }

        return null;
    }

    [Fact]
    public async Task Streaming_SummarizesToolResult_WithoutCallingAnotherTool()
    {
        var client = new MockChatClient();
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create(SummarizeSpending)] };
        // History after a tool already ran this turn (what FunctionInvokingChatClient re-invokes us with).
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Please use a tool."),
            new(ChatRole.Assistant, [new FunctionCallContent("mock-SummarizeSpending", nameof(SummarizeSpending), null)]),
            new(ChatRole.Tool, [new FunctionResultContent("mock-SummarizeSpending", "You spent $42 on groceries.")]),
        };

        var calls = new List<FunctionCallContent>();
        var text = string.Empty;
        await foreach (var update in client.GetStreamingResponseAsync(history, options))
        {
            text += update.Text;
            calls.AddRange(update.Contents.OfType<FunctionCallContent>());
        }

        Assert.Empty(calls); // must not loop into a second tool call
        Assert.Contains(nameof(SummarizeSpending), text, StringComparison.Ordinal);
        Assert.Contains("groceries", text, StringComparison.OrdinalIgnoreCase); // echoes the tool's result
    }
}
