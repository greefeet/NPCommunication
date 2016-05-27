# NPCommunication
A simple way to transfer data on the same computer or over a network.

## Get Style

Transfer data between client/server objects as following:

```cs
//server.exe
NPServer server = new NPServer("ServerName", "ServerPassword");
server.UpdateCommand("Ping", ()=> { return "Pong"; });
server.UpdateCommand("1", ()=> { return "ONE"; });
server.UpdateCommand("2", ()=> { return "TWO"; });
server.Start();
Console.ReadLine();
server.Stop();
```

```cs
//client.exe
NPClient client = new NPClient("ServerName", "ServerPassword");
Console.WriteLine(client.Get("Ping"));
Console.WriteLine(client.Get("1"));
Console.WriteLine(client.Get("2"));
```
client.exe Result :
```cmd
Pong
ONE
TWO
```

## Sync Style

Binding real time data as following: 

```cs
//sync.exe
NPServer server = new NPServer("ServerName", "ServerPassword");
server.Sync("Data", "FirstSync");
server.Start();

string ClientData = null;
NPClient Client = new NPClient("ServerName", "ServerPassword");
Client.Subscribe<string>("Data", v =>  {
    Console.WriteLine("ClientData change to : " + v);
    ClientData = v;
} );

server.Sync("Data", "SecondSync");
server.Sync("Data", "ThirdSync");

Task.Delay(1000).Wait();

server.Start();
Console.ReadLine();
server.Stop();
```
sync.exe Result :
```cmd
ClientData change to : FirstSync
ClientData change to : SecondSync
ClientData change to : ThirdSync
```

## Getting Start
This project is on NUGET. To install NPCommunication, run the following command in the [Package Manager Console]

```pm
PM> Install-Package NPCommunication 
```

[Package Manager Console]: <https://www.nuget.org/packages/NPCommunication>