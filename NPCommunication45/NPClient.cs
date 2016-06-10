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

using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.Serialization;
using System.Threading;

namespace NPCommunication
{
    public class NPClient
    {
        public string PipeName { get; private set; }
        private string VerifyMessage, ServerName;
        public NPClient(string PipeName, string VerifyMessage)
        {
            this.PipeName = PipeName;
            this.VerifyMessage = VerifyMessage;
            this.ServerName = "";
            byte[] bytes = BitConverter.GetBytes(DateTime.Now.Ticks);
            //UniqueId = Convert.ToBase64String(bytes).Replace('+', '_').Replace('/', '-').TrimEnd('=');
            UniqueId = Convert.ToBase64String(bytes).TrimEnd('=');
            DataSyncAction = new Dictionary<string, dynamic>();
        }
        public NPClient(string ServerName,string PipeName,string VerifyMessage)
        {
            this.ServerName = ServerName;
            this.PipeName = PipeName;
            this.VerifyMessage = VerifyMessage;
            byte[] bytes = BitConverter.GetBytes(DateTime.Now.Ticks);
            UniqueId = Convert.ToBase64String(bytes).TrimEnd('=');
            DataSyncAction = new Dictionary<string, dynamic>();
        }
        public string Get(string Command,params object[] argruments)
        {
            return Get<string>(Command, argruments);
        }
        public o Get<o>(string Command, params object[] argruments)
        {
            //Check argrument
            foreach(object obj in argruments)
            {
                Type ObjectType = obj.GetType();
                bool IsHaveDataContract = Attribute.IsDefined(ObjectType, typeof(DataContractAttribute));
                if (!IsHaveDataContract && !ObjectType.IsSerializable)
                {
                    throw new ArgumentException("'" + ObjectType.Name + "' is not serializable type");
                }
            }
            

            o Result = default(o);
            NPResultType ResultType = NPResultType.Complete;
            NamedPipeClientStream pipeClient = null;
            try
            {
                pipeClient = new NamedPipeClientStream(ServerName.Length == 0 ? "." : ServerName, PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                pipeClient.Connect(500);
                var Sender = SerializationContext.Default.GetSerializer<NPCallContract>();
                NPCallContract ClientCall = new NPCallContract();
                ClientCall.VerifyMessage = VerifyMessage;
                ClientCall.MethodCommand = Command;
                ClientCall.Type = NPCalType.Get;
                ClientCall.Argruments = argruments;
                Sender.Pack(pipeClient, ClientCall);

                var Receiver = SerializationContext.Default.GetSerializer<NPResultContract<o>>();
                NPResultContract<o> ClientResult = Receiver.Unpack(pipeClient);
                switch (ClientResult.Type)
                {
                    case NPResultType.Complete:
                        Result = (o)ClientResult.ObjectResult;
                        ResultType = NPResultType.Complete;
                        break;
                    case NPResultType.InvalidOperationException:
                    case NPResultType.MissingCommandException:
                    case NPResultType.TimeoutException:
                    case NPResultType.VerifyMessageException:
                        ResultType = ClientResult.Type;
                        break;
                }
            }
            catch (TimeoutException)
            {
                ResultType = NPResultType.TimeoutException;
            }
            finally
            {
                pipeClient.Close();
                pipeClient.Dispose();
            }

            if (ResultType == NPResultType.TimeoutException)
            {
                throw new TimeoutException("Missing '" + PipeName + "' server");
            }
            else if (ResultType == NPResultType.VerifyMessageException)
            {
                throw new UnauthorizedAccessException("Verify Message Incurrect");
            }
            else if (ResultType == NPResultType.MissingCommandException)
            {
                throw new MissingMethodException("Missing Command '" + Command + "'");
            }
            else if (ResultType == NPResultType.InvalidOperationException)
            {
                throw new InvalidOperationException("Somethings error on command '" + Command + "'");
            }
            return Result;
        }
        string UniqueId;
        Dictionary<string, dynamic> DataSyncAction;
        public void Subscribe<o>(string Channel, NPAction<o> Action)
        {
            //Update DataSyncAction
            if (DataSyncAction.ContainsKey(Channel))
                DataSyncAction[Channel] = Action;
            else
                DataSyncAction.Add(Channel, Action);

            //Open NPServer Loop get Data
            MessagePackSerializer<NPData<o>> Unpacker = SerializationContext.Default.GetSerializer<NPData<o>>();
            NamedPipeServerStream pipeServer = new NamedPipeServerStream(string.Format("{0}.{1}.{2}",PipeName,UniqueId, Channel), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);    
            pipeServer.BeginWaitForConnection(new AsyncCallback(WaitForDataCallBack), new object[3] { pipeServer , Channel, Unpacker });

            bool IsHaveOldData = Get<bool>("Subscribe", Environment.MachineName, Channel, UniqueId);
        }
        void WaitForDataCallBack(IAsyncResult Result)
        {
            string Channel = null;
            NamedPipeServerStream pipeServer = null;
            dynamic Unpacker = null;
            try
            {
                object[] ObjState = (object[])Result.AsyncState;

                // Get the pipe
                pipeServer = (NamedPipeServerStream)(ObjState[0]);
                Channel = (string)ObjState[1];
                //dynamic Converter = ObjState[2];
                //var Receiver = SerializationContext.Default.GetSerializer<NPData<object>>();
                Unpacker = ObjState[2];

                // End waiting for the connection
                pipeServer.EndWaitForConnection(Result);

                //NPData<o>
                var NPData = Unpacker.Unpack(pipeServer);

                if(NPData != null && NPData.VerifyMessage == VerifyMessage && NPData.Channel == Channel && DataSyncAction.ContainsKey(Channel))
                    DataSyncAction[Channel](NPData.Data);

                //Clear Server
                if (pipeServer != null)
                {
                    pipeServer.Dispose();
                    pipeServer = null;
                }

                
            }
            catch
            {
                //Cannot access close pipe
                //Happen when stop 
            }
            if (Channel != null && Unpacker != null && DataSyncAction.ContainsKey(Channel))
            {
                pipeServer = new NamedPipeServerStream(string.Format("{0}.{1}.{2}", PipeName, UniqueId, Channel), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                pipeServer.BeginWaitForConnection(new AsyncCallback(WaitForDataCallBack), new object[3] { pipeServer, Channel, Unpacker });
            }
        }
        public void Unsubscribe(string Channel)
        {
            if (DataSyncAction.ContainsKey(Channel))
            {
                DataSyncAction.Remove(Channel);

                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", string.Format("{0}.{1}.{2}", PipeName, UniqueId, Channel), PipeDirection.Out, PipeOptions.Asynchronous))
                {
                    try
                    {
                        pipeClient.Connect(500);
                        pipeClient.Write(new byte[] { 0 }, 0, 1);
                        if (pipeClient.IsConnected) pipeClient.Close();
                    }
                    catch
                    {
                        if (pipeClient.IsConnected) pipeClient.Close();
                    }
                    pipeClient.Dispose();
                }
            }
                
        }
    }
}
