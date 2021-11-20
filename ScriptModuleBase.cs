using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;

namespace LuaEx
{
    public abstract class ScriptModuleBase : ModuleBase, IEquatable<ScriptModuleBase>
    {
        public event EventHandler Compiled;

        private readonly object lckObj = new object();

        #region Compilation

        public bool IsCompiled { get; private set; } = false;

        private readonly List<FuncNode> LibraryFuncs = new List<FuncNode>();

        private readonly List<FuncNode> Funcs = new List<FuncNode>();

        private readonly List<FuncNode> UserFuncs = new List<FuncNode>();

        public DynValue LibraryFunction(string name)
        {
            if (LibraryFuncs.Find(fnd => fnd.Module.Name.Equals(name,
                StringComparison.InvariantCultureIgnoreCase)) is FuncNode fNode)
            {
                return fNode.Func;
            }
            return DynValue.Nil;
        }

        public DynValue Function(string name)
        {
            if (Funcs.Find(fnd => fnd.Module.Name.Equals(name,
                StringComparison.InvariantCultureIgnoreCase)) is FuncNode fNode)
            {
                return fNode.Func;
            }
            return DynValue.Nil;
        }

        public DynValue MainFuncion()
        {
            return Funcs.Count > 0 ? Function(Name) : DynValue.Nil;
        }

        public void RunLibrary(string toModule = null)
        {
            lock (lckObj)
            {
                if (toModule == null)
                {
                    LibraryFuncs.ForEach((fNode) => fNode.Update());
                }
                else
                {
                    foreach (FuncNode fNode in LibraryFuncs)
                    {
                        if (fNode.Module.Name.Equals(toModule, StringComparison.InvariantCultureIgnoreCase))
                        {
                            break;
                        }

                        fNode.Update();
                    }
                }
            }

        }

        public DynValue Run(string toModule = null)
        {
            try
            {
                if (!IsCompiled)
                {
                    throw new Exception("The module is not compiled yet!!");
                }

                DynValue ret = DynValue.Nil;
                if (toModule == null)
                {
                    Funcs.ForEach((fNode) => fNode.Update());
                    UserFuncs.ForEach((fNode) => fNode.Update());
                    ret = Funcs.Last().ReturnValue;
                }
                else
                {
                    foreach (FuncNode fNode in Funcs)
                    {
                        if (fNode.Module.Name.Equals(toModule, StringComparison.InvariantCultureIgnoreCase))
                        {
                            break;
                        }

                        fNode.Update();
                        ret = fNode.ReturnValue;
                    }
                }
                return ret;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error occurred while executing module {Name} : {ex.Message}");
            }

            return DynValue.Nil;

        }

        public bool Compile(ModuleBase Library, out string errMsg)
        {
            lock (lckObj)
            {
                try
                {
                    errMsg = null;

                    ClearCompilation();

                    List<FuncNode> libFuncs = new List<FuncNode>();
                    List<FuncNode> compiledFuncs = new List<FuncNode>();

                    //compile library modules if any..
                    if (Library != null)
                    {
                        Library.CompileAsLibrary(this, ref libFuncs, out errMsg);
                    }

                    if (errMsg != null)
                    {
                        goto xerr;
                    }

                    Compile(null, ref compiledFuncs, out errMsg);

                    if (errMsg != null)
                    {
                        goto xerr;
                    }

                    SetCompiled(libFuncs, compiledFuncs);

                    return true;
                }
                catch (SyntaxErrorException e)
                {
                    errMsg = $"Syntax Error: {e.DecoratedMessage}";
                }
                catch (ScriptRuntimeException e)
                {
                    errMsg = e.DecoratedMessage;
                }
                catch (Exception e)
                {
                    errMsg = e.Message;
                }

            xerr:
                return false;
            }
        }

        protected virtual void Compile(Script script, ref List<FuncNode> funcs,
                                       out string errMsg)
        {
            lock (lckObj)
            {
                errMsg = null;

                try
                {
                    Script s = script ?? this.Script;
                    foreach (ScriptModuleBase module in Modules)
                    {
                        module.Compile(s, ref funcs, out errMsg);
                    }

                    funcs.Add(new FuncNode(s.LoadString(ManageCode(), s.Globals, CodeFriendlyName), this));

                }
                catch (SyntaxErrorException e)
                {
                    errMsg = $"Syntax Error: {Name} - [{e.DecoratedMessage}]";
                }
                catch (ScriptRuntimeException e)
                {
                    errMsg = $"{Name} - {e.DecoratedMessage}";
                }
                catch (Exception e)
                {
                    errMsg = $"{Name} - {e.Message}";
                }
            }
        }

        public override void CompileAsLibrary(ScriptModuleBase module, ref List<FuncNode> libFuncs,
                                        out string errMsg)
        {
            lock (lckObj)
            {
                errMsg = null;

                try
                {
                    foreach (ScriptModuleBase mdl in this.Modules)
                    {
                        mdl.CompileAsLibrary(module, ref libFuncs, out errMsg);
                    }

                    Script s = module.Script;
                    libFuncs.Add(new FuncNode(s.LoadString(ManageCode(), s.Globals, CodeFriendlyName), this));
                }
                catch (SyntaxErrorException e)
                {
                    errMsg = $"Syntax Error: {Name} - [{e.DecoratedMessage}]";
                }
                catch (ScriptRuntimeException e)
                {
                    errMsg = $"{Name} - {e.DecoratedMessage}";
                }
                catch (Exception e)
                {
                    errMsg = $"{Name} - {e.Message}";
                }
            }

        }

        private void SetCompiled(List<FuncNode> libFuncs, List<FuncNode> funcs)
        {
            lock (lckObj)
            {
                LibraryFuncs.Clear();
                LibraryFuncs.AddRange(libFuncs);

                Funcs.Clear();
                Funcs.AddRange(funcs);

                IsCompiled = true;
                Compiled?.Invoke(this, null);
            }

        }

        private void ClearCompilation()
        {
            lock (lckObj)
            {
                LibraryFuncs.Clear();
                Funcs.Clear();
                UserFuncs.Clear();
                IsCompiled = false;
            }
        }

        /// <summary>
        /// User functions can be used to monitor inner variables of module while running. 
        /// </summary>
        /// <param name="expr"></param>
        /// <returns></returns>
        public FuncNode AddUserFunction(string expr)
        {
            lock (lckObj)
            {
                FuncNode fnd = new FuncNode(Script.LoadString(expr), this);
                UserFuncs.Add(fnd);
                return fnd;
            }
        }

        /// <summary>
        /// User functions can be used to monitor inner variables of module while running. 
        /// </summary>
        /// <param name="fNode"></param>
        /// <returns></returns>
        public bool RemoveUserFunction(FuncNode fNode)
        {
            return UserFuncs.Remove(fNode);
        }

        #endregion

        public ScriptModuleBase(string name, int code_type, string code_friendly_name = null)
            : base(name)
        {
            CodeFriendlyName = string.IsNullOrEmpty(code_friendly_name) ?
                                $"{name}_{code_type}" : code_friendly_name;
            CodeType = code_type;
            InitializeScript();
        }

        public string CodeFriendlyName { get; }

        public int CodeType { get; }

        private string code = "";

        public string Code
        {
            get => code;
            set
            {
                if (value == null)
                {
                    value = "";
                }

                if (SetProperty(ref code, value))
                {
                    ClearCompilation();
                }
            }
        }

        public Script Script { get; private set; } = null;

        public SourceCode SourceCode => Script.GetSourceCode(CodeFriendlyName);

        public bool IsEmpty => string.IsNullOrEmpty(Code);

        public void Clear()
        {
            InitializeScript();
        }

        private void InitializeScript()
        {
            Script = new Script();
            Script.Registry["Module"] = Name;
            ClearCompilation();
        }

        protected virtual string ManageCode()
        {
            return Code;
        }

        public abstract void Write(XmlWriter wr, string localName, SaveOptions opts);

        public bool Equals(ScriptModuleBase other)
        {
            if (other == null)
            {
                return false;
            }

            return CodeType == other.CodeType &&
                   Name.Equals(other.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override bool Equals(object obj)
        {
            ScriptModuleBase other = obj as ScriptModuleBase;
            return Equals(other);
        }        

        public static bool operator ==(ScriptModuleBase x, ScriptModuleBase y)
        {
            if (x is null)
            {
                return y is null;
            }

            return x.Equals(y);
        }

        public static bool operator !=(ScriptModuleBase x, ScriptModuleBase y)
        {
            return !(x == y);
        }

    }

}
