#region -- License Terms --
//
// NPCommunication
//
// Copyright (C) 2016 Khomsan Phonsai
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
#endregion -- License Terms --

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MsgPack.Serialization;
using System.Runtime.Serialization;

namespace NPCommunication
{
    public delegate void NPAction<c>(c Income);
    public delegate d NPAction<c, d>(c Income);
    public delegate c NPCommand<c>(object[] Argruments);

    public class NPServer : IDisposable
    {
        public enum NPInteract : byte
        {
            Currect = 0,
            Failed = 1,
            TryAgain = 3
        }
        public bool IsRunning {
            get {
                return _IsRunning;
            }
            private set {
                _IsRunning = value;
                IsRunningArgs args = new IsRunningArgs();
                args.IsRunning = value;
                IsRunningChanged?.Invoke(this, args);
            }
        }
        private bool _IsRunning;
        public event EventHandler IsRunningChanged;

        public string PipeName { get; private set; }
        private string VerifyMessage;

        private NamedPipeServerStream Server;
        private NamedPipeServerStream TempServer;
        private Dictionary<string, dynamic> Command;


        //private List<string> Subscriber;
        private Dictionary<string, List<string[]>> Subscriber;
        public NPServer(string PipeName, string VerifyMessage)
        {
            this.PipeName = PipeName;
            this.VerifyMessage = VerifyMessage;

            Subscriber = new Dictionary<string, List<string[]>>();
            SyncData = new Dictionary<string, byte[]>();
            Command = new Dictionary<string, dynamic>();
            Command.Add("Subscribe", new NPCommand<bool>(args => {
                string MachineName = NPConvertor.ToString(args[0]);
                string Channel = NPConvertor.ToString(args[1]);
                string UniqueId = NPConvertor.ToString(args[2]);

                if (Subscriber.ContainsKey(Channel))
                {
                    List<string[]> OldClient = Subscriber[Channel];
                    if(OldClient.Count(c=>c[1] == UniqueId)==0)
                    {
                        OldClient.Add(new string[] { MachineName, UniqueId });
                        Subscriber[Channel] = OldClient;
                    }
                }
                else
                {
                    List<string[]> FirstSubscribe = new List<string[]>() { new string[] { MachineName, UniqueId } };
                    Subscriber.Add(Channel, FirstSubscribe);
                }

                if (SyncData.ContainsKey(Channel))
                {
                    using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(MachineName, string.Format("{0}.{1}.{2}", PipeName, UniqueId, Channel), PipeDirection.Out, PipeOptions.Asynchronous))
                    {
                        try
                        {
                            pipeClient.Connect(500);
                            byte[] data = SyncData[Channel];
                            pipeClient.Write(data, 0, data.Length);
                            if (pipeClient.IsConnected) pipeClient.Close();
                        }
                        catch(Exception Ex)
                        {
                            if (pipeClient.IsConnected) pipeClient.Close();
                        }
                        pipeClient.Dispose();
                    }
                }
                return SyncData.ContainsKey(Channel);
            }));

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
        public async Task Stop()
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
                    await Task.Delay(1);
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
                    #region NPContract
                    var Receiver = SerializationContext.Default.GetSerializer<NPCallContract>();
                    NPCallContract ClientCall = Receiver.Unpack(pipeServer);
                    NPResultContract<object> ClientResult = new NPResultContract<object>();
                    if (ClientCall.VerifyMessage == VerifyMessage)
                    {
                        switch (ClientCall.Type)
                        {
                            case NPCalType.Get:
                                if (Command.ContainsKey(ClientCall.MethodCommand))
                                {
                                    try
                                    {
                                        ClientResult.ObjectResult = (Command[ClientCall.MethodCommand])(ClientCall.Argruments);
                                        ClientResult.Type = NPResultType.Complete;
                                    }
                                    catch
                                    {
                                        ClientResult.Type = NPResultType.InvalidOperationException;
                                    }
                                }
                                else
                                {
                                    ClientResult.Type = NPResultType.MissingCommandException;
                                }
                                break;
                        }
                    }
                    else
                    {
                        ClientResult.Type = NPResultType.VerifyMessageException;
                    }
                    var Sender = SerializationContext.Default.GetSerializer<NPResultContract<object>>();
                    Sender.Pack(pipeServer, ClientResult);
                    pipeServer.Dispose();
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
                //Happen when stop 
            }
        }
        public void UpdateCommand<o>(string Key, NPCommand<o> Command)
        {
            Type ObjectType = typeof(o);
            bool IsHaveDataContract = Attribute.IsDefined(typeof(o), typeof(DataContractAttribute));
            if (IsHaveDataContract || ObjectType.IsSerializable)
            {
                if (this.Command.ContainsKey(Key))
                    this.Command[Key] = Command;
                else
                    this.Command.Add(Key, Command);
            }
            else
            {
                throw new NotSupportedException("'" + ObjectType.Name + "' is not serializable type");
            }
        }
        public void RemoveCommand(string Key)
        {
            if (Command.ContainsKey(Key))
                Command.Remove(Key);
        }
        Dictionary<string, byte[]> SyncData;
        public void Sync<o>(string Channel, o Data)
        {
            //Check o type IsSerializable
            Type ObjectType = typeof(o);
            bool IsHaveDataContract = Attribute.IsDefined(ObjectType, typeof(DataContractAttribute));
            if (!IsHaveDataContract && !ObjectType.IsSerializable)
                throw new ArgumentException("'" + ObjectType.Name + "' is not serializable type");

            //Store Data
            MessagePackSerializer<NPData<o>> Serializer = SerializationContext.Default.GetSerializer<NPData<o>>();
            NPData<o> RawData = new NPData<o>() { VerifyMessage = VerifyMessage, Channel = Channel, Data = Data };
            byte[] ByteData = Serializer.PackSingleObject(RawData);
            if (SyncData.ContainsKey(Channel))
                SyncData[Channel] = ByteData;
            else
                SyncData.Add(Channel, ByteData);

            //Send to all subscriber
            if (Subscriber.ContainsKey(Channel))
            {
                List<string[]> Subscriber = this.Subscriber[Channel];
                //MemoryStream StreamData = new MemoryStream(ByteData);
                byte[] data = SyncData[Channel];
                //pipeClient.Write(data, 0, data.Length);
                List<string[]> Remove = new List<string[]>();
                foreach (string[] sub in Subscriber)
                {
                    using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(sub[0], string.Format("{0}.{1}.{2}", PipeName, sub[1],Channel), PipeDirection.Out, PipeOptions.Asynchronous))
                    {
                        try
                        {
                            pipeClient.Connect(500);
                            pipeClient.Write(data, 0, data.Length);
                            if (pipeClient.IsConnected) pipeClient.Close();
                        }
                        catch
                        {
                            if (pipeClient.IsConnected) pipeClient.Close();

                            Remove.Add(sub);
                        }
                        pipeClient.Dispose();
                    }
                }
                foreach(string[] sub in Remove)
                {
                    Subscriber.Remove(sub);
                }
                this.Subscriber[Channel] = Subscriber;
            }
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
                Stop().Wait();
            }

            if (Resource != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Resource);
                Resource = IntPtr.Zero;
            }
        }

        #region Helper
        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);
        [DllImport("kernel32.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        public static bool IsNamedPipeOpen(string PipeName)
        {
            //Style ONE
            //return File.Exists(@"\\.\pipe\"+PipeName);

            //Style Two
            //string[] PIPES = System.IO.Directory.GetFiles(@"\\.\pipe\");
            //return PIPES.Count(p => p == @"\\.\pipe\" + PipeName) > 0;

            WIN32_FIND_DATA data;
            IntPtr handle = FindFirstFile(@"\\.\pipe\*", out data);
            if (handle != new IntPtr(-1))
            {
                do
                    if (data.cFileName == PipeName)
                        return true;
                while (FindNextFile(handle, out data) != 0);
                FindClose(handle);
            }
            return false;
        }
        #endregion
    }
    public class IsRunningArgs : EventArgs
    {
        public bool IsRunning { get; set; }
    }
}
