using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Vita3KBot {
    public static class Utils {

        // Sends an HTTP Get request. Might not be the best solution.
        public static async Task<string> HttpGet(string address, string parameters = "") {
            var client = new HttpClient{ BaseAddress = new Uri(address) };
            // Add User-Agent in header so Github API allows our requests.
            client.DefaultRequestHeaders.Add("User-Agent", "Vita3KBot");
            var response = await client.GetAsync(parameters);

            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadAsStringAsync();
                client.Dispose();
                return result;
            }

            client.Dispose();
            throw new Exception("Received " + response.StatusCode
                                        + " status code from " + address + parameters + ".");
        }

        // Discord Markdown Code.
        public static string Code(string code, string lang = "") {
            return "```" + lang + "\n" + code + "\n```";
        }
    }
}