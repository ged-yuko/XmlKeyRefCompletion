using Microsoft.VisualStudio.Language.Intellisense;
// using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace XmlKeyRefCompletion
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("xml")]
    [Name("token completion test")]
    internal class TestCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public TestCompletionSourceProvider()
        {

        }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            //var nav = this.NavigatorService.GetTextStructureNavigator(textBuffer);

            //var shot = textBuffer.CurrentSnapshot;
            //var trackingSpan = shot.CreateTrackingSpan(0, shot.Length, SpanTrackingMode.EdgeExclusive);
            //var span = nav.GetSpanOfEnclosing(trackingSpan.GetSpan(shot));

            //var tree = span.CollectTree(
            //    ss => this.GetChilds(nav, ss),
            //    ss => new[] { "\r", "\n" }.Aggregate(shot.GetText(ss.Span), (s, c) => s.Replace(c, string.Empty)).TrimLength(200)
            //);

            //Debug.Print(tree);

            return new TestCompletionSource(this, textBuffer);
        }

        //private IEnumerable<SnapshotSpan> GetChilds(ITextStructureNavigator nav, SnapshotSpan span)
        //{
        //    var items = new HashSet<SnapshotSpan>();
        //    var item = nav.GetSpanOfFirstChild(span);

        //    while (items.Add(item))
        //        item = nav.GetSpanOfNextSibling(item);

        //    return items;
        //}

    }

}