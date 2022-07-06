using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HelionChat
{
    class Program
    {

        static Dictionary<string, string> clients;
        public static double LastTime;
        private static NetServer server;
        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Type 'C' if u want to use application as Client, or 'S' as Server");
                string type = Console.ReadLine();
                if (type.ToLower() == "c")
                {
                    InitClient();
                    break;
                } 
                else if (type.ToLower() == "s")
                {
                    InitServer();
                    break;
                } 
                else
                {
                    Console.WriteLine("Unknown type. Try again.");
                }
            }
        }

        
        static void InitServer()
        {
            clients = new Dictionary<string, string>();
            NetPeerConfiguration config = new NetPeerConfiguration("chat");
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.MaximumConnections = 100;
            config.Port = 25565;
            server = new NetServer(config);

            double nextSendUpdates = NetTime.Now;

            server.Start();
            Log("Server started!");
            string id = "";

            while (!Console.KeyAvailable || Console.ReadKey().Key != ConsoleKey.Escape)
            {
                NetIncomingMessage message;
                while ((message = server.ReadMessage()) != null)
                {
                    switch (message.MessageType)
                    {
                        case NetIncomingMessageType.DiscoveryRequest:
                            server.SendDiscoveryResponse(null, message.SenderEndPoint);
                            break;
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.ErrorMessage:
                            Log(message.ReadString());
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
                            id = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);
                            if (status == NetConnectionStatus.Connected)
                            {
                                Log("Client with id:" + id + " connected");
                            }
                            else if (status == NetConnectionStatus.Disconnected)
                            {
                                Log("Client with id:" + id + " disconnected");
                                SendToAll($"Client {clients[id]} disconnected");
                                clients.Remove(id);
                            }

                            break;
                        case NetIncomingMessageType.ConnectionApproval:
                            string name = message.ReadString();
                            Log("Client with id:" + id + " set name to "+ name);
                            clients.Add(id, name);
                            SendToAll($"Client {name} connected");
                            message.SenderConnection.Approve();
                            break;
                        case NetIncomingMessageType.Data:
                            id = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);
                            var text = $"({clients[id]}): {message.ReadString()}";
                            SendToAll(text);
                            Log(text);
                            break;
                    }
                }
                double now = NetTime.Now;
                if (now > nextSendUpdates)
                {
                    double DeltaTime = now - LastTime;
                    LastTime = now;
                    nextSendUpdates += (1.0 / 30.0);
                }
                Thread.Sleep(1);
            }
            server.Shutdown("app exiting");
        }

        public static void SendTo(NetConnection sck, string msg)
        {
            NetOutgoingMessage om = server.CreateMessage();
            om.Write(msg);
            server.SendMessage(om, sck, NetDeliveryMethod.ReliableOrdered, 0);
        }
        public static void SendToAll(string msg)
        {
            List<NetConnection> all = server.Connections;
            if (all.Count > 0)
            {
                NetOutgoingMessage om = server.CreateMessage();
                om.Write(msg);
                server.SendMessage(om, all, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        private static NetClient socket;
        static void InitClient()
        {
            Console.Write("Enter your name: ");
            string name = Console.ReadLine();
            NetPeerConfiguration config = new NetPeerConfiguration("chat");
            config.AutoFlushSendQueue = false;
            socket = new NetClient(config);
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            socket.RegisterReceivedCallback(new SendOrPostCallback(GotMessage));

            socket.Start();
            NetOutgoingMessage hail = socket.CreateMessage(name);
            socket.Connect("127.0.0.1", 25565, hail);
            while (true)
            {
                string text = Console.ReadLine();
                if (text.ToLower() == "close")
                {
                    Disconnect();
                    break;
                }
                NetOutgoingMessage om = socket.CreateMessage(text);
                socket.SendMessage(om, NetDeliveryMethod.ReliableOrdered);
                socket.FlushSendQueue();
            }
            Console.WriteLine("Connection closed. Press any key to exit.");
            Console.ReadKey();
        }

        static void Disconnect()
        {
            socket.Disconnect("dis");
            socket.Shutdown("bye");
        }

        static void GotMessage(object peer)
        {
            NetIncomingMessage msg;
            while ((msg = socket.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.ErrorMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Log(msg.ReadString());
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
                        if (status == NetConnectionStatus.Connected)
                        {
                            Log("Connected");
                        }
                        if (status == NetConnectionStatus.Disconnected)
                        {
                            string reason = msg.ReadString();
                            Log("Disconnected from server. Reason: " + reason);
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        string Msg = msg.ReadString();
                        Log(Msg);
                        break;
                    default:
                        Log("Something going wrong...");
                        break;
                }
            }
        }

        public static void Log(string msg)
        {
            Console.WriteLine("[" + DateTime.Now + "]:" + msg);
        }
    }
}
