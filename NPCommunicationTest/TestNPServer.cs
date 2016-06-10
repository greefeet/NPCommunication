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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NPCommunication;
using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace NPCommunicationTest
{
    [TestClass]
    public class TestNPServer
    {
        static SemaphoreSlim Queue = new SemaphoreSlim(1);
        static int Counter = 0;
        [TestMethod]
        public void NPServer01_StartStop()
        {
            Trace.WriteLine("NPServer01_StartStop " + Counter++.ToString());
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";

            //NO Test Pipe
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            NPServer server = new NPServer(PipeName, VerifyMessage);

            Assert.AreEqual(server.PipeName, PipeName);
            Assert.AreEqual(server.IsRunning, false);

            //NO Test Pipe
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            server.Start();
            Assert.AreEqual(server.IsRunning, true);
            Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));

            server.Stop().Wait();
            Assert.AreEqual(server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            Trace.WriteLine("END NPServer01_StartStop " + Counter++.ToString());
        }

        [TestMethod]
        public void NPServer02_DuplicateStart()
        {
            Trace.WriteLine("NPServer02_DuplicateStart " + Counter++.ToString());
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";

            //NO Test Pipe
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            using (NPServer Server1 = new NPServer(PipeName, VerifyMessage))
            {
                Assert.AreEqual(Server1.PipeName, PipeName);
                

                //NO Test Pipe
                Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
                Assert.AreEqual(Server1.IsRunning, false);

                Server1.Start();
                Assert.AreEqual(Server1.IsRunning, true);
                Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));

                using (NPServer Server2 = new NPServer(PipeName, VerifyMessage))
                {
                    Assert.AreEqual(Server2.PipeName, PipeName);
                    Assert.AreEqual(Server2.IsRunning, false);

                    Server2.Start();
                    Assert.AreEqual(Server2.IsRunning, false);
                                        
                    Server2.Stop().Wait();
                }
                Server1.Stop().Wait();
                Assert.AreEqual(Server1.IsRunning, false);
            }
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            Trace.WriteLine("END NPServer02_DuplicateStart " + Counter++.ToString());
        }
        
        [TestMethod]
        public void NPServer03_Command()
        {
            Trace.WriteLine("NPServer03_Command " + Counter++.ToString());
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";

            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            using (NPServer Server = new NPServer(PipeName, VerifyMessage))
            {
                Server.UpdateCommand("3Arabe", args => { return "Fedfe"; });
                Server.UpdateCommand("3ONE", args => { return "1"; });
                Server.UpdateCommand("3Thai", args => { return "ไทย"; });
                Server.UpdateCommand("3ไทย", args => { return "Thai ไทย"; });
                Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
                Server.Start();

                Assert.AreEqual(Server.IsRunning, true);
                Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));

                NPClient Client = new NPClient(PipeName, VerifyMessage);
                Assert.AreEqual(Client.Get("3Arabe"), "Fedfe");

                Assert.AreEqual(Server.IsRunning, true);
                Assert.AreEqual(Client.Get("3ONE"), "1");

                Assert.AreEqual(Server.IsRunning, true);


                Assert.AreEqual(Client.Get("3Thai"), "ไทย");
                Assert.AreEqual(Server.IsRunning, true);
                Assert.AreEqual(Client.Get("3ไทย"), "Thai ไทย");
                Assert.AreEqual(Server.IsRunning, true);

                //Update command
                Server.UpdateCommand("3ONE", (o) => { return "New ONE"; });
                Assert.AreEqual(Client.Get("3ONE"), "New ONE");
                Assert.AreEqual(Server.IsRunning, true);

                Server.Stop().Wait();
                Assert.AreEqual(Server.IsRunning, false);
                Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            }
            Trace.WriteLine("END NPServer03_Command " + Counter++.ToString());
        }

        [TestMethod]
        public void NPServer04_MissingCommand()
        {
            Trace.WriteLine("NPServer04_MissingCommand " + Counter++.ToString());
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";

            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            using (NPServer Server = new NPServer(PipeName, VerifyMessage))
            {
                string Result4Arabe = "Fedfe";
                string Result4One = "1";
                Server.UpdateCommand("4Arabe", args => { return Result4Arabe; });
                Server.UpdateCommand("4ONE", args => { return Result4One; });
                Server.Start();
                Assert.AreEqual(Server.IsRunning, true);

                NPClient Client = new NPClient(PipeName, VerifyMessage);

                string Result = Client.Get("4Arabe");
                Assert.AreEqual(Result, Result4Arabe);

                Result = Client.Get("4ONE");
                Assert.AreEqual(Result, Result4One);

                bool MissingMethodException = false;
                try
                {
                    Client.Get("Fedfe");
                }
                catch(MissingMethodException)
                {
                    MissingMethodException = true;
                }
                Assert.IsTrue(MissingMethodException);
                Server.Stop().Wait();
            }
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            Trace.WriteLine("END NPServer04_MissingCommand " + Counter++.ToString());
        }

        [TestMethod]
        public void NPServer05_VerifyMessageIncurrect()
        {
            Trace.WriteLine("NPServer05_VerifyMessageIncurrect " + Counter++.ToString());
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            string VerifyFailMessage = "ArabeMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            using (NPServer Server = new NPServer(PipeName, VerifyMessage))
            {
                Server.Start();
                Assert.AreEqual(Server.IsRunning, true);
                NPClient Client = new NPClient(PipeName, VerifyFailMessage);
                bool UnauthorizedAccessException = false;
                try
                {
                    Client.Get("Fedfe");
                }
                catch(UnauthorizedAccessException)
                {
                    UnauthorizedAccessException = true;
                }
                Assert.IsTrue(UnauthorizedAccessException);
                Server.Stop().Wait();
            }
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            Trace.WriteLine("END NPServer05_VerifyMessageIncurrect " + Counter++.ToString());
        }

        [TestMethod]
        public void NPServer06_RemoveCommand()
        {
            Trace.WriteLine("NPServer06_RemoveCommand " + Counter++.ToString());
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            using (NPServer Server = new NPServer(PipeName, VerifyMessage))
            {
                Server.UpdateCommand("Arabe", args => { return "Fedfe"; });
                Server.UpdateCommand("ONE", args => { return "1"; });
                Server.Start();
                Assert.AreEqual(Server.IsRunning, true);

                NPClient Client = new NPClient(PipeName, VerifyMessage);
                Assert.AreEqual(Client.Get("Arabe"), "Fedfe");
                Assert.AreEqual(Client.Get("ONE"), "1");

                Server.RemoveCommand("ONE");

                bool MissingMethodException = false;
                try
                {
                    Client.Get("ONE");
                }
                catch (MissingMethodException)
                {
                    MissingMethodException = true;
                }
                Assert.IsTrue(MissingMethodException);
            }
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            Trace.WriteLine("END NPServer06_RemoveCommand " + Counter++.ToString());
        }

        [TestMethod]
        public void NPServer07_ClientMissing()
        {
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
            NPClient Client = new NPClient(PipeName, VerifyMessage);
            bool MissingMethodException = false;
            try
            {
                Client.Get("Fedfe");
            }
            catch (TimeoutException)
            {
                MissingMethodException = true;
            }
            Assert.IsTrue(MissingMethodException);


            NPServer Server = new NPServer(PipeName, VerifyMessage);
            Server.UpdateCommand("Fedfe", args => { return "Arabe"; });
            Server.Start();
            Assert.AreEqual(Server.IsRunning, true);
            Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));

            Assert.AreEqual(Client.Get("Fedfe"), "Arabe");

            Server.Stop().Wait();
            Server.Dispose();
            Assert.AreEqual(Server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
        }

        [TestMethod]
        public void NPServer08_Networking()
        {
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            NPServer Server = new NPServer(PipeName, VerifyMessage);
            Server.UpdateCommand("Fedfe", args => { return "Arabe"; });
            Server.Start();
            Assert.AreEqual(Server.IsRunning, true);
            Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));


            NPClient Client = new NPClient(Environment.MachineName, PipeName, VerifyMessage);
            Assert.AreEqual(Client.Get("Fedfe"), "Arabe");

            Server.Stop().Wait();
            Server.Dispose();
            Assert.AreEqual(Server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
        }
        [TestMethod]
        public void NPServer09_GetCustomContract()
        {
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            NPServer Server = new NPServer(PipeName, VerifyMessage);
            CustomContract FromServer = new CustomContract() { Key = "This is Custom", Value = "Custom Value" };
            Server.UpdateCommand("Custom", args => { return FromServer; });
            Server.Start();

            Assert.AreEqual(Server.IsRunning, true);
            Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));

            NPClient Client = new NPClient(Environment.MachineName, PipeName, VerifyMessage);
            CustomContract result = Client.Get<CustomContract>("Custom");
            Assert.AreEqual(result, FromServer);

            Server.Stop().Wait();
            Server.Dispose();
            Assert.AreEqual(Server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
        }
        #region CustomContract
        [DataContract]
        public class CustomContract : object
        {
            [DataMember(Order = 1)]
            public string Key { get; set; }
            [DataMember(Order = 2)]
            public string Value { get; set; }

            public override bool Equals(object obj)
            {
                CustomContract p = obj as CustomContract;
                return Equals(p);
            }
            public bool Equals(CustomContract p)
            {
                return p.Key == Key && p.Value == Value;
            }
            public override int GetHashCode()
            {
                return Key.GetHashCode() ^ Value.GetHashCode();
            }
        }
        #endregion

        [TestMethod]
        public void NPServer10_GetWithArgs()
        {
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            NPServer Server = new NPServer(PipeName, VerifyMessage);
            Server.UpdateCommand("Add", args => {
                int First = NPConvertor.ToInt(args[0]);
                int Second = NPConvertor.ToInt(args[1]);
                return First + Second;
            });
            Server.UpdateCommand("Custom", args => {
                string KEY = NPConvertor.ToString(args[0]);
                string VALUE = NPConvertor.ToString(args[1]);
                CustomContract custom = NPConvertor.To<CustomContract>(args[2]);
                return custom.Key == KEY && custom.Value == VALUE;
            });
            Server.Start();

            NPClient Client = new NPClient(Environment.MachineName, PipeName, VerifyMessage);
            Assert.AreEqual(Client.Get<int>("Add",1,2), 3);
            Assert.IsFalse(Client.Get<bool>("Custom", "YES", "SIR", new CustomContract() { Key = "YES", Value = "ARABE" }));
            Assert.IsTrue(Client.Get<bool>("Custom", "YES", "SIR", new CustomContract() { Key = "YES", Value = "SIR" }));

            Server.Stop().Wait();
            Server.Dispose();
            Assert.AreEqual(Server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
        }
        
        [TestMethod]
        public void NPServer11_NodeHub()
        {
            string PipeName = "Test";
            string VerifyMessage = "TestMessage";
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));

            NPServer Server = new NPServer(PipeName, VerifyMessage);

            string ServerName = "Arabe";
            int ServerNumber = 1;
            Server.Sync("Name", ServerName);
            Server.Sync("Number", ServerNumber);
            Server.Start();
            
            NPClient Client = new NPClient(PipeName, VerifyMessage);
            string ClientName = default(string);
            int ClientNumber = default(int);
            Client.Subscribe<string>("Name", v => {
                ClientName = v;
            } );
            Client.Subscribe<int>("Number", v => ClientNumber = v );

            Task.Delay(100).Wait();

            //Test First Subscribe Data
            Assert.AreEqual(ClientName, ServerName);            //Arabe = Arabe
            Assert.AreEqual(ClientNumber, ServerNumber);        //1 = 1

            string ServerName2 = "Fedfe";
            int ServerNumber2 = 2;

            Server.Sync("Name", ServerName2);
            Server.Sync("Number", ServerNumber2);

            Task.Delay(100).Wait();

            //Test First Change Data
            Assert.AreEqual(ClientName, ServerName2);           //Fedfe = Fedfe
            Assert.AreEqual(ClientNumber, ServerNumber2);       //2 = 2

            Client.Unsubscribe("Number");
            Task.Delay(100).Wait();
            string ServerName3 = "Yes Sir";

            Server.Sync("Name", ServerName3);
            Task.Delay(100).Wait();
            Server.Sync("Number", 3);
            
            Assert.AreEqual(ClientName, ServerName3);           //Yes Sir = Yes Sir
            Assert.AreEqual(ClientNumber, ServerNumber2);       //2 = 2

            Client.Unsubscribe("Name");

            Server.Sync("Name", "NotThings");

            Assert.AreEqual(ClientName, ServerName3);           //Yes Sir = Yes Sir

            Server.Stop().Wait();
            Server.Dispose();
            Assert.AreEqual(Server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
        }
    }
}
