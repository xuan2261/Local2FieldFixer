using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local2Field_Fixer
{
    class Program
    {
        private enum LogType
        {
            Done,
            Error
        }
        static void Log(string m, LogType Type)
        {
            switch (Type)
            {
                case LogType.Done:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("[");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Done");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("]");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(" {0}", m);
                    break;
                case LogType.Error:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("[");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("Error");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("]");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(" {0}", m);
                    break;
            }
        }
        // Discord : CursedLand#2802
        static Dictionary<string, TypeSig> CctorFieldList = new Dictionary<string, TypeSig>();
        static Dictionary<string, Local> RestoredList = new Dictionary<string, Local>();
        static ModuleDefMD Mod { set; get; }
        static TypeDef CctorType { set; get; }
        static void Main(string[] args)
        {
            // Check Module Is Loaded In Param:args
            if (args.Length == 1)
            {
                Mod = ModuleDefMD.Load(args[0]);
                CctorType = Mod.GlobalType;
                Log($"{Mod.Name} Loaded Successfly", LogType.Done);
            }
            else
            {
                Log("No Module Loaded Please Drag & Drop", LogType.Error);
                Console.ReadKey();
                return;
            }
            Fix();
            var NewName = args[0].Contains(".exe") ? args[0].Replace(".exe", " - Fixed.exe") : args[0].Replace(".dll", " - Fixed.dll");
            Save(NewName);
            Log($"Saved As {NewName}\nPress Any Key To Close ....", LogType.Done);
            Console.ReadKey();
        }
        static void Save(string NewName)
        {
            if (Mod.IsILOnly)
            {
                var Options = new ModuleWriterOptions(Mod)
                { Logger = DummyLogger.NoThrowInstance };
                Options.MetadataOptions.Flags = MetadataFlags.PreserveAll;
                Mod.Write(NewName, Options);
            }
            else
            {
                var NativeOptions = new NativeModuleWriterOptions(Mod, false)
                { Logger = DummyLogger.NoThrowInstance };
                NativeOptions.MetadataOptions.Flags = MetadataFlags.PreserveAll;
                Mod.NativeWrite(NewName, NativeOptions);
            }
        }
        static void Fix()
        {
            foreach (var FieldDef in CctorType.Fields.Where(f => f.IsStatic))
                CctorFieldList.Add(FieldDef.Name, FieldDef.FieldSig.GetFieldType());
            foreach (var TypeDef in Mod.Types.Where(t => t.HasMethods)) {
                foreach (var MethodDef in TypeDef.Methods.Where(m => m.HasBody)) {
                    IList<Instruction> IL = MethodDef.Body.Instructions;
                    for (var x = 0; x < IL.Count; x++) {
                        if (IL[x].OpCode == OpCodes.Ldsfld || IL[x].OpCode == OpCodes.Ldsflda || IL[x].OpCode == OpCodes.Stsfld && IL[x].Operand is FieldDef) {
                            var Name = ((FieldDef)IL[x].Operand).Name;
                            if (CctorFieldList.ContainsKey(Name)) {
                                TypeSig Sig = null;
                                CctorFieldList.TryGetValue(Name, out Sig);
                                var RestoredLocal = new Local(Sig, Name, 0);
                                MethodDef.Body.Variables.Add(RestoredLocal);
                                CctorType.Fields.Remove((FieldDef)IL[x].Operand);
                                if (!RestoredList.ContainsKey(Name)) {
                                    IL[x].OpCode = GetOpType(IL[x].OpCode.Code);
                                    IL[x].Operand = RestoredLocal;
                                    RestoredList.Add(Name, RestoredLocal);
                                    Log($"Restored {Sig.FullName} At {x}", LogType.Done); } else {
                                    IL[x].OpCode = GetOpType(IL[x].OpCode.Code);
                                    IL[x].Operand = RestoredList[Name];
                                    Log($"Restored {Sig.FullName} At {x}", LogType.Done); } } } } } }
        }
        static OpCode GetOpType(Code X)
        {
            // Thx 4 @drakoniа#0601 for some advice :)
            switch (X)
            {
                case Code.Stsfld:
                    return OpCodes.Stloc;
                case Code.Ldsfld:
                    return OpCodes.Ldloc;
                case Code.Ldsflda:
                    return OpCodes.Ldloca;
                default:
                    return null;
            }
        }
    }
}
