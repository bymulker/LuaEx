using System;
using System.Collections.Generic;

namespace LuaEx
{
    public class LuaCompiler : ModuleBase
    {
        public ModuleBase Library { get; } = new ModuleBase("lib");

        public LuaCompiler(string name) : base(name)
        {
        }

        public void ClearAllModules()
        {
            Library.ClearModules();
            ClearModules();
        }

        public bool Compile(out string errMsg)
        {
            errMsg = null;

            foreach (ScriptModuleBase module in Modules)
            {
                if (!module.Compile(Library, out errMsg))
                {
                    return false;
                }
            }

            return true;

        }

        public override void CompileAsLibrary(ScriptModuleBase module, ref List<FuncNode> libFuncs, out string errMsg)
        {
            throw new NotImplementedException();
        }

    }

}
