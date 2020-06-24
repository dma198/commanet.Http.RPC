using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

namespace commanet.Http.RPC
{
    public static class Utils
    {


        private static AssemblyBuilder? ab = null;

        public static ModuleBuilder? ModuleBuilder { get; private set; } = null;

        private static int id=0;
        public static void Init()
        {

            if (ab == null)
            {
                var aName = new AssemblyName(Process.GetCurrentProcess().ProcessName + "HttpRpcImpl" + id);
                if (aName == null || aName.Name == null)
                    #pragma warning disable CA1303 // Do not pass literals as localized parameters
                    throw new Exception("Can' get current process name!");
                    #pragma warning restore CA1303 // Do not pass literals as localized parameters

                AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()),
                    AssemblyBuilderAccess.Run);

                ab = AssemblyBuilder.DefineDynamicAssembly(
                                        aName, AssemblyBuilderAccess.Run);
                ModuleBuilder = ab.DefineDynamicModule(aName.Name);

                id++;
            }
        }

        public static string StripInterfaceName(string OriginalName)
        {
            if (OriginalName == null)
                throw new ArgumentNullException(nameof(OriginalName));

            if (OriginalName.StartsWith("I",StringComparison.Ordinal))
                return OriginalName.Substring(1);
            return OriginalName;
        }

        private static readonly Dictionary<string, Type> MethodParametersHolderTypesCache = new Dictionary<string, Type>();
        public static Type GetMethodRequestType(Type t, string methodName)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));
            if (ModuleBuilder == null)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("ModuleBuilder property can't be null");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters

            var m = t.GetMethod(methodName);
            if(m==null)
            {
                throw new Exception($"Can't find method with name {methodName} in type {t.Name}");
            }

            var typeName = StripInterfaceName(t.Name) + "Impl" + m.Name + "Args";
            var upTypeName = typeName.ToUpperInvariant();
            if (MethodParametersHolderTypesCache.ContainsKey(upTypeName))
                return MethodParametersHolderTypesCache[upTypeName];
            
            var tbParamsHolder = ModuleBuilder.DefineType(typeName, TypeAttributes.Public);
            if(tbParamsHolder==null)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't create parameters holder type");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters


            foreach (var p in m.GetParameters())
            {
                if (p == null || p.Name==null) continue;
                //tbParamsHolder.DefineField(p.Name, p.ParameterType, FieldAttributes.Public);
                EmitAutoProperty(tbParamsHolder, p.Name, p.ParameterType);
            }

            var res = tbParamsHolder?.CreateType();
            if (res != null)
            {
                MethodParametersHolderTypesCache.Add(upTypeName, res);
            }

            if(res==null)
            {
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't get method request type");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return res;
        }

        public static string BackingFieldName(string propertyName)
        {
            return $"<{propertyName}>k__BackingField";
        }


        private static PropertyInfo EmitAutoProperty(this TypeBuilder tb, string propertyName, Type propertyType)
        {
            var backingField = tb.DefineField(BackingFieldName(propertyName), propertyType, FieldAttributes.Private);
            var propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

            var getMethod = tb.DefineMethod("get_" + propertyName,
                                   MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,        
                                   propertyType,
                                   Type.EmptyTypes);            var getGenerator = getMethod.GetILGenerator();
            getGenerator.Emit(OpCodes.Ldarg_0);
            getGenerator.Emit(OpCodes.Ldfld, backingField);
            getGenerator.Emit(OpCodes.Ret);
            propertyBuilder.SetGetMethod(getMethod);

            var setMethod = tb.DefineMethod("set_" + propertyName,
                                   MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,        
                                   null,
                                   new [] { propertyType });            
            var setGenerator = setMethod.GetILGenerator();
            setGenerator.Emit(OpCodes.Ldarg_0);
            setGenerator.Emit(OpCodes.Ldarg_1);
            setGenerator.Emit(OpCodes.Stfld, backingField);
            setGenerator.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setMethod);

            return propertyBuilder;
        }

        private static readonly Dictionary<string, Type> MethodResponseHolderTypesCache = new Dictionary<string, Type>();
        public static Type GetMethodResponseHolderType(string methodName, Type returnType)
        {
            string typeName = methodName + "Resp";
            if (MethodResponseHolderTypesCache.ContainsKey(typeName))
                return MethodResponseHolderTypesCache[typeName];
            if (ModuleBuilder == null)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("ModuleBuilder property can't be null");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters


            TypeBuilder tbHolder = ModuleBuilder.DefineType(typeName, TypeAttributes.Public);
            if (returnType != typeof(void))
                tbHolder.DefineField("res", returnType, FieldAttributes.Public);
            tbHolder.DefineField("ServerError", typeof(string), FieldAttributes.Public);
            var res = tbHolder.CreateType();
            if(res!=null)
                MethodResponseHolderTypesCache.Add(typeName, res);
            if (res == null)
            {
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't get method response type");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            return res;
        }

        public static Type[] GetMethodParameterTypes(MethodInfo m)
        {
            if (m == null)
                throw new ArgumentNullException(nameof(m));

            List<Type> res = new List<Type>();
            foreach (var p in m.GetParameters())
                res.Add(p.ParameterType);
            return res.ToArray();
        }

    }
}