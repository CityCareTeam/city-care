using System.Text.Json.Serialization;

namespace CityCare.Api.Dtos.Users;

public sealed class UpdatePushTokenRequest
{
    [JsonPropertyName("push_token")]
    public string PushToken { get; set; } = null!;
}
