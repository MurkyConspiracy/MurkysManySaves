using System;
using System.Linq;
using System.Reflection;

namespace MurkysManySaves
{
    /// <summary>Finds and invokes ES3's types at runtime, since this assembly has no compile-time reference to it.</summary>
    internal static class Es3ReflectionHandler
    {
        /// <summary>Finds a type by name, checking Assembly-CSharp first, then every loaded assembly.</summary>
        public static Type FindType(string typeName)
        {
            Type type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        public static Type FindEs3Type() => FindType("ES3");

        /// <summary>
        /// Finds a public static method matching paramTypes, tolerating trailing optional
        /// parameters (ES3 commonly has these, which Type.GetMethod can't match by itself).
        /// </summary>
        public static MethodInfo FindMethod(Type type, string methodName, params Type[] paramTypes)
        {
            MethodInfo exact = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, paramTypes, null);
            if (exact != null)
                return exact;

            foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (candidate.Name != methodName || candidate.IsGenericMethodDefinition)
                    continue;

                var parameters = candidate.GetParameters();
                if (parameters.Length < paramTypes.Length)
                    continue;

                bool matches = true;
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    if (parameters[i].ParameterType != paramTypes[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    for (int i = paramTypes.Length; i < parameters.Length; i++)
                    {
                        if (!parameters[i].IsOptional)
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (matches)
                    return candidate;
            }

            return null;
        }

        /// <summary>Invokes a method found via FindMethod, filling in Type.Missing for any omitted optional params.</summary>
        public static object Invoke(MethodInfo method, object instance, params object[] args)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == args.Length)
                return method.Invoke(instance, args);

            var fullArgs = new object[parameters.Length];
            Array.Copy(args, fullArgs, args.Length);
            for (int i = args.Length; i < parameters.Length; i++)
            {
                fullArgs[i] = Type.Missing;
            }

            return method.Invoke(instance,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.OptionalParamBinding,
                null, fullArgs, null);
        }
    }
}
