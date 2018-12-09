using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlKeyRefCompletion
{
    internal class FileXmlUrlResolver : XmlUrlResolver
    {
        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            if (absoluteUri == null)
            {
                throw new ArgumentNullException(nameof(absoluteUri));
            }
            if (absoluteUri.Scheme.StartsWith("file", StringComparison.InvariantCultureIgnoreCase))
            {
                return base.GetEntity(absoluteUri, role, ofObjectToReturn);
            }
            throw new NotSupportedException($"URI scheme \"{absoluteUri.Scheme}\" isn't supported by {nameof(FileXmlUrlResolver)}.");
        }

        public override Task<object> GetEntityAsync(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            return new Task<object>(() => GetEntity(absoluteUri, role, ofObjectToReturn));
        }
    }
}
