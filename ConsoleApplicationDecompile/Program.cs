using ICSharpCode.ILSpy;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using ICSharpCode.ILSpy.TextView;
using System.Diagnostics;

namespace ConsoleApplicationDecompile
{

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //Debugger.Launch();
                string fullAssemblyPath = null, typeName = null, methodName = null, parametersString = string.Empty;
                if (args.Length > 0)
                {
                    fullAssemblyPath = args[0];
                    typeName = args[1];
                    methodName = args[2];
                    if (args.Length == 4)
                        parametersString = args[3];
                }
                else
                {
                    var thisType = typeof(Program);
                    fullAssemblyPath = thisType.Assembly.Location;
                    typeName = thisType.Name;
                    methodName = "GetMethodDefinition";
                }

                var source = GetSourceOfMethod2(fullAssemblyPath, typeName, methodName, parametersString);
                Console.Write(source);
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }

            //var source = GetSourceOfMethod2(fullAssemblyPath, typeName, methodName);
            //var methods = thisType.GetMethods();
            //List<MethodInfo> methodInfos = new List<MethodInfo>();
            //foreach (var method in methods)
            //{
            //    if (method.Name == "GetMethodDefinition")
            //    {
            //        methodInfos.Add(method);
            //    }
            //}
            //var source = GetSourceOfMethod(methodInfos[0]);
            //var source2 = GetSourceOfMethod(methodInfos[1]);
            //Console.Write(source);

            //var thisType = typeof(Program);
            //var method = thisType.GetMethod("GetMethodDefinition");

            //var source = GetSourceOfMethod(method);

            //Console.WriteLine(source);
            //Console.WriteLine("press enter");
            //Console.ReadLine();


            //LoadedAssembly assembly;
            //assembly.AssemblyDefinition.Modules[0];
            //TypeReference typeReference = new TypeReference("ConsoleApplicationDecompile", "Program", ModuleDefinition
            //MethodDefinition definition = new MethodDefinition("TestMethod", MethodAttributes.Public, TypeReference
        }

        public static string GetSourceOfMethod(MethodInfo methodInfo)
        {
            var methodDefinition = GetMethodDefinition(methodInfo);

            var source = GetSourceOfMethod2(methodDefinition);

            return source;
        }

        public static string GetSourceOfMethod2(string fullAssemblyPath, string typeName, string methodName, string parametersString)
        {
            var methodDefinition = GetMethodDefinition(fullAssemblyPath, typeName, methodName, parametersString);

            var source = GetSourceOfMethod2(methodDefinition);

            return source;
        }

        private static string GetSourceOfMethod2(MethodDefinition methodDefinition)
        {
            AvalonEditTextOutput textOutput = new AvalonEditTextOutput();
            CSharpLanguage language = new CSharpLanguage();
            var options = new DecompilationOptions();
            language.DecompileMethod(methodDefinition, textOutput, options);

            textOutput.PrepareDocument();

            var source = textOutput.GetDocument().Text;
            //var source = textOutput.GetSource();

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
                    bool isName = GetTypeDefinitionForName(nestedType, name, out typeDefinitionToCreate);
                    if (isName)
                    {
                        return true;
                    }
                }
            }
            typeDefinitionToCreate = null;
            return false;
        }

        public static bool IsCorrectMethod(MethodDefinition method, string name, string parametersString)
        {
            if (method.Name != name)
                return false;

            var parametersStringArray = string.IsNullOrWhiteSpace(parametersString) ? new string[0] : parametersString.Split('|');

            if (parametersStringArray.Length != method.Parameters.Count)
                return false;

            for (int i = 0; i < parametersStringArray.Length; i++)
            {
                if (parametersStringArray[i].ToUpper() != method.Parameters[i].ParameterType.Name.ToUpper())
                    return false;
            }

            return true;
        }

        public static MethodDefinition GetMethodDefinition(string fullAssemblyPath, string typeName, string methodName, string parametersString)
        {
            var module = ModuleDefinition.ReadModule(fullAssemblyPath);

            TypeDefinition typeDefinition = null;
            foreach (var type in module.Types)
            {
                if (GetTypeDefinitionForName(type, typeName, out typeDefinition))
                {
                    break;
                }
            }


            MethodDefinition methodDefinition = null;
            foreach (var method in typeDefinition.Methods)
            {
                if (IsCorrectMethod(method, methodName, parametersString))
                {
                    methodDefinition = method;
                    break;
                }
            }
            return methodDefinition;
        }

        public static MethodDefinition GetMethodDefinition(MethodInfo methodInfo)
        {
            var thisType = methodInfo.DeclaringType;
            var module = ModuleDefinition.ReadModule(thisType.Assembly.Location);

            var parameters = methodInfo.GetParameters();
            string[] parametersArray = new string[parameters.Length];
            for (int i = 0; i < parametersArray.Length; i++)
            {
                parametersArray[i] = parameters[i].ParameterType.Name;
            }
            var parametersString = string.Join("|", parametersArray);

            var methodDefinition = GetMethodDefinition(thisType.Assembly.Location, methodInfo.DeclaringType.Name, methodInfo.Name, parametersString);
            return methodDefinition;

        }

    }

}
