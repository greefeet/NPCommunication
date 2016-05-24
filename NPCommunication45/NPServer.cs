using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MsgPack.Serialization;

namespace NPCommunication
{
    public delegate string NPCommand();
    public delegate c NPCommand<c>();
    
    public class NPServer : IDisposable
    {
        public enum NPInteract : byte
        {
            Currect = 0,
            Failed = 1,
            TryAgain = 3
        }
        public bool IsRunning { get; private set; }
        public string PipeName { get; private set; }
        private string VerifyMessage;

        private NamedPipeServerStream Server;
        private NamedPipeServerStream TempServer;
        private Dictionary<string, dynamic> Command;

        public NPServer(string PipeName, string VerifyMessage)
        {
            this.PipeName = PipeName;
            this.VerifyMessage = VerifyMessage;
            Command = new Dictionary<string, dynamic>();
            RunningToken = null;
            IsRunning = false;

        }
        private CancellationTokenSource RunningToken;
        public void Start()
        {
            if (!IsRunning)
            {
                RunningToken = new CancellationTokenSource();
                int counter = 0;
                while (IsNamedPipeOpen(PipeName) && counter < 5)
                {
                    counter++;
                    Task.Delay(400);
                }
                if (counter >= 5)
                {
                    RunningToken.Cancel();
                    RunningToken.Dispose();
                    RunningToken = null;
                    IsRunning = false;
                }
                else
                {
                    Server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    Server.BeginWaitForConnection(new AsyncCallback(WaitForConnectionCallBack), Server);
                    IsRunning = true;
                }
            }
        }
        public void Stop()
        {
            if (IsRunning)
            {
                if (RunningToken != null)
                {
                    RunningToken.Cancel();
                    RunningToken.Dispose();
                    RunningToken = null;
                }
                
                int Counter = 0;
                do
                {
                    if (Server != null)
                    {
                        Server.Dispose();
                        Server = null;
                    }
                    if (TempServer != null)
                    {
                        TempServer.Dispose();
                        TempServer = null;
                    }
                    Task.Delay(400).Wait();
                    Counter++;
                } while (IsNamedPipeOpen(PipeName) && Counter < 5);
                IsRunning = false;
            }
        }
        void WaitForConnectionCallBack(IAsyncResult Result)
        {
            try
            {
                // Get the pipe
                NamedPipeServerStream pipeServer = (NamedPipeServerStream)(Result.AsyncState);
                TempServer = pipeServer;
                // End waiting for the connection
                pipeServer.EndWaitForConnection(Result);
                
                if (!RunningToken.IsCancellationRequested)
                {
                    #region NPCommunication
                    string VerifyMessage = "";
                    var Receiver = SerializationContext.Default.GetSerializer<string>();
                    VerifyMessage = Receiver.Unpack(pipeServer);
                    if (VerifyMessage == this.VerifyMessage)
                    {
                        SendInteract(pipeServer, NPInteract.Currect);
                        string Command = Receiver.Unpack(pipeServer);
                        if (this.Command.ContainsKey(Command))
                        {
                            SendInteract(pipeServer, NPInteract.Currect);
                            if (Receiver.Unpack(pipeServer) == "GET")
                            {
                                Receiver.Pack(pipeServer, ((NPCommand)this.Command[Command])());
                                pipeServer.Dispose();
                            }
                        }
                        else
                        {
                            SendInteract(pipeServer, NPInteract.Failed);
                            pipeServer.Dispose();
                        }
                    }
                    else
                    {
                        SendInteract(pipeServer, NPInteract.Failed);
                        pipeServer.Dispose();
                    }
                    #endregion
                    #region CleanCode
                    if (pipeServer != null)
                    {
                        pipeServer.Dispose();
                        pipeServer = null;
                    }

                    if (Server != null)
                    {
                        Server.Dispose();
                        Server = null;
                    }
                    #endregion
                    #region Recursively wait for the connection
                    pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    Server = pipeServer;
                    if (RunningToken!=null && !RunningToken.IsCancellationRequested)
                        pipeServer.BeginWaitForConnection(new AsyncCallback(WaitForConnectionCallBack), Server);
                    #endregion
                }

            }
            catch
            {
                //Cannot access close pipe
            }
        }
        private void SendInteract(NamedPipeServerStream stream, NPInteract Data)
        {
            stream.WriteByte((byte)Data);
            stream.Flush();
        }
        public void UpdateCommand<o>(string Key,o Command)
        {
            if (this.Command.ContainsKey(Key))
                this.Command[Key] = Command;
            else
                this.Command.Add(Key, Command);
        }
        public void RemoveCommand(string Key)
        {
            if (this.Command.ContainsKey(Key))
                this.Command.Remove(Key);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~NPServer()
        {
            Dispose(false);
        }
        private IntPtr Resource = Marshal.AllocHGlobal(100);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Free Managed Resources
                Stop();
            }

            if (Resource != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Resource);
                Resource = IntPtr.Zero;
            }
        }

        #region Helper
        public static bool IsNamedPipeOpen(string PipeName)
        {
            //File.Exists(@"\\.\pipe\" + PipeName)     //Old Style it connect PipeStream and make NPCommunication Failed
            string[] PIPES = Directory.GetFiles(@"\\.\pipe\");
            return PIPES.Count(p => p == @"\\.\pipe\" + PipeName) > 0;
        }
        #endregion
    }
}
