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

using System.Runtime.Serialization;

namespace NPCommunication
{
    [DataContract]
    public class NPCallContract
    {
        [DataMember(Order = 1)]
        public string VerifyMessage { get; set; }
        [DataMember(Order = 2)]
        public string MethodCommand { get; set; }
        [DataMember(Order = 3)]
        public NPCalType Type { get; set; }
        [DataMember(Order = 4)]
        public object[] Argruments { get; set; }
    }
    public enum NPCalType : int
    {
        Get = 0,
        Invoke = 1
    }
    [DataContract]
    public class NPResultContract<o>
    {
        [DataMember]
        public NPResultType Type { get; set; }
        [DataMember]
        public o ObjectResult { get; set; }
        public NPResultContract()
        {
            ObjectResult = default(o);
        }
    }
    public enum NPResultType : int
    {
        Complete = 0,
        TimeoutException = 1,
        VerifyMessageException = 2,
        MissingCommandException = 3,
        InvalidOperationException = 4
    }
    [DataContract]
    public class NPData<o>
    {
        [DataMember]
        public string VerifyMessage { get; set; }
        [DataMember]
        public string Channel { get; set; }
        [DataMember]
        public o Data { get; set; }
    }
}
