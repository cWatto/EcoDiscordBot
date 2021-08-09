using Discord;
using Discord.WebSocket;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace EcoDiscordBot
{
    class Program
    {
        private string BOT_TOKEN = "";
        const ulong CHANNEL_ID = 874516528903647305;
        private string PATH_TO_SERVER = "";
        private string PATH_TO_NETWORK_CONFIG = "";
        
        private Process _ecoServerProcess;
        private DiscordSocketClient _client;
        private IMessageChannel _chatChannel;

        static void Main(string[] args)
            //Start App in async context
            => new Program().MainAsync(args).GetAwaiter().GetResult();
    
        public async Task MainAsync(string[] args)
        {
            /* Path to Eco and Discord Token are required */
            if(args.Length < 2 || String.IsNullOrEmpty(args[0]) || String.IsNullOrEmpty(args[1]))
            {
                Console.WriteLine("No PATH to EcoServer (arg 0) set OR no discord token set (arg 1)");
                Console.ReadKey();
                return;
            }

            BOT_TOKEN = args[1];
            PATH_TO_SERVER = args[0];
            PATH_TO_NETWORK_CONFIG = Path.Combine(PATH_TO_SERVER, "Configs", "Network.eco");

            Console.WriteLine("Discord Token: " + BOT_TOKEN);
            Console.WriteLine("Path to Eco: " + PATH_TO_SERVER);
            Console.WriteLine("Path to Server: " + Path.Combine(PATH_TO_SERVER, "EcoServer.exe"));
            Console.WriteLine("Path to Network Config: " + PATH_TO_NETWORK_CONFIG);

            Console.WriteLine("Storing Public IP into Web Server URL config");
            var publicIP = await GetPublicIP();

            WriteURLToNetworkConfig(publicIP);


            /* Launch EcoServer in a new Shell */
            _ecoServerProcess = new Process();
            //Opens seperately in new CMD shell
            _ecoServerProcess.StartInfo.UseShellExecute = true;
            _ecoServerProcess.StartInfo.FileName = Path.Combine(PATH_TO_SERVER, "EcoServer.exe");
            _ecoServerProcess.StartInfo.WorkingDirectory = PATH_TO_SERVER;
            _ecoServerProcess.Start();
            _ecoServerProcess.Refresh();

            _client = new DiscordSocketClient();
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, BOT_TOKEN);
            await _client.StartAsync();

            _client.MessageReceived += MessageReceived;

            _client.Ready += async () =>
            {
                _chatChannel = _client.GetChannel(CHANNEL_ID) as IMessageChannel;

                var localIP = await GetLocalIP();

                await _chatChannel.SendMessageAsync("---EcoBot Started---\n\n/health to see if I'm alive\n/server to get my current address\n/restart will safely restart me if i'm being a fuckhead");
                await _chatChannel.SendMessageAsync($"Local IP Address is: {localIP}\nPublic IP Address is: {publicIP}\nWeb Server: http://{publicIP}:3001/");
            };

            //Waits for the Eco Server to shutdown and restarts the computer if it does
            await _ecoServerProcess.WaitForExitAsync();

            await (_client.GetChannel(CHANNEL_ID) as IMessageChannel).SendMessageAsync("EcoServer exited, restarting PC.");
            //Restart computer here
            System.Diagnostics.Process.Start("shutdown", "/r /t 0"); 
        }

        /* Quick and dirty way to update the network config file to store the public IP into the WebServer so the poll booth will open to the correct URL */
        private void WriteURLToNetworkConfig( string publicIP )
        {
            string json = File.ReadAllText(PATH_TO_NETWORK_CONFIG);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
            jsonObj["WebServerUrl"] = $"http://{publicIP}:3001/";
            string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(PATH_TO_NETWORK_CONFIG, output);
        }

        /* Commands */
        private async Task MessageReceived(SocketMessage message)
        {
            if( message.Content.StartsWith("/") )
            {
                var command = message.Content.Substring(1);

                switch(command)
                {
                    case "restart":
                        //Simulate a force close so that the server saves.. 
                        await _chatChannel.SendMessageAsync("Saving world..");
                        Process p = Process.Start("taskkill", "/IM EcoServer.exe");
                        await p.WaitForExitAsync();
                        await _chatChannel.SendMessageAsync("World saved. (Keep trying if you don't get a exited message after this)");
                        break;
                    case "server":
                        var localIP = await GetLocalIP();
                        var publicIP = await GetPublicIP();
                        await _chatChannel.SendMessageAsync($"Local IP Address is: {localIP}\nPublic IP Address is: {publicIP}\nWeb Server: http://{publicIP}:3001/");
                        break;
                    case "health":
                        _ecoServerProcess.Refresh();
                        if( _ecoServerProcess.HasExited)
                        {
                            await _chatChannel.SendMessageAsync("The server appears to not be functioning or running... try /restart.");
                        }
                        else
                        {
                            TimeSpan difference = DateTime.Now.Subtract(_ecoServerProcess.StartTime);

                            string suffix = "";
                            long totalTime = 0;
                            if( difference.TotalSeconds < 60)
                            {
                                totalTime = difference.Seconds;
                                suffix = "seconds";
                            }else if (difference.TotalMinutes < 60)
                            {
                                totalTime = difference.Minutes;
                                suffix = "minutes";
                            }
                            else
                            {
                                totalTime = difference.Hours;
                                suffix = "hours";
                            }

                            await _chatChannel.SendMessageAsync($"Looks fine to me. Alive for {totalTime} {suffix}");
                        }
                        break;
                    default:
                        await _chatChannel.SendMessageAsync($"Not sure what you're trying to do { message.Author.ToString().Split("#")[0] }, you dumbass. ");
                        break;
                }
            }

        }
        private async Task<String> GetLocalIP()
        {
            var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
            foreach(var ip in host.AddressList)
            {
                if( ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "I have no idea, lmao.";
        }

        private async Task<String> GetPublicIP()
        {
            try
            {
                string url = "http://checkip.dyndns.org";
                System.Net.WebRequest req = System.Net.WebRequest.Create(url);
                System.Net.WebResponse resp = req.GetResponse();
                System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream());
                string response = sr.ReadToEnd().Trim();
                string[] a = response.Split(':');
                string a2 = a[1].Substring(1);
                string[] a3 = a2.Split('<');
                string a4 = a3[0];
                return a4;
            }catch(Exception e)
            {
                return "LOOKUP FAIL, ASK CONNOR OR RESTART SERVER";
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
