using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawingGame.Hubs
{
    public class GameHub:Hub
    {
        static Dictionary<string, string> users = new Dictionary<string, string>();
        public async Task SetProfile(string nickName)
        {
            lock (users)
            {
                if (!users.ContainsKey(this.Context.ConnectionId))
                    users.Add(this.Context.ConnectionId, nickName);
                else
                    users[this.Context.ConnectionId] = nickName;
            }
            await this.Clients.Others.SendAsync("usersChanged", users);
        }
        public Dictionary<string,string> GetUsers()
        {
            return users;
        }
        static Random r = new Random();
        static string[] Things = new string[] { "Banán", "Jablko", "Kočárek", "Trumpetu" };
        static List<string> ValidResponse = new List<string>();
        public async Task StartGame()
        {
            var rand = -1;
            lock (r)
            {
                rand = r.Next(0, users.Keys.Count);
            }
            var connectionId = users.Keys.ToArray()[rand];
            await this.Clients.Client(connectionId).SendAsync("draw!");
            lock (r)
            {
                rand = r.Next(0, Things.Length);
            }
            var validThing = Things[rand];
            ValidResponse.Clear();
            ValidResponse.Add(validThing);
            ValidResponse.Add(validThing.ToLower());
            ValidResponse.Add(validThing.ToUpper());
            var NormalizedValidThing = validThing.RemoveDiacritics();
            ValidResponse.Add(NormalizedValidThing);
            ValidResponse.Add(NormalizedValidThing.ToLower());
            ValidResponse.Add(NormalizedValidThing.ToUpper());

            await this.Clients.Client(connectionId).SendAsync("chatMessage", $"Nakresli {validThing}");
            await this.Clients.AllExcept(connectionId).SendAsync("guess!");
            await this.Clients.AllExcept(connectionId).SendAsync("chatMessage", "Uhodnete co to je");
        }
        public async Task ProcessMessage(string msg)
        {
            if (ValidResponse.Contains(msg))
            {
                await this.Clients.Caller.SendAsync("chatMessage", "uhodl jsi");
                await this.Clients.Others.SendAsync("chatMessage", $"{users[this.Context.ConnectionId]} uhodl co se kresli");
            }
            else
            {
                await this.Clients.All.SendAsync("chatMessage", $"{users[this.Context.ConnectionId]}: {msg}");
            }
        }
        public async Task SendLines(Point[] points)
        {
            await this.Clients.Others.SendAsync("drawLines", points);
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            lock (users)
            {
                if (users.ContainsKey(this.Context.ConnectionId))
                    users.Remove(this.Context.ConnectionId);
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    public static class StringExtensionMethods
    {
        static Dictionary<char, char> replaceDict = new Dictionary<char, char>();

        static StringExtensionMethods()
        {
            var chars = "říšžťčýůňúěďáéó"; //příliš žluťoučký kůň úpěl ďábelské ódy
            var charsReplace = "risztcyunuedaeo";
            for (int i = 0; i < chars.Length; i++)
            {
                replaceDict.Add(chars[i], charsReplace[i]);
            }
        }

        public static string RemoveDiacritics(this string text)
        {
            StringBuilder sb = new StringBuilder(text);
            for (int i = 0; i < sb.Length; i++)
            {
                if (replaceDict.ContainsKey(sb[i]))
                {
                    sb[i] = replaceDict[sb[i]];
                }
            }
            return sb.ToString();
        }
    }
}
