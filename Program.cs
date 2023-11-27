using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;
using System.Reflection;
using TypeTransferCli;

if (args.Length == 0)
{
    var versionString = Assembly.GetEntryAssembly()?
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                            .InformationalVersion
                            .ToString();

    Console.WriteLine($"TypeTransfer v{versionString}");
    Console.WriteLine("-------------\n");
    Console.WriteLine("使用說明:");
    Console.WriteLine("     參數1: 要轉換的namespace");
    Console.WriteLine("     參數2: 要輸出的路徑");
    return;
}

var parameter_1 = args[0];
var parameter_2 = args[1];

var currentDirectory = Directory.GetCurrentDirectory();
string csprojFile = ParseCsprojExtension.FindCsprojFile(currentDirectory);
if (string.IsNullOrWhiteSpace(csprojFile))
{
    Console.WriteLine("錯誤: 目前路徑找不到.csproj檔案");
    return;
}

var projectDirectory = Path.GetDirectoryName(csprojFile);
Console.WriteLine(projectDirectory);
Console.WriteLine("======================");
// Specify the target namespace
string targetNamespace = parameter_1;

// Get all C# class files in the project
var classFiles = ParseCsprojExtension.GetCSharpFiles(projectDirectory);
foreach (var classFile in classFiles)
{
    Console.WriteLine($"{classFile}");
}
Console.WriteLine("======================");

// Collect types in the specified namespace
var typesInNamespace = new List<Type>();

foreach (var classFile in classFiles)
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(classFile));
    var root = tree.GetRoot();

    var namespaceDeclarations = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();

    foreach (var namespaceDeclaration in namespaceDeclarations)
    {
        //Console.WriteLine($"namespaceDeclaration ==> {namespaceDeclaration.Name.ToString()}");
        if (namespaceDeclaration.Name.ToString().StartsWith(targetNamespace))
        {
            var classDeclarations = namespaceDeclaration.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDeclaration in classDeclarations)
            {
                // Get the Type for each class
                var className = classDeclaration.Identifier.Text;
                //Console.WriteLine($"{className}");
                var fullClassName = $"{namespaceDeclaration.Name}.{className}";
                //Console.WriteLine($"{fullClassName}");
                // !!這邊要改，需要載入目標DLL!!
                // https://stackoverflow.com/questions/61084393/how-to-get-the-type-of-a-user-input-expression-using-roslyn
                // https://stackoverflow.com/questions/64365731/how-to-reference-another-dll-in-roslyn-dynamically-compiled-code
                var type = Type.GetType(fullClassName);

                if (type != null)
                {
                    typesInNamespace.Add(type);
                }
            }
        }
    }
}

foreach (var type in typesInNamespace)
{
    Console.WriteLine($"{type.Name}");
}

var allTypesWithNestedList = new List<Type>();
typesInNamespace.ForEach(x =>
{
    allTypesWithNestedList.AddRange(TypeTransferExtension.GetAllNestedTypes(x));
});
var csTypeList = allTypesWithNestedList.Union(typesInNamespace).Distinct().ToList();
foreach (var csType in csTypeList)
{
    Console.WriteLine(csType.FullName);
}

Console.WriteLine("======================");

var resultDictionary = new Dictionary<string, string>();
foreach (var type in csTypeList)
{
    var jsType = TypeTransferExtension.ConvertCsToJs(type);
    var filePath = Path.Combine(projectDirectory, parameter_2, jsType.FilePath);
    Console.WriteLine(filePath);
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

foreach (var result in resultDictionary)
{
    Console.WriteLine(result.Key + " : " + result.Value);
}
Console.WriteLine("======================");