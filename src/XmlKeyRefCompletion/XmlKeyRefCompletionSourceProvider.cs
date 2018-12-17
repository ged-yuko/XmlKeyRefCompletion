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

                    var doc = completionCommandHandler.CurrentDocumentData;
                    if (doc != null)
                    {

                        var triggerPoint = session.GetTriggerPoint(_textBuffer);

                        var point = triggerPoint.GetPoint(session.TextView.TextSnapshot);
                        var line = point.GetContainingLine();
                        var lineNumber = line.LineNumber;
                        var linePosition = point.Position - line.Start.Position;

                        var text = doc.FindTextAt(lineNumber, linePosition);
                        var attr = text.ParentNode as MyXmlAttribute;

                        if (text != null && attr != null && attr.ReferencedKeyPartData != null && linePosition < text.TextLocation.Column + text.Length)
                        {
                            var compList = new List<Completion>();
                            foreach (string str in attr.ReferencedKeyPartData.Values)
                                compList.Add(new Completion(str, str, str, null, null));

                            var key = attr.ReferencedKeyPartData.KeyData;
                            var part = attr.ReferencedKeyPartData;

                            var name = "Keys of " + (key.Arity > 1 ? (key.Name + "#" + part.PartInfo.Id ?? part.Index.ToString()) : key.Name);

                            completionSets.Add(new CompletionSet(
                                name,
                                name,
                                this.FindTokenSpanAtPosition(triggerPoint, session),
                                compList,
                                null)
                            );
                        }
                        else
                        {
                            var compList = new List<Completion>();

                            var name = "Keys...";
                            completionSets.Add(new CompletionSet(
                                name,    //the non-localized title of the tab
                                name,    //the display title of the tab
                                this.FindTokenSpanAtPosition(triggerPoint, session),
                                compList,
                                null)
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
            }
        }

        private ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint point, ICompletionSession session)
        {
            SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition) - 1;
            ITextStructureNavigator navigator = _sourceProvider.NavigatorService.GetTextStructureNavigator(_textBuffer);
            TextExtent extent = navigator.GetExtentOfWord(currentPoint);
            return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        }

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