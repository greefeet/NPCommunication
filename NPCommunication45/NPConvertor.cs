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
using System.Runtime.Serialization;

namespace NPCommunication
{
    public class NPConvertor
    {
        public static string ToString(object arg)
        {
            return To<string>(arg);
        }
        public static int ToInt(object arg)
        {
            return To<int>(arg);
        }
        public static double ToDouble(object arg)
        {
            return To<double>(arg);
        }
        public static o To<o>(object arg)
        {
            //Check argrument
            Type ObjectType = typeof(o);
            bool IsHaveDataContract = Attribute.IsDefined(ObjectType, typeof(DataContractAttribute));
            if (!IsHaveDataContract && !ObjectType.IsSerializable)
                throw new ArgumentException("'" + ObjectType.Name + "' is not serializable type");

            MessagePackSerializer<object> Converter = SerializationContext.Default.GetSerializer<object>();
            byte[] temp = Converter.PackSingleObject(arg);
            MessagePackSerializer<o> Packer = SerializationContext.Default.GetSerializer<o>();
            return Packer.UnpackSingleObject(temp);
        }
    }
}
