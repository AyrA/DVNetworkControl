using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVTestMod
{
    /*
    public static class EvilHacks
    {
        public static CallResult Call(this object o, string Name, params object[] arguments)
        {
            if (o == null || string.IsNullOrEmpty(Name))
            {
                return default;
            }
            if (arguments == null)
            {
                arguments = new object[0];
            }
            var ParamTypes = arguments.Select(m => m?.GetType()).ToArray();
            var Member = o.GetType().GetMethod(Name);
            if (Member == null)
            {
                return default;
            }
            if (IsMatchingTypeChain(Member.GetParameters().Select(m => m.ParameterType).ToArray(), ParamTypes))
            {
                CallResult ret = default;
                ret.Called = true;
                ret.Result = Member.Invoke(o, arguments.Length > 0 ? arguments : null);
                return ret;
            }
            return default;
        }

        private static bool IsMatchingTypeChain(Type[] FunctionArguments, Type[] ProvidedArguments)
        {
            if (FunctionArguments == null)
            {
                return ProvidedArguments == null;
            }
            if (ProvidedArguments == null)
            {
                return false;
            }
            if (FunctionArguments.Length != ProvidedArguments.Length)
            {
                return false;
            }
            for (var i = 0; i < FunctionArguments.Length; i++)
            {
                if (ProvidedArguments[i] != null && ProvidedArguments[i] != FunctionArguments[i])
                {
                    return false;
                }
            }
            return true;
        }
    }

    public struct CallResult
    {
        public bool Called;
        public object Result;
    }
    //*/
}
