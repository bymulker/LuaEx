using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Loaders;
using Serilog;
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

        public Guid ID { get; } = Guid.NewGuid();

        #region Compilation

        public bool IsCompiled { get; private set; } = false;

        private readonly List<FuncNode> LibraryFuncs = new List<FuncNode>();

        private readonly List<FuncNode> Funcs = new List<FuncNode>();

        /// <summary>
        /// Set by user for debugging purposes etc. They are run immediately after
        /// normal functs.
        /// </summary>
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
            catch (ScriptRuntimeException srex)
            {
                Log.Error(srex, "Script Error while executing module {ModuleName} : {DecMessage}"
                    , Name, srex.DecoratedMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled Error while executing module {ModuleName}", Name);
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

        protected virtual void Compile(ScriptModuleBase targetModule, ref List<FuncNode> funcs,
                                       out string errMsg)
        {
            lock (lckObj)
            {
                errMsg = null;

                try
                {
                    ScriptModuleBase m = targetModule ?? this;
                    foreach (ScriptModuleBase module in Modules)
                    {
                        module.Compile(m, ref funcs, out errMsg);
                    }

                    funcs.Add(new FuncNode(m.Script.LoadString(ManageCode(), 
                                 m.Context, CodeFriendlyName), this));

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

                    libFuncs.Add(new FuncNode(module.Script.LoadString(ManageCode(), 
                        module.Context, CodeFriendlyName), this));
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

        protected Script Script { get; private set; } = null;

        public Table Globals { get => Script.Globals; }

        protected Table Runtime { get; private set; } = null;

        public Table Context => Runtime ?? Globals;

        public SourceCode SourceCode => Script.GetSourceCode(CodeFriendlyName);

        public bool IsEmpty => string.IsNullOrEmpty(Code);

        public ScriptModuleBase(string name, int code_type, bool use_seperate_runtime, string code_friendly_name = null)
            : base(name)
        {
            CodeFriendlyName = string.IsNullOrEmpty(code_friendly_name) ?
                                $"{name}_{code_type}" : code_friendly_name;
            CodeType = code_type;            

            InitializeScript(use_seperate_runtime);
        }

        public void Clear()
        {
            InitializeScript(Runtime != null);
        }

        private void InitializeScript(bool use_seperate_runtime)
        {
            Script = new Script();
            Script.Registry["Module"] = Name;

            if (use_seperate_runtime)
            {
                Runtime = new Table(Script);
            }

            ClearCompilation();
        }

        protected virtual string ManageCode()
        {
            return Code;
        }

        public void SetModulePaths(string[] paths)
        {
            ((ScriptLoaderBase)Script.Options.ScriptLoader).ModulePaths = paths;
        }

        public void SetPrintDebugAction(Action<string> prntAction)
        {
            Script.Options.DebugPrint = prntAction;
        }

        /// <summary>
        /// Warning !: Use at one time initialization! As this routine uses [DoString] function using 
        /// in a recurring task leads to memory leak.
        /// </summary>
        /// <param name="script"></param>
        /// <param name="glbContext"></param>
        public void RegisterCode(string code)
        {
            Script.DoString(code, Context);
        }

        public DynamicExpression CreateDynamicExpression(string expr)
        {
            return Script.CreateDynamicExpression(expr);
        }

        public DynamicExpression CreateConstDynamicExpression(string expr, DynValue constant)
        {
            return Script.CreateConstantDynamicExpression(expr, constant);
        }

        public abstract void Write(XmlWriter wr, string localName, SaveOptions opts);

        public bool SameAs(ScriptModuleBase other)
        {
            if (other == null)
            {
                return false;
            }

            return CodeType == other.CodeType &&
                   Name.Equals(other.Name, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool Equals(ScriptModuleBase other)
        {
            if (other == null)
            {
                return false;
            }

            return ID.Equals(other.ID);
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
