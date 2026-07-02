using System.Text.Json.Serialization;

namespace Cortex.AspNetCore.Channels;

/// <summary>
/// The subset of Meta's WhatsApp Business webhook delivery that Cortex consumes (field names are the
/// Cloud API's snake_case). A delivery can carry inbound messages, delivery statuses, or both; only
/// text messages are turned into agent turns.
/// </summary>
public sealed record WhatsAppWebhookPayload
{
    /// <summary>The webhook object type — <c>whatsapp_business_account</c> for Cloud API deliveries.</summary>
    [JsonPropertyName("object")]
    public string? SubscribedObject { get; init; }

    [JsonPropertyName("entry")]
    public IReadOnlyList<Entry>? Entries { get; init; }

    public sealed record Entry
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("changes")]
        public IReadOnlyList<Change>? Changes { get; init; }
    }

    public sealed record Change
    {
        [JsonPropertyName("field")]
        public string? Field { get; init; }

        [JsonPropertyName("value")]
        public ChangeValue? Value { get; init; }
    }

    public sealed record ChangeValue
    {
        [JsonPropertyName("messaging_product")]
        public string? MessagingProduct { get; init; }

        [JsonPropertyName("contacts")]
        public IReadOnlyList<Contact>? Contacts { get; init; }

        [JsonPropertyName("messages")]
        public IReadOnlyList<Message>? Messages { get; init; }
    }

    public sealed record Contact
    {
        [JsonPropertyName("wa_id")]
        public string? WaId { get; init; }

        [JsonPropertyName("profile")]
        public Profile? Profile { get; init; }
    }

    public sealed record Profile
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    public sealed record Message
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        /// <summary>The sender's phone number (E.164 without the plus).</summary>
        [JsonPropertyName("from")]
        public string? From { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("text")]
        public TextBody? Text { get; init; }
    }

    public sealed record TextBody
    {
        [JsonPropertyName("body")]
        public string? Body { get; init; }
    }
}
