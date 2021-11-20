﻿using MoonSharp.Interpreter;
using MvvmHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuaEx
{
    public class FuncNode : ObservableObject
    {
        private DynValue returnValue;

        public DynValue Func { get; }

        private Closure Function => Func.Function;

        public ScriptModuleBase Module { get; }

        public DynValue ReturnValue
        {
            get => returnValue;
            private set
            {
                SetProperty(ref returnValue, value);
            }
        }

        public FuncNode(DynValue fnVal, ScriptModuleBase module)
        {
            Debug.Assert(fnVal.Type == DataType.Function &&
                         fnVal.Function != null);
            Func = fnVal;
            Module = module;
        }

        public void Update()
        {
            ReturnValue = Function.Call();
        }

    }
}
