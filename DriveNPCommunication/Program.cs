using NPCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriveNPCommunication
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ReadKey();
            Console.WriteLine("Test");
            NPServer Server = new NPServer("Arabe", "Fedfe");
            int Counter = 0;
            Server.UpdateCommand<NPCommand>("Arabe", () => { return "ONE" + Counter++.ToString() ; });
            Server.Start();
            Task.Delay(5000);

            NPClient Client = new NPClient("Arabe", "Fedfe");
            Console.WriteLine("Result : " + Client.Get("Arabe"));
            Console.ReadKey();
            Console.WriteLine("Result : " + Client.Get("Arabe"));
            Console.ReadKey();
            Console.WriteLine("Result : " + Client.Get("Arabe"));
            Server.Stop();
            Console.ReadKey();
        }
    }
}
