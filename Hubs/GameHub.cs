using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DrawingGame.Hubs
{
    public class GameHub:Hub
    {
        static Dictionary<string, string> users = new Dictionary<string, string>();
        private static List<string> Guessed = new List<string>();
        private static string painterId = "";
        private IHubContext<GameHub> _hubContext;
        private IWebHostEnvironment _webHostEnvironment;
        public async Task SetProfile(string nickName)
        {
            if (users.ContainsValue(nickName)) return;
            if(users.Count > 12) return;
            lock (users)
            {
                if (!users.ContainsKey(this.Context.ConnectionId))
                    users.Add(this.Context.ConnectionId, nickName);
                else
                    users[this.Context.ConnectionId] = nickName;
            }
            await this.Clients.Others.SendAsync("usersChanged", users, Guessed);
        }
        public Dictionary<string,string> GetUsers()
        {
            return users;
        }
        static Random r = new Random();
        static string[] Things = new string[] { "Banán", "Jablko", "Kočárek", "Trumpetu" };
        static List<string> ValidResponse = new List<string>();
        private static int GameIndex = 0;

        public GameHub(IHubContext<GameHub> hubContext, IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
            string wordsRaw = File.ReadAllText(webHostEnvironment.ContentRootPath +"/App_Data/words.txt");
            wordsRaw = wordsRaw.ToLower();
            Things = wordsRaw.Split(';');
            _hubContext = hubContext;
        }
        public async Task StartGame()
        {
            Guessed.Clear();
            var rand = -1;
            lock (r)
            {
                rand = r.Next(0, users.Keys.Count);
            }
            var connectionId = users.Keys.ToArray()[rand];
            painterId = connectionId;
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

            //await this.Clients.Client(connectionId).SendAsync("chatMessage", $"Nakresli {validThing}");
            await this.Clients.Client(connectionId).SendAsync("drawInfo", $"Nakresli {validThing}");
            await this.Clients.AllExcept(connectionId).SendAsync("drawInfo", $"Úhadni co {users[connectionId]} kreslí!");
            await this.Clients.AllExcept(connectionId).SendAsync("guess!");

            GameIndex++;
            StopTimer();
        }

        public async Task StopGame()
        {
            await this._hubContext.Clients.All.SendAsync("drawInfo", "Konec kola");
            await this._hubContext.Clients.All.SendAsync("gameStop");
            Guessed.Clear();
            await this._hubContext.Clients.All.SendAsync("usersChanged", users, Guessed);

        }

        private async void StopTimer()
        {
            int index = GameIndex;
            await Task.Delay(TimeSpan.FromSeconds(30));
            if(index == GameIndex)
                await StopGame();
        }

        public async Task ProcessMessage(string msg)
        {
            if (ValidResponse.Contains(msg))
            {
                await this.Clients.Caller.SendAsync("drawInfo", $"Uhodl jsi!");
                await this.Clients.Others.SendAsync("chatMessage", $"{users[this.Context.ConnectionId]} uhodl co se kresli");
                Guessed.Add(users[this.Context.ConnectionId]);
                if (Guessed.Count == users.Count - 1)
                {
                    await this.Clients.All.SendAsync("gameStop");
                    await this._hubContext.Clients.All.SendAsync("drawInfo", "Konec kola");
                    Guessed.Clear();
                }
                await this.Clients.All.SendAsync("usersChanged", users, Guessed);
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
