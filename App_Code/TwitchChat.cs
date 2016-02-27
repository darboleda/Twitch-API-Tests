using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Net.WebSockets;

using System.IO;

using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

public class TwitchMessage
{
    public struct EmoteInfo
    {
        public int Id;
        public int StartingIndex;
        public int EndingIndex;
    }

    public string Color;
    public string DisplayName;

    public bool IsMod;
    public bool IsSubscriber;
    public bool IsTurbo;

    public int UserId;
    public string UserType;
    public string UserName;

    public string Channel;

    public string MessageRaw;

    [System.NonSerialized]
    public List<object> Message;
}

/// <summary>
/// Summary description for TwitchChat
/// </summary>
public class TwitchChat
{
    private string username;
    private string oauth;

    public TwitchChat(string username, string oauth)
    {
        this.username = username;
        this.oauth = oauth;
    }

    TcpClient client;
    CancellationTokenSource cts;

    public async Task<string> Connect()
    {
        if (client != null) client.Close();
        cts = new CancellationTokenSource();

        client = new TcpClient();

        Debug.WriteLine("Opening up web socket");

        try
        {
            await client.ConnectAsync("irc.twitch.tv", 6667);
            stream = client.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream);

            await writer.WriteLineAsync("PASS oauth:" + oauth);
            await writer.WriteLineAsync("NICK " + username);
            await writer.FlushAsync();

            await writer.WriteLineAsync("CAP REQ :twitch.tv/commands");
            await writer.WriteLineAsync("CAP REQ :twitch.tv/tags");
            await writer.FlushAsync();
        }
        catch (SocketException ex)
        {
            return ex.StackTrace;
        }

        Debug.WriteLine("Web socket opened");
        return "Connected\n";
    }

    NetworkStream stream;
    StreamReader reader;
    StreamWriter writer;

    public async Task<string> Listen()
    {
        if (stream.CanRead)
        {
            return await reader.ReadLineAsync(); ;
        }
        return null;
    }

    public async Task Send(string message)
    {
        if (stream.CanWrite)
        {
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }
    }

    private const string MessagePattern =
        @"@color=(?<color>#[\dA-Fa-f]+)?;" +
        @"display-name=(?<displayname>[^;]+)?;" +
        @"emotes=(?<emotes>[^;]*);" +
        @"mod=(?<mod>[01]);" +
        @"(?:room-id=[\d]+;)?" +
        @"subscriber=(?<sub>[01]);" +
        @"turbo=(?<turbo>[01]);" +
        @"user-id=(?<userid>\d+);" +
        @"user-type=(?<usertype>[^\s]*) " +
        @":(?<username>[^!]+)!\k<username>@\k<username>\.tmi\.twitch\.tv " +
        @"PRIVMSG #(?<channel>[^\s]+) " +
        @":(?<message>.*)";

    private const string SimplifiedMessagePattern =
        @"@" +
        @"(?<tags>(?:[^;]+;)*[^;\s]+) " +
        @":(?<username>[^!]+)!\k<username>@\k<username>\.tmi\.twitch\.tv " +
        @"PRIVMSG #(?<channel>[^\s]+) " +
        @":(?<message>.*)";

    private static Regex reg = new Regex(SimplifiedMessagePattern);
    public async Task<string> ListenAndParse()
    {
        string s = await Listen();
        Debug.WriteLine(s);

        if (!reg.IsMatch(s)) return s;

        TwitchMessage message = new TwitchMessage();
        Match m = reg.Match(s);

        string tagString = m.Groups["tags"].Value;
        string[] tags = tagString.Split(';');
        Dictionary<string, string> dict = new Dictionary<string, string>();

        foreach (var v in from tag in tags select tag.Split('='))
        {
            dict.Add(v[0], v[1]);
        }

        message.Color = dict.ContainsKey("color") ? dict["color"] : null;
        message.DisplayName = dict.ContainsKey("display-name") ? dict["display-name"] : null;
        message.IsMod = dict.ContainsKey("mod") ? dict["mod"] == "1" : false;
        message.IsSubscriber = dict.ContainsKey("subscriber") ? dict["subscriber"] == "1" : false;
        message.IsTurbo = dict.ContainsKey("turbo") ? dict["turbo"] == "1" : false;
        message.UserId = dict.ContainsKey("user-id") ? int.Parse(dict["user-id"]) : 0;
        message.UserType = dict.ContainsKey("user-type") ? dict["user-type"] : null;

        message.UserName = m.Groups["username"].Value;
        message.Channel = m.Groups["channel"].Value;
        message.MessageRaw = m.Groups["message"].Value;

        return JObject.FromObject(message).ToString(Newtonsoft.Json.Formatting.None);
    }
}
