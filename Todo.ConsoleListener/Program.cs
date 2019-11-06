// Licensed under the MIT license. See LICENSE file in the samples root for full license information.

using Microsoft.AspNetCore.SignalR.Client;
using SignalR.Models;
using System;
using System.Threading.Tasks;

namespace Client1
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            while (true)
            {
                const string DefaultUri = "http://localhost:7071";
                Console.Write("Please enter server (Press enter for default): ");
                var url = Console.ReadLine();

                if (string.IsNullOrEmpty(url))
                {
                    url = DefaultUri;
                }

                Console.Write("Please enter station: ");
                var station = Console.ReadLine();

                var connection = new HubConnectionBuilder()
                    .WithUrl($"{url}/api")
                    .WithAutomaticReconnect()
                    .Build();

                // the string here needs to match the target put into the SignalR message in the run function
                connection.On<Song>($"currentSongChanged_{station}", song =>
                {
                    Console.WriteLine($"Change occured: {song.Title}");
                });

                Console.WriteLine("Connecting...");

                await connection.StartAsync();

                Console.WriteLine($"Connected ({connection.ConnectionId})");

                Console.ReadLine();

                await connection.DisposeAsync();

                Console.TreatControlCAsInput = false;
            }
        }
    }
}
