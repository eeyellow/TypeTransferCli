﻿using System.Reflection;
using System.Xml;

namespace TypeTransferCli
{
    /// <summary>
    /// Utility class to provide documentation for various types where available with the assembly
    /// </summary>
    internal static class DocumentationExtensions
    {
        /// <summary> Provides the documentation comments for a specific method </summary>
        /// <param name="methodInfo">The MethodInfo (reflection data ) of the member to find documentation for</param>
        /// <returns>The XML fragment describing the method</returns>
        public static XmlElement GetDocumentation(this MethodInfo methodInfo)
        {
            // Calculate the parameter string as this is in the member name in the XML
            var parametersString = "";
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                if (parametersString.Length > 0)
                {
                    parametersString += ",";
                }
                parametersString += parameterInfo.ParameterType.FullName;
            }

            //AL: 15.04.2008 ==> BUG-FIX remove “()” if parametersString is empty
            return XmlFromName(methodInfo.DeclaringType, 'M', parametersString.Length > 0 ? $"{methodInfo.Name}({parametersString})" : methodInfo.Name);
        }

        /// <summary> Provides the documentation comments for a specific member </summary>
        /// <param name="memberInfo">The MemberInfo (reflection data) or the member to find documentation for</param>
        /// <returns>The XML fragment describing the member</returns>
        private static XmlElement GetDocumentation(this MemberInfo memberInfo) =>
            // First character [0] of member type is prefix character in the name in the XML
            XmlFromName(memberInfo.DeclaringType, memberInfo.MemberType.ToString()[0], memberInfo.Name);

        /// <summary> Returns the Xml documentation summary comment for this member </summary>
        /// <param name="memberInfo"></param>
        /// <returns></returns>
        public static string GetSummary(this MemberInfo memberInfo)
        {
            try
            {
                var element = memberInfo.GetDocumentation();
                var summaryElm = element?.SelectSingleNode("summary");
                return summaryElm == null ? "" : summaryElm.InnerText.Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary> Provides the documentation comments for a specific type </summary>
        /// <param name="type">Type to find the documentation for</param>
        /// <returns>The XML fragment that describes the type</returns>
        private static XmlElement GetDocumentation(this Type type) =>
            // Prefix in type names is T
            XmlFromName(type, 'T', "");

        /// <summary> Gets the summary portion of a type's documentation or returns an empty string if not available </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetSummary(this Type type)
        {
            try
            {
                var element = type.GetDocumentation();
                var summaryElm = element?.SelectSingleNode("summary");
                return summaryElm == null ? "" : summaryElm.InnerText.Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }

        }

        /// <summary> Obtains the XML Element that describes a reflection element by searching the members for a member that has a name that describes the element. </summary>
        /// <param name="type">The type or parent type, used to fetch the assembly</param>
        /// <param name="prefix">The prefix as seen in the name attribute in the documentation XML</param>
        /// <param name="name">Where relevant, the full name qualifier for the element</param>
        /// <returns>The member that has a name that describes the specified reflection element</returns>
        private static XmlElement XmlFromName(this Type type, char prefix, string name)
        {
            var fullName = string.IsNullOrWhiteSpace(name) ? $"{prefix}:{type.FullName}" : $"{prefix}:{type.FullName}.{name}";

            var xmlDocument = XmlFromAssembly(type.Assembly);

            var matchedElement = xmlDocument["doc"]?["members"]?.SelectSingleNode("member[@name='" + fullName + "']") as XmlElement;

            if (matchedElement == null)
            {
                fullName = fullName.Replace("+", ".");
                xmlDocument = XmlFromAssembly(type.Assembly);
                matchedElement = xmlDocument["doc"]?["members"]?.SelectSingleNode("member[@name='" + fullName + "']") as XmlElement;
            }

            return matchedElement;
        }

        /// <summary> A cache used to remember Xml documentation for assemblies </summary>
        private static readonly Dictionary<Assembly, XmlDocument> _cache = new Dictionary<Assembly, XmlDocument>();

        /// <summary> A cache used to store failure exceptions for assembly lookups </summary>
        private static readonly Dictionary<Assembly, Exception> _failCache = new Dictionary<Assembly, Exception>();

        /// <summary> Obtains the documentation file for the specified assembly </summary>
        /// <param name="assembly">The assembly to find the XML document for</param>
        /// <returns>The XML document</returns>
        /// <remarks>This version uses a cache to preserve the assemblies, so that
        /// the XML file is not loaded and parsed on every single lookup</remarks>
        private static XmlDocument XmlFromAssembly(this Assembly assembly)
        {
            if (_failCache.TryGetValue(assembly, out var failCache))
            {
                throw failCache;
            }

            try
            {

                if (!_cache.ContainsKey(assembly))
                {
                    // load the document into the cache
                    _cache[assembly] = XmlFromAssemblyNonCached(assembly);
                }

                return _cache[assembly];
            }
            catch (Exception exception)
            {
                _failCache[assembly] = exception;
                throw;
            }
        }

        /// <summary> Loads and parses the documentation file for the specified assembly </summary>
        /// <param name="assembly">The assembly to find the XML document for</param>
        /// <returns>The XML document</returns>
        private static XmlDocument XmlFromAssemblyNonCached(Assembly assembly)
        {
            var assemblyFilename = assembly.Location;

            if (!string.IsNullOrWhiteSpace(assemblyFilename))
            {
                StreamReader streamReader;

                try
                {
                    streamReader = new StreamReader(Path.ChangeExtension(assemblyFilename, ".xml"));
                }
                catch (FileNotFoundException exception)
                {
                    throw new Exception("XML documentation not present (make sure it is turned on in project properties when building)", exception);
                }

                var xmlDocument = new XmlDocument();
                xmlDocument.Load(streamReader);
                return xmlDocument;
            }

            throw new Exception("Could not ascertain assembly filename", null);
        }
    }
}
