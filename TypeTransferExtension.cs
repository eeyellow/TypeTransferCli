using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using CommonToolLibrary;

namespace TypeTransferCli
{
    internal static class TypeTransferExtension
    {
        private static readonly Type[] _nonPrimitivesExcludeList =
        {
            typeof(object), typeof(string), typeof(decimal), typeof(void),
        };

        private static readonly IDictionary<Type, string> _convertedTypes = new Dictionary<Type, string>
        {
            [typeof(string)] = "string",
            [typeof(char)] = "string",
            [typeof(byte)] = "number",
            [typeof(sbyte)] = "number",
            [typeof(short)] = "number",
            [typeof(ushort)] = "number",
            [typeof(int)] = "number",
            [typeof(uint)] = "number",
            [typeof(long)] = "number",
            [typeof(ulong)] = "number",
            [typeof(float)] = "number",
            [typeof(double)] = "number",
            [typeof(decimal)] = "number",
            [typeof(bool)] = "boolean",
            [typeof(object)] = "any",
            [typeof(void)] = "void",
            [typeof(StringValues)] = "string",
            [typeof(DateTime)] = "datetime",
            [typeof(DateTime?)] = "datetime",
        };

        private static readonly IDictionary<string, string> _typeInitVal = new Dictionary<string, string>
        {
            ["string"] = "\"\"",
            ["number"] = "0",
            ["boolean"] = "false",
            ["void"] = "null",
            ["datetime"] = "\"\"",
            ["any"] = "new Object()",
        };

        /// <summary> 產生Script類型 </summary>
        public enum GenerateScriptType
        {
            /// <summary> All </summary>
            [Description("All")]
            All = 0,
            /// <summary> Javascript </summary>
            [Description("Javascript")]
            Javascript = 1,
            /// <summary> Typescript </summary>
            [Description("Typescript")]
            Typescript = 2,
        }

        /// <summary> 產生Script </summary>
        /// <param name="rootPath"></param>
        /// <param name="scriptType"></param>
        public static Dictionary<string, string> Generate(string rootPath, GenerateScriptType scriptType = GenerateScriptType.Javascript)
        {
            var resultDictionary = new Dictionary<string, string>();

            // 清除舊檔案
            var folderPath = Path.Combine(rootPath, "TypeModules");
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }

            var assemblies = Assembly.GetExecutingAssembly();
            var viewModelsToConvert = GetViewModelsToConvert(assemblies).ToList();
            var allTypesWithNestedList = new List<Type>();
            viewModelsToConvert.ForEach(x =>
            {
                allTypesWithNestedList.AddRange(GetAllNestedTypes(x));
            });

            var csTypeList = allTypesWithNestedList.Union(viewModelsToConvert).Distinct().ToList();
            var enumTypeList = GetEnumsToConvert(assemblies).ToList();

            #region TypeScript未完成

            //if (scriptType == GenerateScriptTypeEnum.All || scriptType == GenerateScriptTypeEnum.Typescript)
            //{
            //    #region 產生TS
            //    foreach (Type type in typesToConvert)
            //    {
            //        var tsType = ConvertCs2Ts(type);
            //        var fullPath = Path.Combine(path, tsType.Name);

            //        var directory = Path.GetDirectoryName(fullPath);
            //        if (!Directory.Exists(directory))
            //        {
            //            Directory.CreateDirectory(directory);
            //        }

            //        File.WriteAllLines(fullPath, tsType.Lines);
            //    }
            //    #endregion 產生TS
            //}

            #endregion

            if (scriptType is GenerateScriptType.All or GenerateScriptType.Javascript)
            {
                #region 產生 ViewModels Javascript

                foreach (var type in csTypeList)
                {
                    var jsType = ConvertCsToJs(type);
                    var filePath = Path.Combine(folderPath, jsType.FilePath);
                    var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    if (!resultDictionary.ContainsKey(filePath))
                    {
                        File.WriteAllLines(filePath, jsType.Lines);
                        resultDictionary.TryAdd(filePath, jsType.FilePath);
                    }
                }

                #endregion

                #region 產生 Enums Javascript

                var enumPath = Path.Combine(folderPath, "Enums");
                var enumFullPath = Path.Combine(enumPath, "EnumType.js");
                var enumDirectory = Path.GetDirectoryName(enumFullPath) ?? string.Empty;
                if (!Directory.Exists(enumDirectory))
                {
                    Directory.CreateDirectory(enumDirectory);
                }

                var enumTextList = new List<string>
                {
                    "/* ===== 此檔案是自動產生 ===== */", "/* ===== 請勿手動變更修改 ===== */", ""
                };
                foreach (var type in enumTypeList)
                {
                    var enumLines = ConvertEnum2Js(type);
                    enumTextList.AddRange(enumLines);
                }

                File.WriteAllLines(enumFullPath, enumTextList);
                resultDictionary.TryAdd(enumFullPath, enumFullPath);

                #endregion
            }

            return resultDictionary;
        }

        private static (bool, Type) ReplaceByGenericArgument(Type type)
        {
            if (type.IsArray)
            {
                return (true, type.GetElementType());
            }

            if (!type.IsConstructedGenericType)
            {
                return (true, type);
            }

            var genericArgument = type.GenericTypeArguments.First();

            var isTask = type.GetGenericTypeDefinition() == typeof(Task<>);
            //var isActionResult = type.GetGenericTypeDefinition() == typeof(ActionResult<>);
            var isEnumerable = typeof(IEnumerable<>).MakeGenericType(genericArgument).IsAssignableFrom(type);

            if (!isTask && /*!isActionResult &&*/ !isEnumerable)
            {
                return (false, type);
            }

            if (genericArgument.IsConstructedGenericType)
            {
                return ReplaceByGenericArgument(genericArgument);
            }

            return (true, genericArgument);
        }
        private static IEnumerable<Type> GetViewModelsToConvert(Assembly assembly)
        // !x.IsGenericType: 排除泛型
        // x.IsNestedPublic: 把FvmLogin底下的也納進來
        // x.GetProperties().Length > 0 || x.GetFields().Length > 0: 排除沒有欄位的(FvmLogin)
            => assembly.GetTypes().Where(x => !x.IsAbstract && (x.IsPublic || x.IsNestedPublic) && !x.IsGenericType &&
                                              (x.GetProperties().Length > 0 || x.GetFields().Length > 0) && (x.Namespace ?? string.Empty).Contains(".ViewModels"))
                       .ToList()
                       .Select(ReplaceByGenericArgument)
                       .Where(t => t.Item1)
                       .Select(t => t.Item2)
                       .Where(t => !t.IsPrimitive && !_nonPrimitivesExcludeList.Contains(t))
                       .Distinct()
                       .ToList();
        private static IEnumerable<Type> GetEnumsToConvert(Assembly assembly)
            => assembly.GetTypes().Where(x => !x.IsAbstract && (x.IsPublic || x.IsNestedPublic) && !x.IsGenericType &&
                                              (x.GetProperties().Length > 0 || x.GetFields().Length > 0) && (x.Namespace ?? string.Empty).Contains(".Enums"))
                       .ToList()
                       .Select(ReplaceByGenericArgument)
                       .Where(t => t.Item1)
                       .Select(t => t.Item2)
                       .Where(t => !t.IsPrimitive && !_nonPrimitivesExcludeList.Contains(t))
                       .Distinct()
                       .ToList();
        private static (string FileName, string FilePath) TransferTypeToFileName(Type type, string extension = "js")
        {
            var fileName = (type.FullName ?? string.Empty).Split(".").LastOrDefault() ?? string.Empty;
            fileName = fileName.Contains('+') ? fileName.Replace('+', '_') : fileName;
            return (fileName, $@"{(type.Namespace?.Replace(".", @"\") ?? "EmptyNameSpace")}\{fileName}.{extension}".Replace("Template\\", string.Empty));
        }
        public static Type[] GetAllNestedTypes(Type type, List<Type> allNestedTypesForProperty = null)
        {
            allNestedTypesForProperty ??= new List<Type>();
            if (!allNestedTypesForProperty.Contains(type))
            {
                allNestedTypesForProperty.Add(type);
            }

            foreach (var propertyInfo in type.GetProperties())
            {
                if (!_convertedTypes.ContainsKey(propertyInfo.PropertyType))
                {
                    if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GenericTypeArguments.Any())
                    {
                        foreach (var propertyType in propertyInfo.PropertyType.GenericTypeArguments)
                        {
                            if (!allNestedTypesForProperty.Contains(propertyType))
                            {
                                allNestedTypesForProperty.AddRange(GetAllNestedTypes(propertyType, allNestedTypesForProperty));
                            }
                        }
                    }
                    else if (!allNestedTypesForProperty.Contains(propertyInfo.PropertyType))
                    {
                        allNestedTypesForProperty.AddRange(GetAllNestedTypes(propertyInfo.PropertyType, allNestedTypesForProperty));
                    }
                }
            }
            var result = new[]
                         {
                             type
                         }
                         .Concat(allNestedTypesForProperty)
                         .Where(a => !_convertedTypes.ContainsKey(a) && a.IsClass)
                         .Distinct()
                         .ToArray();

            return result;
        }
        private static Type GetArrayOrEnumerableType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            if (type.IsConstructedGenericType)
            {
                var typeArgument = type.GenericTypeArguments.First();

                if (typeof(IEnumerable<>).MakeGenericType(typeArgument).IsAssignableFrom(type))
                {
                    return typeArgument;
                }
            }
            return null;
        }
        private static Type GetNullableType(Type type)
        {
            if (type.IsConstructedGenericType)
            {
                var typeArgument = type.GenericTypeArguments.First();

                if (typeArgument.IsValueType && typeof(Nullable<>).MakeGenericType(typeArgument).IsAssignableFrom(type))
                {
                    return typeArgument;
                }
            }
            return null;
        }
        private static string ConvertType(Type typeToUse)
        {
            if (_convertedTypes.TryGetValue(typeToUse, out var convertType))
            {
                return convertType;
            }

            if (typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var keyType = typeToUse.GenericTypeArguments[0];
                var valueType = typeToUse.GenericTypeArguments[1];
                return $"{{ [key: {ConvertType(keyType)}]: {ConvertType(valueType)} }}";
            }
            return typeToUse.Name;
        }
        private static void OutputJsClass(List<string> lines, Type type, string fileName)
        {
            var constructorTemp = new List<string>();

            lines.Add("/**");

            var classSummary = type.GetSummary().Trim();
            if (!string.IsNullOrWhiteSpace(classSummary))
            {
                lines.Add($" * {classSummary}");
            }

            lines.Add(" * @class");
            lines.Add(" */");
            lines.Add($"class {fileName} {{");

            var publicProperties = type.GetProperties().Where(p => p.GetMethod?.IsPublic ?? false);
            foreach (var property in publicProperties)
            {
                var propType = property.PropertyType;
                var arrayType = GetArrayOrEnumerableType(propType);
                var nullableType = GetNullableType(propType);
                var typeToUse = nullableType ?? arrayType ?? propType;

                var convertedType = ConvertType(typeToUse);

                // 遇到 Func 會出錯， 先做排除字元
                if (convertedType.Contains("Func"))
                {
                    convertedType = convertedType.Replace("`", string.Empty);
                }

                var suffix = "";
                suffix = arrayType != null ? "[]" : suffix;
                suffix = nullableType != null ? "|null" : suffix;

                if (suffix == "[]")
                {
                    constructorTemp.Add($"        this.{property.Name} = {suffix};");
                    constructorTemp.Add($"        this.ModelError__{property.Name} = '';");
                }
                else
                {
                    if (_typeInitVal.ContainsKey(convertedType))
                    {
                        constructorTemp.Add(_typeInitVal.TryGetValue(convertedType, out var convertedTypeValue) ? $"        this.{property.Name} = {convertedTypeValue};" : $"        this.{property.Name};");
                        constructorTemp.Add($"        this.ModelError__{property.Name} = '';");
                    }
                    else if (convertedType.Contains("{ [key:"))
                    {
                        constructorTemp.Add($"        this.{property.Name} = new Object();");
                        constructorTemp.Add($"        this.ModelError__{property.Name} = '';");
                    }
                    else
                    {
                        constructorTemp.Add($"        this.{property.Name} = new {convertedType}();");
                        constructorTemp.Add($"        this.ModelError__{property.Name} = '';");
                    }
                }

                lines.Add("    /**");

                var propertySummary = property.GetSummary().Trim();
                if (!string.IsNullOrWhiteSpace(propertySummary))
                {
                    lines.Add($"     * {propertySummary}");
                }

                lines.Add($"     * @type {{{convertedType}{suffix}}}");
                lines.Add("     */");
                lines.Add($"    {property.Name};");
            }

            lines.Add("");
            lines.Add("    /** 建構式 */");
            lines.Add("    constructor () {");
            lines.AddRange(constructorTemp);
            lines.Add("    }");
            lines.Add("}");
        }
        private static void OutputJsEnum(ICollection<string> lines, Type type, string fileName)
        {
            var enumDictionary = EnumMapTool.ParseToDictionary(type);

            lines.Add("/**");

            var classSummary = type.GetSummary().Trim();
            if (!string.IsNullOrWhiteSpace(classSummary))
            {
                lines.Add($" * {classSummary}");
            }

            lines.Add(" * @enum");
            lines.Add(" */");

            lines.Add($"const {fileName} = Object.freeze({{");
            foreach (var key in enumDictionary.Keys)
            {
                lines.Add($"    {enumDictionary[key].Item1}: Object.freeze({{");
                var desc = enumDictionary[key].Item2;
                // 如果沒有註解 就抓 displayName、display(name)、summary
                if (string.IsNullOrWhiteSpace(desc))
                {
                    desc = type.GetField(enumDictionary[key].Item1)?.GetCustomAttributes<DisplayNameAttribute>().FirstOrDefault()?.DisplayName.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(desc))
                    {
                        desc = type.GetField(enumDictionary[key].Item1)?.GetCustomAttributes<DisplayAttribute>().FirstOrDefault()?.Name?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(desc))
                        {
                            desc = type.GetField(enumDictionary[key].Item1)?.GetSummary().Trim() ?? string.Empty;
                        }
                    }
                }
                lines.Add($"        Name: `{desc}`,");
                lines.Add($"        Value: {key},");
                lines.Add("    }),");
            }

            lines.Add("})");
        }
        public static (string FilePath, string[] Lines) ConvertCsToJs(Type type)
        {
            var fileNameInfo = TransferTypeToFileName(type);
            var lines = new List<string>
            {
                "/* ===== 此檔案是自動產生 ===== */",
                "/* ===== 請勿手動變更修改 ===== */",
                ""
            };

            if (type.IsClass || type.IsInterface)
            {
                OutputJsClass(lines, type, fileNameInfo.FileName);
            }
            else if (type.IsEnum)
            {
                OutputJsEnum(lines, type, fileNameInfo.FileName);
            }
            else
            {
                throw new InvalidOperationException();
            }

            lines.Add("export {");
            lines.Add($"    {fileNameInfo.FileName}");
            lines.Add("}");

            return (fileNameInfo.FilePath, lines.ToArray());
        }
        private static IEnumerable<string> ConvertEnum2Js(Type type)
        {
            var lines = new List<string>();
            if (type.IsEnum)
            {
                OutputJsEnum(lines, type, type.Name);
            }
            else
            {
                throw new InvalidOperationException();
            }
            lines.Add("");

            lines.Add("export {");
            lines.Add($"    {type.Name}");
            lines.Add("}");
            lines.Add("");

            return lines;
        }





        private static (string Name, string[] Lines) ConvertCs2Ts(Type type)
        {
            var fileNameInfo = TransferTypeToFileName(type);
            var filename = $@"{(type.Namespace?.Replace(".", @"\") ?? "EmptyNameSpace")}\{fileNameInfo.FileName}.d.ts";

            var types = GetAllNestedTypes(type);

            var lines = new List<string>();

            foreach (var t in types)
            {
                lines.Add($"");

                if (t.IsClass || t.IsInterface)
                {
                    OutputTsClass(lines, t);
                }
                else if (t.IsEnum)
                {
                    ConvertTsEnum(lines, t);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            return (filename, lines.ToArray());
        }
        private static void OutputTsClass(ICollection<string> lines, Type type)
        {
            lines.Add($"/**");

            var classSummary = type.GetSummary().Trim();
            if (!string.IsNullOrWhiteSpace(classSummary))
            {
                lines.Add($" * {classSummary}");
            }

            lines.Add(" * @class");
            lines.Add(" */");
            lines.Add($"interface {type.Name} {{");

            var publicProperties = type.GetProperties().Where(p => p.GetMethod?.IsPublic ?? false);
            foreach (var property in publicProperties)
            {
                var propType = property.PropertyType;
                var arrayType = GetArrayOrEnumerableType(propType);
                var nullableType = GetNullableType(propType);

                var typeToUse = nullableType ?? arrayType ?? propType;


                var convertedType = ConvertType(typeToUse);

                var suffix = "";
                suffix = arrayType != null ? "[]" : suffix;
                suffix = nullableType != null ? "|null" : suffix;

                lines.Add($"    /**");

                var propertySummary = property.GetSummary().Trim();
                if (!string.IsNullOrWhiteSpace(propertySummary))
                {
                    lines.Add($"     * {propertySummary}");
                }

                lines.Add($"     * @type {{{convertedType}{suffix}}}");
                lines.Add("     */");
                lines.Add($"    {property.Name}: {convertedType}{suffix};");
            }

            lines.Add($"}}");
        }
        private static void ConvertTsEnum(ICollection<string> lines, Type type)
        {
            var enumValues = type.GetEnumValues().Cast<int>().ToArray();
            var enumNames = type.GetEnumNames();

            lines.Add($"export enum {type.Name} {{");

            for (var i = 0; i < enumValues.Length; i++)
            {
                lines.Add($"  {enumNames[i]} = {enumValues[i]},");
            }

            lines.Add($"}}");
        }
    }
}
