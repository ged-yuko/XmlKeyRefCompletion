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
using XmlKeyRefCompletion.Doc;

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
            return new TestCompletionSource(this, textBuffer);
        }
    }

    internal class TestCompletionSource : ICompletionSource
    {
        private TestCompletionSourceProvider _sourceProvider;
        private ITextBuffer _textBuffer;

        public TestCompletionSource(TestCompletionSourceProvider sourceProvider, ITextBuffer textBuffer)
        {
            _sourceProvider = sourceProvider;
            _textBuffer = textBuffer;
        }


        void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                if (session.TextView.Properties.TryGetProperty<XmlKeyRefCompletionCommandHandler>(typeof(XmlKeyRefCompletionCommandHandler).GUID, out var completionCommandHandler))
                {

                    completionCommandHandler.DocumentDataLoader.ForceReload();

                    var doc = completionCommandHandler.DocumentDataLoader.DocumentData;
                    if (doc != null)
                    {

                        var mayBePoint = session.GetTriggerPoint(session.TextView.TextSnapshot);
                        if (mayBePoint.HasValue)
                        {
                            var point = mayBePoint.Value;
                            var line = point.GetContainingLine();
                            var lineNumber = line.LineNumber;
                            var linePosition = point.Position - line.Start.Position;

                            var text = doc.FindTextAt(lineNumber, linePosition);
                            var attr = text?.ParentNode as MyXmlAttribute;

                            if (text != null && attr != null && attr.ReferencedKeyPartData != null && linePosition < text.TextLocation.Column + text.Length)
                            {
                                var compList = new List<Completion>();
                                foreach (string str in attr.ReferencedKeyPartData.Values.OrderBy(s => s))
                                    compList.Add(new Completion(str, str, str, null, null));

                                var key = attr.ReferencedKeyPartData.KeyData;
                                var part = attr.ReferencedKeyPartData;

                                var name = "Keys of " + (key.Arity > 1 ? (key.Name + "#" + part.PartInfo.Id ?? part.Index.ToString()) : key.Name);

                                var trackingSpanLine = line.Snapshot.GetLineFromLineNumber(text.TextLocation.Line - 1);
                                var trackingSpanPosition = trackingSpanLine.Start.Position + text.TextLocation.Column - 1;
                                var trackingSpan = point.Snapshot.CreateTrackingSpan(trackingSpanPosition, text.Value.Length, SpanTrackingMode.EdgeInclusive);

                                completionSets.Add(new CompletionSet(
                                    name,
                                    name,
                                    //this.FindTokenSpanAtPosition(session, point),
                                    trackingSpan,
                                    compList,
                                    null)
                                );
                            }
                            else
                            {
                                var compList = new List<Completion>();
                                completionSets.Add(new CompletionSet());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        }

        //private ITrackingSpan FindTokenSpanAtPosition(ICompletionSession session, SnapshotPoint point)
        //{
        //    //SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition) - 1;
        //    //ITextStructureNavigator navigator = _sourceProvider.NavigatorService.GetTextStructureNavigator(_textBuffer);
        //    //TextExtent extent = navigator.GetExtentOfWord(currentPoint);
        //    point.GetContainingLine().GetText().LastIndexOf('"', point.Position)
        //    return currentPoint.Snapshot.CreateTrackingSpan(, SpanTrackingMode.EdgeInclusive);
        //}

        private bool m_isDisposed = false;

        void IDisposable.Dispose()
        {
            if (!m_isDisposed)
            {
                GC.SuppressFinalize(this);
                m_isDisposed = true;
            }
        }
    }


}