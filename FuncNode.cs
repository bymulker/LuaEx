using MoonSharp.Interpreter;
using MvvmHelpers;
using System;
using System.Diagnostics;

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
            try
            {
                ReturnValue = Function.Call();
            }
            catch (InterpreterException syex)
            {
                Trace.WriteLine($"Error executing module function:{syex.DecoratedMessage} at module: {Module.Name}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error executing module function:{ex.Message} at module: {Module.Name}");
            }
        }        

    }
}
