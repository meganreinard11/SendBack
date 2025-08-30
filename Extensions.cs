using System.Text.RegularExpressions;

namespace myCar
{
    public static class Extensions
    {
        public static decimal ToDecimal(this string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;
            str = str.Trim().Replace("$", "").Replace(",", "");
            return decimal.TryParse(str, out decimal result) ? result : 0;
        }

        public static int ToInt(this string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;
            str = str.Trim().Replace("$", "").Replace(",", "");
            return int.TryParse(str, out int result) ? result : 0;
        }

        public static string Left(this string str, int length) => str.Length <= length ? str : str.Substring(0, length);
        public static string Left(this string str, char value) => str.Length <= str.IndexOf(value) ? str : str.Substring(0, str.IndexOf(value));
        public static string Left(this string str, string value) => str.Length <= str.IndexOf(value) ? str : str.Substring(0, str.IndexOf(value));

        public static string GetJsonStr(this string str, string propertyName) => str.Between("\"" + propertyName + "\":\"", "\"");

        public static string GetHtmlStr(this string str, string propertyName) => str.Between(propertyName + "\">", "<");

        public static string Between(this string str, string beforeString, string afterString)
        {
            int startAt = 0;
            return str.Between(beforeString, afterString, ref startAt);
        }

        public static string Between(this string str, string beforeString, string afterString, ref int startAt)
        {
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(beforeString) || string.IsNullOrEmpty(afterString)) return string.Empty;
            int startIndex = str.IndexOf(beforeString, startAt);
            if (startIndex == -1) return string.Empty;
            startIndex += beforeString.Length;
            int endIndex = str.IndexOf(afterString, startIndex);
            if (endIndex == -1) return string.Empty;
            if (endIndex < startIndex) return string.Empty; // Ensure end is after start
            startAt = endIndex;
            return str.Substring(startIndex, endIndex - startIndex);
        }

        public static async Task<string> GetJsonAsync(this string str, HttpClient http, CancellationToken token)
        {
            using HttpResponseMessage message = await http.GetAsync(str, token);
            return await ResolveJsonRequest(message, str, "Get");
        }

        public static async Task<string> PostJsonAsync(this string str, HttpClient http, HttpContent content, CancellationToken token)
        {
            using HttpResponseMessage message = await http.PostAsync(str, content, token);
            return await ResolveJsonRequest(message, str, "Post");
        }

        public static async Task<string> GetHtmlAsync(this string str, HttpClient http, CancellationToken token)
        {
            using HttpResponseMessage message = await http.GetAsync(str, token);
            return await ResolveHtmlRequest(message, str, "Get", token);
        }

        private static async Task<string> ResolveHtmlRequest(HttpResponseMessage message, string url, string requestType, CancellationToken token)
        {
            if (!message.IsSuccessStatusCode) throw new Exception($"Status {message.StatusCode}: Async '{requestType}' request to '{url}' has failed");
            string result = await message.Content.ReadAsStringAsync();
            return result.CleanHtml();
        }

        private static async Task<string> ResolveJsonRequest(HttpResponseMessage message, string url, string requestType)
        {
            if (!message.IsSuccessStatusCode) throw new Exception($"Status {message.StatusCode}: Async '{requestType}' request to '{url}' has failed");
            string result = await message.Content.ReadAsStringAsync();
            return result.CleanJson();
        }

        private static string CleanHtml(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty; // Remove comments
            str = Regex.Replace(str, @"&nbsp;", " ");           // Replace non-breaking spaces
            str = Regex.Replace(str, @"\s{2,}", " ");           // Remove multiple spaces
            return str.Replace("&amp;", "&")                    // Unescape ampersands
                      .Replace("&quot;", "\"")                  // Unescape quotes
                      .Replace("&lt;", "<")                     // Unescape less than
                      .Replace("&gt;", ">")                     // Unescape greater than
                      .Replace(@"<\/", "</");                   // Unescape single quotes
        }
        private static string CleanJson(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            str = Regex.Replace(str, @"\s{2,}", " ");           // Remove multiple spaces
            return str.Replace("\\\"", "\"")                    // Unescape quotes
                      .Replace("\\\\", "\\");                   // Unescape backslashes
        }
    }
}
