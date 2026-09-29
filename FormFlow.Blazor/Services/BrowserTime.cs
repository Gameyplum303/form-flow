using Microsoft.JSInterop;

namespace FormFlow.Blazor.Services
{
    /// <summary>
    /// Converts between UTC and the browser's local time. The server only learns the browser's time zone
    /// through JavaScript, once the page is interactive.
    /// </summary>
    public static class BrowserTime
    {
        /// <summary>Minutes from the browser's local time to UTC, as JavaScript's getTimezoneOffset reports them.</summary>
        public static ValueTask<int> GetTimezoneOffsetAsync(this IJSRuntime js) => js.InvokeAsync<int>("formFlow.timezoneOffset");

        /// <summary>A local time in the browser's time zone, as UTC.</summary>
        public static DateTime ToUtc(DateTime local, int timezoneOffset) =>
            DateTime.SpecifyKind(local.AddMinutes(timezoneOffset), DateTimeKind.Utc);

        /// <summary>A UTC time, as local time in the browser's time zone.</summary>
        public static DateTime ToLocal(DateTime utc, int timezoneOffset) =>
            DateTime.SpecifyKind(utc.AddMinutes(-timezoneOffset), DateTimeKind.Unspecified);
    }
}
