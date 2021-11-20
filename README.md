# LuaEx
A modular approach to MoonSharp eliminating memory leaks caused by wrong usage of the library

The library helps usage of the Moonsharp Lua Interpreter in a tree based modular manner. 
Once the modules are compiled, they can be run individually in recurring tasks. 
Although a LuaCompiler class is provided, modules can be compiled individually.

If you have library modules (typically these will have to be included in each of the modules)
they should be run for each including modules just for once.

You can use AddUserFunction to watch inner values while executing. for example; 
string expr = $"return ({a})";
fNode = module.AddUserFunction(expr);

Now you can call fNode.Update() and inquire its ReturnValue.

Example usage: Create your own ScriptModule class deriving from ScriptModuleBase ;
(Dont worry about CodeTypeEnum, it is just an int value to distinguish module types)

LibraryModule libModule = new LibraryModule("libModule")
            {
                Code = @"    
                -- defines a factorial function
                function fact (n)
                    if (n == 0) then
                        return 1
                    else
                        return n * fact(n - 1);
                    end
                end
                "
            };

            ScriptModule m_a = new ScriptModule("ma", CodeTypeEnum.TICK)
            {
                Code = @"a = x + 8"
            };
            ScriptModule mA = new ScriptModule("mA", CodeTypeEnum.TICK)
            {
                Code = @"return a + 4"
            };
            mA.AddModule(m_a);

            ScriptModule m_b = new ScriptModule("mb", CodeTypeEnum.TICK)
            {
                Code = @"b = fact(x)"
            };
            ScriptModule mB = new ScriptModule("mB", CodeTypeEnum.TICK)
            {
                Code = @"return b + 4"
            };
            mB.AddModule(m_b);

            LuaCompiler prc = new LuaCompiler("test");
            prc.Library.AddModule(libModule);
            prc.AddModule(mA);
            prc.AddModule(mB);

            var compiled = prc.Compile(out _);
            
            var fn = mA.Function("mA");
            var mx = prc.GetModule(fn);

            mA.RunLibrary();
            mB.RunLibrary();

            //---------------------------------------
            mA.Script.Globals["x"] = 6;
            mB.Script.Globals["x"] = 7;

            var a = mA.Run();
            var b = mB.Run();
            Console.WriteLine($"{a},{b}");

            mA.Script.Globals["x"] = 22;
            mB.Script.Globals["x"] = 4;

            a = mA.Run();
            b = mB.Run();
            Console.WriteLine($"{a},{b}");     
