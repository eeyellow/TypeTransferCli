using System.Xml;

namespace TypeTransferCli
{
    internal static class ParseCsprojExtension
    {
        public static string FindCsprojFile(string directory)
        {
            // Look for csproj files in the current directory and its parent directories
            while (!string.IsNullOrEmpty(directory))
            {
                string csprojFile = Directory.GetFiles(directory, "*.csproj").FirstOrDefault();

                if (csprojFile != null)
                {
                    return csprojFile;
                }

                // Move to the parent directory
                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        public static string[] GetCSharpFiles(string directory)
        {
            // Search for C# files in the project directory and its subdirectories
            return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
        }
    }
}
