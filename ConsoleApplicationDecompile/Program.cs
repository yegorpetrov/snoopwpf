using ICSharpCode.ILSpy;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using ICSharpCode.ILSpy.TextView;

namespace ConsoleApplicationDecompile
{
    class Program
    {
        static void Main(string[] args)
        {
            var thisType = typeof(Program);
            var method = thisType.GetMethod("GetMethodDefinition");

            var source = GetSourceOfMethod(method);

            Console.WriteLine(source);
            Console.WriteLine("press enter");
            Console.ReadLine();
            //LoadedAssembly assembly;
            //assembly.AssemblyDefinition.Modules[0];
            //TypeReference typeReference = new TypeReference("ConsoleApplicationDecompile", "Program", ModuleDefinition
            //MethodDefinition definition = new MethodDefinition("TestMethod", MethodAttributes.Public, TypeReference
        }

        public static string GetSourceOfMethod(MethodInfo methodInfo)
        {
            var methodDefinition = GetMethodDefinition(methodInfo);

            AvalonEditTextOutput textOutput = new AvalonEditTextOutput();
            CSharpLanguage language = new CSharpLanguage();
            var options = new DecompilationOptions();
            language.DecompileMethod(methodDefinition, textOutput, options);

            var source = textOutput.GetSource();

            return source;

        }

        private static bool GetTypeDefinitionForName(TypeDefinition typeDefinition, string name, out TypeDefinition typeDefinitionToCreate)
        {

            if (typeDefinition.Name == name)
            {
                typeDefinitionToCreate = typeDefinition;
                return true;
            }

            if (typeDefinition.HasNestedTypes)
            {
                foreach (var nestedType in typeDefinition.NestedTypes)
                {
                    bool isName = GetTypeDefinitionForName(nestedType, name, out typeDefinition);
                    if (isName)
                    {
                        typeDefinitionToCreate = nestedType;
                        return true;
                    }
                }
            }
            typeDefinitionToCreate = null;
            return false;
        }

        public static MethodDefinition GetMethodDefinition(MethodInfo methodInfo)
        {
            var thisType = methodInfo.DeclaringType;
            var module = ModuleDefinition.ReadModule(thisType.Assembly.Location);

            TypeDefinition typeDefinition = null;
            foreach (var type in module.Types)
            {
                //if (type.Name == thisType.Name)
                if (GetTypeDefinitionForName(type, thisType.Name, out typeDefinition))
                {
                    //typeDefinition = type;
                    break;
                }
            }

            MethodDefinition methodDefinition = null;
            foreach (var method in typeDefinition.Methods)
            {
                if (method.Name == methodInfo.Name)
                //if (method.Name.ToUpper().Contains(methodInfo.Name.ToUpper()) || 
                //    methodInfo.Name.ToUpper().Contains(method.Name.ToUpper()))
                {
                    methodDefinition = method;
                    break;
                }
            }
            return methodDefinition;
        }

        public void TestMethod()
        {
            int sum = 0;
            for (int i = 0; i < 100; i++)
                sum += i;

            int result = sum;
        }

    }
}
