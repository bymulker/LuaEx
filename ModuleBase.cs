using MoonSharp.Interpreter;
using MvvmHelpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LuaEx
{
    public class ModuleBase : ObservableObject
    {
        public string Name { get; }

        #region Module Hierarchy

        private readonly List<ScriptModuleBase> modules = new List<ScriptModuleBase>();

        public ModuleBase(string name)
        {
            Name = name;
        }

        public ModuleBase(string name, ScriptModuleBase[] modules) : this(name)
        {
            this.modules.AddRange(modules);
        }

        public IEnumerable<ScriptModuleBase> Modules => modules.AsEnumerable();

        public int GetIndex(ScriptModuleBase module)
        {
            return modules.IndexOf(module);
        }

        public int ModuleCount => modules.Count;

        public bool AddModule(ScriptModuleBase module)
        {
            if (!modules.Contains(module))
            {
                modules.Add(module);
                return true;
            }
            return false;
        }

        public bool RemoveModule(ScriptModuleBase module)
        {
            return modules.Remove(module);
        }

        public void ClearModules()
        {
            modules.Clear();
        }

        public ModuleBase GetModule(string name)
        {
            if (Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return this;
            }
            else
            {
                foreach (ModuleBase module in modules)
                {
                    return module.GetModule(name);
                }

                return null;
            }
        }

        public ModuleBase GetModule(DynValue fn)
        {
            string mName = (string)fn.Function.OwnerScript.Registry["Module"];
            return GetModule(mName);
        }

        public virtual void CompileAsLibrary(ScriptModuleBase module, ref List<FuncNode> libFuncs,
                                        out string errMsg)
        {
            errMsg = null;
            foreach (ScriptModuleBase libModule in Modules)
            {
                libModule.CompileAsLibrary(module, ref libFuncs, out errMsg);
            }
        }

        #endregion
    }
}
