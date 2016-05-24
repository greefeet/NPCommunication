using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NPCommunication
{
    public class NPExecute
    {
        public object Execute(string Name, params object[] args)
        {
            Type thisType = this.GetType();
            MethodInfo theMethod = thisType.GetMethod(Name);
            return theMethod.Invoke(this, args);
        }
    }
}