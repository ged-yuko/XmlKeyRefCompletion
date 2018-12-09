using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.XmlEditor;
using Microsoft.XmlEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;

namespace XmlKeyRefCompletion
{
    //public class XmlSchemaReference
    //{
    //    public static readonly XmlSchemaReference Empty = new XmlSchemaReference(null, null, null);
    //    private Uri location;
    //    private string targetNamespace;

    //    // Methods
    //    public XmlSchemaReference(string targetNamespace, Uri location, Dictionary<string, string> properties = null)
    //    {
    //        this.targetNamespace = targetNamespace;
    //        this.location = location;
    //        this.Properties = properties ?? new Dictionary<string, string>();
    //    }

    //    public override bool Equals(object obj)
    //    {
    //        XmlSchemaReference reference = obj as XmlSchemaReference;
    //        if (reference == null)
    //        {
    //            return false;
    //        }
    //        return ((reference == this) || ((this.targetNamespace == reference.targetNamespace) && (this.location == reference.location)));
    //    }

    //    public override int GetHashCode()
    //    {
    //        int num = (this.targetNamespace != null) ? this.targetNamespace.GetHashCode() : 0;
    //        if (this.location != null)
    //        {
    //            num ^= this.location.GetHashCode();
    //        }
    //        return num;
    //    }

    //    public static bool operator ==(XmlSchemaReference left, XmlSchemaReference right)
    //    {
    //        return ((left == right) || (((left != null) && (right != null)) && left.Equals(right)));
    //    }

    //    public static bool operator !=(XmlSchemaReference left, XmlSchemaReference right)
    //    {
    //        return !(left == right);
    //    }

    //    // Properties
    //    public Uri Location
    //    {
    //        get
    //        {
    //            return this.location;
    //        }
    //    }

    //    public Dictionary<string, string> Properties { get; private set; }

    //    public string TargetNamespace
    //    {
    //        get
    //        {
    //            return this.targetNamespace;
    //        }
    //    }

    //    public static XmlSchemaReference FromDynamic(dynamic s)
    //    {
    //        return new XmlSchemaReference((string)s.TargetNamespace, (Uri)s.Location, (Dictionary<string, string>)s.Properties);
    //    }

    //    public static IList<XmlSchemaReference> FromList(IList<dynamic> list)
    //    {
    //        return list.Select(FromDynamic).ToList();
    //    }
    //}

    //class MySchemaSetBuilder
    //{
    //    dynamic _schemaSetBuilder;

    //    public MySchemaSetBuilder()
    //    {
    //        dynamic xmlSchemaService = (IVsShell)Package.GetGlobalService(Marshal.GetTypeFromCLSID(Guid.Parse("1A5ACA9F-DFC2-44d4-8E3D-A2ADAC944FAB")));
    //        _schemaSetBuilder = xmlSchemaService.CreateSchemaSetBuilder();
    //    }

    //    public void Compile()
    //    {
    //        _schemaSetBuilder.Compile();
    //    }
    //}

    static class ReflectionHelpers
    {
        public static object CallMethod(this object obj, string methodName, params object[] args)
        {
            var method = obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(obj, args == null || args.Length == 0 ? null : args);
        }

        public static object GetProperty(this object obj, string propertyName)
        {
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop.GetValue(obj);
        }
    }

    class MySchemaCache
    {
        object _schemaCache;

        public XmlSchemaSetBuilder SchemaSetBuilder
        {
            get
            {
                return (XmlSchemaSetBuilder)_schemaCache.GetProperty("SchemaSetBuilder");
            }
        }

        public MySchemaCache()
        {
            var shell = (IVsShell)Package.GetGlobalService(typeof(SVsShell));

            if (!ErrorHandler.Succeeded(shell.GetPackageEnum(out var packagesIterator)))
                return;

            var pp = new IVsPackage[1];
            var packages = new List<IVsPackage>();
            while (ErrorHandler.Succeeded(packagesIterator.Next(1, pp, out var taken)) && taken > 0)
                packages.Add(pp[0]);

            var xmlEditorPackageId = Guid.Parse("87569308-4813-40A0-9CD0-D7A30838CA3F");
            var xmlEditorVsPackage = packages.FirstOrDefault(p => p.GetType().TryGetCustomAttribute<GuidAttribute>(out var id) && Guid.Parse(id.Value) == xmlEditorPackageId);
            //dynamic xmlEditorPackage = (Package)xmlEditorVsPackage;
            // var xmlEditorServiceContainer = (IServiceContainer)xmlEditorVsPackage;
            //_schemaCache = xmlEditorPackage.GetCache();

            _schemaCache = xmlEditorVsPackage.CallMethod("GetCache");
        }

        //internal XmlResolver GetResolver(string baseUrl)
        //{
        //    return _schemaCache.GetResolver(baseUrl);
        //}

        internal IList<XmlSchemaReference> GetCandidateSchemas(object doc)
        {
            return (IList<XmlSchemaReference>)_schemaCache.CallMethod("GetCandidateSchemas", doc);
        }

        internal IList<XmlSchemaReference> GetAssociatedSchemas(object doc, object options, object errorHandler)
        {
            return (IList<XmlSchemaReference>)_schemaCache.CallMethod("GetAssociatedSchemas", doc, options, errorHandler);
        }

        internal void AddSet(XmlSchemaSet xmlSchemaSet)
        {
            _schemaCache.CallMethod("AddSet", xmlSchemaSet);
        }
    }
}
//    class MySchemaSetObtainer
//    {
//        MySchemaCache schemaCache = new MySchemaCache();
//        object doc;
//        object options = Activator.CreateInstance(Type.GetType("Microsoft.XmlEditor.XmlProjectOptions"));

//        public MySchemaSetObtainer(object editorXmlDocument)
//        {
//            doc = editorXmlDocument;

//            //dynamic xmlSchemaService = (IVsShell)Package.GetGlobalService(Marshal.GetTypeFromCLSID(Guid.Parse("1A5ACA9F-DFC2-44d4-8E3D-A2ADAC944FAB")));
//            // IList<dynamic> schemas = xmlSchemaService.GetKnownSchemas();
//            // var knownSchemas = schemas.Select(s => XmlSchemaReference.FromDynamic(s)).ToArray();

//            // dynamic schemaSetBuilder = xmlSchemaService.CreateSchemaSetBuilder();
//        }

//        void MakeSchemaSet()
//        {
//            XmlSchemaSet xmlSchemaSet = SetupSchemas(isSchema);
//            substitutionGroups = GetSubsitutionGroups(xmlSchemaSet);
//            if (compileErrors == 0 && (doc.HasXsiAttribute || xmlSchemaSet.Count != 0))
//            {
//                XmlSchemaValidationFlags xmlSchemaValidationFlags = XmlSchemaValidationFlags.ProcessInlineSchema | XmlSchemaValidationFlags.ReportValidationWarnings | XmlSchemaValidationFlags.ProcessIdentityConstraints;
//                xmlSchemaValidationFlags |= XmlSchemaValidationFlags.AllowXmlAttributes;
//                validator = new XmlSchemaValidator(nameTable, xmlSchemaSet, nsResolver, xmlSchemaValidationFlags);
//                XmlResolver schemaResolver = schemaCache.GetSchemaResolver(doc.Location, returnXmlSchemas: false);
//                validator.XmlResolver = schemaResolver;
//                if (doc.Location != null)
//                {
//                    validator.SourceUri = schemaResolver.ResolveUri(null, doc.Location);
//                }
//                validator.ValidationEventSender = this;
//                validator.ValidationEventHandler += OnValidationError;
//                validator.LineInfoProvider = posinfo;
//                validator.Initialize();
//                xmlSchemaInfo = new XmlSchemaInfo();
//            }
//        }

//        private XmlSchemaSet SetupSchemas(bool isSchema)
//        {
//            nsResolver = new MyXmlNamespaceResolver(nameTable);
//            NamespaceFilter filter = IsReservedNamespace;
//            if (doc.IsSchema)
//            {
//                XmlSchemaSet xmlSchemaSet = CompileSchemas(doc.Schemas.GetCompileOnlySet(filter));
//                if (compileErrors == 0 || doc.CompileSet == null || doc.CompileSet.Count == 0 || doc.CompileSet.Count <= xmlSchemaSet.Count)
//                {
//                    doc.CompileSet = xmlSchemaSet;
//                }
//                compileErrors = 0;
//            }
//            XmlSchemaSet xmlSchemaSet2 = CompileSchemas(doc.Schemas.GetValidationSet(filter));
//            doc.SchemaSet = xmlSchemaSet2;
//            doc.CompileErrors = compileErrors;

//            return xmlSchemaSet2;
//        }

//        internal XmlSchemaSet CompileSchemas(IList<XmlSchemaReference> schemas)
//        {
//            XmlSchemaSet xmlSchemaSet = null;
//            IList<XmlSchemaReference> candidateSchemas = schemaCache.GetCandidateSchemas(doc);
//            IList<XmlSchemaReference> associatedSchemas = schemaCache.GetAssociatedSchemas(doc, options, base.ErrorHandler);
//            XmlSchemaSetBuilder schemaSetBuilder = schemaCache.SchemaSetBuilder;
//            lock (schemaSetBuilder)
//            {
//                IList<XmlSchemaReference> sources = schemaSetBuilder.Sources;
//                sources.Clear();
//                schemaSetBuilder.Candidates = candidateSchemas;
//                foreach (XmlSchemaReference item in associatedSchemas)
//                {
//                    sources.Add(item);
//                }
//                foreach (XmlSchemaReference schema in schemas)
//                {
//                    sources.Add(schema);
//                }
//                schemaSetBuilder.Compile();
//                xmlSchemaSet = schemaSetBuilder.CompiledSet;
//            }
//            schemaCache.AddSet(xmlSchemaSet);
//            foreach (Exception error in schemaSetBuilder.Errors)
//            {
//                compileErrors++;
//                HandleSchemaException(error, XmlSeverityType.Error);
//            }
//            if (xmlSchemaSet != null && validationErrorsBySourceUri != null)
//            {
//                foreach (XmlSchema item2 in xmlSchemaSet.Schemas())
//                {
//                    if (!string.IsNullOrEmpty(item2.SourceUri) && !validationErrorsBySourceUri.ContainsKey(item2.SourceUri))
//                    {
//                        CacheEntry cacheEntry = schemaCache.FindSchema(item2);
//                        if (cacheEntry != null)
//                        {
//                            cacheEntry.Error = ErrorCode.None;
//                        }
//                    }
//                }
//            }
//            ReportUnresolvedImports(xmlSchemaSet);
//            if (xmlSchemaSet == null)
//            {
//                xmlSchemaSet = new XmlSchemaSet(nameTable);
//            }
//            return xmlSchemaSet;
//        }

//    }
//}
