using System.Net;
using System.Text.Json;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// The messages the API services share, and how they read the API's error responses.
    /// </summary>
    internal static class ApiErrors
    {
        public const string Unreachable = "Could not reach the server. Please try again.";
        public const string NotYours = "You can only change surveys and questions you created.";

        /// <summary>Runs a request, returning <paramref name="whenUnreachable"/> if the API can't be reached.</summary>
        public static async Task<T> TryAsync<T>(Func<Task<T>> request, T whenUnreachable)
        {
            try
            {
                return await request();
            }
            catch (HttpRequestException)
            {
                return whenUnreachable;
            }
        }

        /// <summary>Sends a change to the API: success, or the reason it failed.</summary>
        public static Task<(bool Success, string? Error)> SendAsync(Func<Task<HttpResponseMessage>> send) =>
            TryAsync<(bool, string?)>(async () =>
            {
                var response = await send();
                return response.IsSuccessStatusCode ? (true, null) : (false, await ReadMessageAsync(response));
            }, (false, Unreachable));

        /// <summary>What went wrong with a failed request, in words to show the user.</summary>
        public static async Task<string> ReadMessageAsync(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return NotYours;
            }

            var body = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(body) ? $"Request failed ({(int)response.StatusCode})" : Describe(body);
        }

        /// <summary>
        /// Pulls the human-readable message out of the API's error shapes:
        /// { "error": "..." }, { "errors": ["..."] }, or a bare JSON string.
        /// </summary>
        public static string Describe(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                {
                    return root.GetString()!;
                }
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString()!;
                    }
                    if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                    {
                        return string.Join(" ", errors.EnumerateArray().Select(e => e.ToString()));
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON; show the body as is.
            }
            return body;
        }

        /// <summary>Reads the "errors" object of a validation problem response, keyed by field name.</summary>
        public static async Task<Dictionary<string, string[]>> ReadFieldErrorsAsync(HttpResponseMessage response)
        {
            var errors = new Dictionary<string, string[]>();
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("errors", out var fields)
                    && fields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in fields.EnumerateObject().Where(f => f.Value.ValueKind == JsonValueKind.Array))
                    {
                        errors[field.Name] = field.Value.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => e.GetString()!)
                            .ToArray();
                    }
                }
            }
            catch (JsonException)
            {
                // Not a validation problem; the caller shows a general message.
            }
            return errors;
        }
    }
}
