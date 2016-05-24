using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static NPCommunication.NPServer;

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
        }
        public NPClient(string ServerName,string PipeName,string VerifyMessage)
        {
            this.ServerName = ServerName;
            this.PipeName = PipeName;
            this.VerifyMessage = VerifyMessage;
        }
        public string Get(string Command)
        {
            string Result = "";

            NamedPipeClientStream pipeClient = null;
            try
            {
                pipeClient = new NamedPipeClientStream(ServerName.Length==0? "." : ServerName, PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                pipeClient.Connect(500);

                var Sender = SerializationContext.Default.GetSerializer<string>();
                //Verify Message
                Sender.Pack(pipeClient, VerifyMessage);
                if (ReceiveInteract(pipeClient) == NPInteract.Currect)
                {
                    //Check Command
                    Sender.Pack(pipeClient, Command);
                    //pipeClient.Send(Command);
                    if (ReceiveInteract(pipeClient) == NPInteract.Currect)
                    {
                        Sender.Pack(pipeClient, "GET");
                        Result = Sender.Unpack(pipeClient);
                    }
                    else
                    {
                        Result = "MissingCommandException";
                    }
                }
                else
                {
                    Result = "VerifyMessageException";
                }
            }
            catch (IOException)
            {
                return Get(Command);
            }
            catch (TimeoutException)
            {
                Result = "TimeoutException";
            }
            finally
            {
                pipeClient.Close();
            }

            if (Result == "TimeoutException")
            {
                throw new TimeoutException("Missing '"+ PipeName+"' server");
            }
            else if(Result == "VerifyMessageException")
            {
                throw new UnauthorizedAccessException("Verify Message Incurrect");
            }
            else if(Result == "MissingCommandException")
            {
                throw new MissingMethodException("Missing Command '" + Command + "'");
            }
            
            return Result;
        }
        
        private NPInteract ReceiveInteract(NamedPipeClientStream stream)
        {
            return (NPInteract)stream.ReadByte();
        }
    }
}
