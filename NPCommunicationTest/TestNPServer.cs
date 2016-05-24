using Microsoft.VisualStudio.TestTools.UnitTesting;
using NPCommunication;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

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

            server.Stop();
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
                                        
                    Server2.Stop();
                }
                Server1.Stop();
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
                Server.UpdateCommand<NPCommand>("3Arabe", () => { return "Fedfe"; });
                Server.UpdateCommand<NPCommand>("3ONE", () => { return "1"; });
                Server.UpdateCommand<NPCommand>("3Thai", () => { return "ไทย"; });
                Server.UpdateCommand<NPCommand>("3ไทย", () => { return "Thai ไทย"; });
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
                Server.UpdateCommand<NPCommand>("3ONE", () => { return "New ONE"; });
                Assert.AreEqual(Client.Get("3ONE"), "New ONE");
                Assert.AreEqual(Server.IsRunning, true);

                Server.Stop();
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
                Server.UpdateCommand<NPCommand>("4Arabe", () => { return Result4Arabe; });
                Server.UpdateCommand<NPCommand>("4ONE", () => { return Result4One; });
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
                Server.Stop();
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
                Server.Stop();
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
                Server.UpdateCommand<NPCommand>("Arabe", () => { return "Fedfe"; });
                Server.UpdateCommand<NPCommand>("ONE", () => { return "1"; });
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
            Server.UpdateCommand<NPCommand>("Fedfe",() => { return "Arabe"; });
            Server.Start();
            Assert.AreEqual(Server.IsRunning, true);
            Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));

            Assert.AreEqual(Client.Get("Fedfe"), "Arabe");

            Server.Stop();
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
            Server.UpdateCommand<NPCommand>("Fedfe", () => { return "Arabe"; });
            Server.Start();
            Assert.AreEqual(Server.IsRunning, true);
            Assert.IsTrue(NPServer.IsNamedPipeOpen(PipeName));


            NPClient Client = new NPClient(System.Environment.MachineName, PipeName, VerifyMessage);
            Assert.AreEqual(Client.Get("Fedfe"), "Arabe");

            Server.Stop();
            Server.Dispose();
            Assert.AreEqual(Server.IsRunning, false);
            Assert.IsFalse(NPServer.IsNamedPipeOpen(PipeName));
        }
    }
}
