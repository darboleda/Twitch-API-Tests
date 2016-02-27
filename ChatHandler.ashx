<%@ WebHandler Language="C#" Class="ChatHandler" %>

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;

using System.Diagnostics;

public class ChatHandler : IHttpHandler
{

    public void ProcessRequest(HttpContext context)
    {
        if (!context.IsWebSocketRequest)
        {
            context.Response.Write("NOT A WEBSOCKET, LOL");
            return;
        }
        context.AcceptWebSocketRequest(ManageSocketMessages);
    }

    public bool IsReusable
    {
        get
        {
            return false;
        }
    }


    public async Task ManageSocketMessages(AspNetWebSocketContext context)
    {
        WebSocket socket = context.WebSocket;
        await SendMessage(socket, "Welcome to the web!\n");

        string username = context.QueryString["username"];
        string oauth = context.QueryString["oauth"];

        await SendMessage(socket, string.Format("Username: {0} :::: OAuth: {1}\n", username, oauth));

        TwitchChat chat = new TwitchChat(username, oauth);
        await chat.Connect();

        await Task.WhenAll(Listen(socket, chat), ListenToTwitch(socket, chat));
    }
    public async Task Listen(WebSocket socket, TwitchChat chat)
    {
        while (true)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
            WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);

            if (socket.State != WebSocketState.Open) break;

            string received = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
            await SendMessage(socket, "ECHO " + received);
            await chat.Send(received);
        }
    }

    public async Task ListenToTwitch(WebSocket socket, TwitchChat chat)
    {
        await SendMessage(socket, await chat.Connect());

        while (true)
        {
            if (socket.State != WebSocketState.Open) break;
            string message = await chat.ListenAndParse();
            if (message != null) await SendMessage(socket, message);
            await Task.Delay(100);
        }
    }

    private async Task SendMessage(WebSocket socket, string message)
    {
        if (socket.State != WebSocketState.Open) return;
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
