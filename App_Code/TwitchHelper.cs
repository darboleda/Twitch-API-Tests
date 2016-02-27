using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;

using Newtonsoft.Json.Linq;

/// <summary>
/// Summary description for TwitchHelper
/// </summary>
public class TwitchHelper
{
    public static string ClientId { get; private set; }
    public static string ClientSecret { get; private set; }

    static TwitchHelper()
    {
        dynamic idData = JObject.Parse(File.ReadAllText(HttpContext.Current.Server.MapPath(@"~/App_Data/id.json")));
        ClientId = idData.ClientId;
        ClientSecret = idData.ClientSecret;
    }

    public TwitchHelper() { }

    class P : IProgress<string>
    {
        public string status = "";
        public void Report(string value)
        {
            status = value;
        }
    }

    public static T BlockOnTask<T>(Task<T> t, int timeout)
    {
        var s = AwaitTimeout<T>(t, timeout, null);
        s.Wait();
        return s.Result;
    }

    public static string NotifyCodeReceived(string code)
    {
        return BlockOnTask<string>(RequestOauthToken(code), 10000) ?? "TIMED OUT";
    }

    public static async Task<T> AwaitTimeout<T>(Task<T> task, int timeout, CancellationTokenSource cts)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) == task)
        {
            return task.Result;
        }
        if (cts != null) cts.Cancel();
        return default(T);
    }

    public static async Task<string> RequestOauthToken(string code)
    {
        HttpResponseMessage response;

        using (HttpClient client = new HttpClient())
        {

            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", "http://localhost:53099/oauth"),
            });

            response = await client.PostAsync("https://api.twitch.tv/kraken/oauth2/token", content).ConfigureAwait(false); ;
        }

        dynamic d = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return d.access_token;
    }

    public static async Task<object> GetCommands()
    {
        return (await GetRequest("https://api.twitch.tv/kraken/", string.Empty).ConfigureAwait(false))["_links"];
    }

    public static async Task<JObject> PutRequestWithOauth(string baseUrl, string command, string oauthToken, string json)
    {
        HttpResponseMessage response;
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", oauthToken);
            response = await client.PutAsJsonAsync(string.Format("{0}{1}", baseUrl, command), JObject.Parse(json)).ConfigureAwait(false); ;
        }
        return JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static async Task<JObject> GetRequestWithOauth(string baseUrl, string command, string oauthToken, params KeyValuePair<string, string>[] parameters)
    {
        HttpResponseMessage response;
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", oauthToken);
            StringBuilder builder = new StringBuilder();
            builder.Append(baseUrl);
            builder.Append(command);
            if (parameters.Length > 0)
            {
                builder.Append("?");
                builder.Append(string.Join("&", parameters.Select(x => string.Format("{0}={1}", x.Key, x.Value))));
            }

            response = await client.GetAsync(builder.ToString()).ConfigureAwait(false); ;
        }
        return JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static async Task<JObject> GetRequest(string baseUrl, string command, params KeyValuePair<string, string>[] parameters)
    {
        HttpResponseMessage response;
        using (HttpClient client = new HttpClient())
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(baseUrl);
            builder.Append(command);
            if (parameters.Length > 0)
            {
                builder.Append("?");
                builder.Append(string.Join("&", parameters.Select(x => string.Format("{0}={1}", x.Key, x.Value))));
            }

            response = await client.GetAsync(builder.ToString()).ConfigureAwait(false); ;
        }
        return JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    public static string BuildAuthUrl()
    {
        return string.Format("https://api.twitch.tv/kraken/oauth2/authorize" +
            "?response_type=code" +
            "&client_id={0}" +
            "&redirect_uri={1}" +
            "&scope={2}",
            ClientId, "http://localhost:53099/oauth",
            "channel_read channel_subscriptions channel_check_subscription channel_editor chat_login"
            );
    }
}
