using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using XmlKeyRefCompletion.Doc;

namespace XmlKeyRefCompletion
{
    // TODO: refactor it!!

    /// <summary>
    /// Export a <see cref="IViewTaggerProvider"/>
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("xml")]
    [TagType(typeof(InvalidKeyrefTag))]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    // [TextViewRole(PredefinedTextViewRoles.Analyzable)]
    public class HighlightInvalidKeyrefTaggerProvider : IViewTaggerProvider, ITableDataSource
    {
        private readonly List<SinkManager> _managers = new List<SinkManager>();      // Also used for locks
        private readonly List<HighlightInvalidKeyrefTagger> _spellCheckers = new List<HighlightInvalidKeyrefTagger>();

        #region ITaggerProvider Members

        [Import]
        public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        //[Import]
        //internal ITextSearchService TextSearchService { get; set; }

        //[Import]
        //internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

        public ITableManager ErrorTableManager { get; private set; }

        [ImportingConstructor]
        internal HighlightInvalidKeyrefTaggerProvider([Import]ITableManagerProvider provider, [Import] ITextDocumentFactoryService textDocumentFactoryService, [Import] IClassifierAggregatorService classifierAggregatorService)
        {
            this.ErrorTableManager = provider.GetTableManager(StandardTables.ErrorsTable);
            this.TextDocumentFactoryService = textDocumentFactoryService;

            // this.ClassifierAggregatorService = classifierAggregatorService;

            this.ErrorTableManager.AddSource(this, StandardTableColumnDefinitions.DetailsExpander,
                                                   StandardTableColumnDefinitions.ErrorSeverity, StandardTableColumnDefinitions.ErrorCode,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.BuildTool,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.ErrorCategory,
                                                   StandardTableColumnDefinitions.Text, StandardTableColumnDefinitions.DocumentName,
                                                   StandardTableColumnDefinitions.Line, StandardTableColumnDefinitions.Column);

            if (Debugger.IsAttached)
                AppDomain.CurrentDomain.FirstChanceException += (sender, ea) => Debug.Print(ea.Exception.ToString());
        }

        /// <summary>
        /// This method is called by VS to generate the tagger
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="textView"> The text view we are creating a tagger for</param>
        /// <param name="buffer"> The buffer that the tagger will examine for instances of the current word</param>
        /// <returns> Returns a HighlightWordTagger instance</returns>
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            // Only provide highlighting on the top-level buffer
            if (textView.TextBuffer != buffer)
                return null;

            //ITextStructureNavigator textStructureNavigator = this.TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

            //return new HighlightInvalidKeyrefTagger(textView, buffer, this.TextSearchService, textStructureNavigator) as ITagger<T>;

            if ((buffer == textView.TextBuffer) && (typeof(T).IsAssignableFrom(typeof(InvalidKeyrefTag))))
            {
                var errorsTagger = buffer.Properties.GetOrCreateSingletonProperty(typeof(HighlightInvalidKeyrefTagger), () => new HighlightInvalidKeyrefTagger(this, textView, buffer) as ITagger<T>);

                return errorsTagger;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region ITableDataSource members

        public string DisplayName
        {
            get
            {
                // This string should, in general, be localized since it is what would be displayed in any UI that lets the end user pick
                // which ITableDataSources should be subscribed to by an instance of the table control. It really isn't needed for the error
                // list however because it autosubscribes to all the ITableDataSources.
                return "XmlKeyRefCompletion";
            }
        }

        public string Identifier
        {
            get
            {
                return "XmlKeyRefCompletion";
            }
        }

        public string SourceTypeIdentifier
        {
            get
            {
                return StandardTableDataSources.ErrorTableDataSource;
            }
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            // This method is called to each consumer interested in errors. In general, there will be only a single consumer (the error list tool window)
            // but it is always possible for 3rd parties to write code that will want to subscribe.
            return new SinkManager(this, sink);
        }
        #endregion

        #region errors sources management

        public void AddSinkManager(SinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (_managers)
            {
                _managers.Add(manager);

                // Add the pre-existing spell checkers to the manager.
                foreach (var spellChecker in _spellCheckers)
                {
                    manager.AddSpellChecker(spellChecker);
                }
            }
        }

        public void RemoveSinkManager(SinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (_managers)
            {
                _managers.Remove(manager);
            }
        }

        public void AddSpellChecker(HighlightInvalidKeyrefTagger tagger)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding or removing the 1st/last tagger).
            lock (_managers)
            {
                _spellCheckers.Add(tagger);

                // Tell the preexisting managers about the new spell checker
                foreach (var manager in _managers)
                {
                    manager.AddSpellChecker(tagger);
                }
            }
        }

        public void RemoveSpellChecker(HighlightInvalidKeyrefTagger tagget)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding or removing the 1st/last tagger).
            lock (_managers)
            {
                _spellCheckers.Remove(tagget);

                foreach (var manager in _managers)
                {
                    manager.RemoveSpellChecker(tagget);
                }
            }
        }

        public void UpdateAllSinks()
        {
            lock (_managers)
            {
                foreach (var manager in _managers)
                {
                    manager.UpdateSink();
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Derive from TextMarkerTag, in case anyone wants to consume
    /// just the HighlightWordTags by themselves.
    /// </summary>
    public class InvalidKeyrefTag : TextMarkerTag, IErrorTag
    {
        public string ErrorType { get; }
        public object ToolTipContent { get; }

        public InvalidKeyrefTag(string errType, object tooltipContent)
            : base("MarkerFormatDefinition/InvalidKeyrefFormatDefinition")
        {
            this.ErrorType = errType;
            this.ToolTipContent = tooltipContent;
        }
    }


    [Export(typeof(EditorFormatDefinition))]
    [Name("MarkerFormatDefinition/InvalidKeyrefFormatDefinition")]
    [UserVisible(true)]
    internal class InvalidKeyrefFormatDefinition : MarkerFormatDefinition
    {
        public InvalidKeyrefFormatDefinition()
        {
            this.BackgroundColor = Colors.LightSalmon;
            this.ForegroundColor = Colors.Red;
            //this.BackgroundColor = Colors.LightYellow;
            //this.ForegroundColor = Colors.DarkGoldenrod;
            this.DisplayName = "Invalid Xml Keyref";
            this.ZOrder = 5;
        }
    }

    public class HighlightInvalidKeyrefTagger : TableEntriesSnapshotFactoryBase, ITagger<InvalidKeyrefTag>, ITableEntriesSnapshotFactory
    {
        public class SpellingErrorsSnapshot : WpfTableEntriesSnapshotBase
        {
            // public readonly Dictionary<SnapshotSpan, int> SpanToIndex = new Dictionary<SnapshotSpan, int>();

            private readonly NormalizedSnapshotSpanCollection _spans;
            private readonly ReadOnlyCollection<MyXmlAttribute> _attrs;
            private readonly string[] _text;
            private readonly string[] _details;

            private readonly string _filePath;
            private readonly int _versionNumber;

            public SpellingErrorsSnapshot NextSnapshot;

            internal SpellingErrorsSnapshot(string filePath, int versionNumber, NormalizedSnapshotSpanCollection spans, ReadOnlyCollection<MyXmlAttribute> attrs)
            {
                _filePath = filePath;
                _versionNumber = versionNumber;

                _spans = spans;
                _attrs = attrs;
                _text = new string[spans.Count];
                _details = new string[spans.Count];

                //int counter = 0;
                //foreach (var item in spans)
                //{
                //    SpanToIndex.Add(item, counter);
                //    counter++;
                //}
            }

            public override int Count
            {
                get
                {
                    return _spans.Count;
                }
            }

            public override int VersionNumber
            {
                get
                {
                    return _versionNumber;
                }
            }

            public override int IndexOf(int currentIndex, ITableEntriesSnapshot newerSnapshot)
            {
                return currentIndex = -1;
            }

            public override bool TryGetValue(int index, string columnName, out object content)
            {
                if ((index >= 0) && (index < _spans.Count))
                {
                    if (columnName == StandardTableKeyNames.DocumentName)
                    {
                        // We return the full file path here. The UI handles displaying only the Path.GetFileName().
                        content = _filePath;
                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.ErrorCategory)
                    {
                        content = "XmlKeyRefCompletion";
                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.ErrorSource)
                    {
                        content = "XmlKeyRefCompletion";
                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.Line)
                    {
                        // Line and column numbers are 0-based (the UI that displays the line/column number will add one to the value returned here).
                        content = _spans[index].Start.GetContainingLine().LineNumber;

                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.Column)
                    {
                        var position = _spans[index].Start;
                        var line = position.GetContainingLine();
                        content = position.Position - line.Start.Position;

                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.Text)
                    {
                        content = this.GetContentText(index);

                        return true;
                    }
                    else if (columnName == StandardTableKeyNames2.TextInlines)
                    {
                        var inlines = new List<Inline>();

                        inlines.Add(new Run(this.GetContentText(index)));

                        //inlines.Add(new Run("No reference target for keyref: "));
                        //inlines.Add(new Run(_spans[index].GetText()));

                        //inlines.Add(new Run(_spans[index].GetText()) {
                        //    FontWeight = FontWeights.ExtraBold
                        //});

                        content = inlines;

                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.ErrorSeverity)
                    {
                        content = __VSERRORCATEGORY.EC_WARNING;

                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.ErrorSource)
                    {
                        content = ErrorSource.Other;

                        return true;
                    }
                    else if (columnName == StandardTableKeyNames.BuildTool)
                    {
                        content = "XmlKeyRefCompletion";

                        return true;
                    }
                    //else if (columnName == StandardTableKeyNames.ErrorCode)
                    //{
                    //    content = _spans[index].GetText();

                    //    return true;
                    //}
                    //else if ((columnName == StandardTableKeyNames.ErrorCodeToolTip) || (columnName == StandardTableKeyNames.HelpLink))
                    //{
                    //    content = string.Format(CultureInfo.InvariantCulture, "http://www.bing.com/search?q={0}", _spans[index].GetText());

                    //    return true;
                    //}

                    // We should also be providing values for StandardTableKeyNames.Project & StandardTableKeyNames.ProjectName but that is
                    // beyond the scope of this sample.
                }

                content = null;
                return false;
            }

            public override bool CanCreateDetailsContent(int index)
            {
                return true;
            }

            public override bool TryCreateDetailsStringContent(int index, out string content)
            {
                content = this.GetDetailsText(index);
                return true;
            }

            public override bool TryCreateToolTip(int index, string columnName, out object toolTip)
            {
                return base.TryCreateToolTip(index, columnName, out toolTip);
            }

            public string GetDetailsText(int index)
            {
                var content = _details[index] ?? (
                    _details[index] = (_attrs[index].ReferencedKeyPartData?.Values?.Count ?? 0) > 0
                                ? "Consider introduce new entity with such a key or correct it to one of the followings: " + string.Join(", ", _attrs[index].ReferencedKeyPartData.Values)
                                : "Consider introduce new entity with such a key."
                );

                return content;
            }

            public string GetContentText(int index)
            {
                var content = _text[index] ?? (
                    _text[index] = string.Format(
                        CultureInfo.InvariantCulture,
                        "No reference target for keyref: {0}",
                        _spans[index].GetText()
                    )
                );

                return content;
            }
        }

        private readonly HighlightInvalidKeyrefTaggerProvider _owner;

        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly XmlDocumentLoader _loader;

        public NormalizedSnapshotSpanCollection HighlightedSpans { get; private set; }
        public SpellingErrorsSnapshot ErrorsSnapshot { get; private set; }
        public string FilePath { get; private set; }

        public HighlightInvalidKeyrefTagger(HighlightInvalidKeyrefTaggerProvider owner, ITextView view, ITextBuffer sourceBuffer)
        {
            _owner = owner;
            _view = view;
            _buffer = sourceBuffer;
            //View.Caret.PositionChanged += CaretPositionChanged;
            //View.LayoutChanged += ViewLayoutChanged;

            // Get the name of the underlying document buffer
            ITextDocument document;
            if (owner.TextDocumentFactoryService.TryGetTextDocument(view.TextDataModel.DocumentBuffer, out document))
            {
                this.FilePath = document.FilePath;

                document.FileActionOccurred += (sender, ea) => {
                    if (ea.FileActionType == FileActionTypes.DocumentRenamed)
                        this.FilePath = ea.FilePath;
                };
                // TODO we should listen for the file changing its name (ITextDocument.FileActionOccurred)
            }

            this.ErrorsSnapshot = new SpellingErrorsSnapshot(this.FilePath, 0, new NormalizedSnapshotSpanCollection(), new ReadOnlyCollection<MyXmlAttribute>(new MyXmlAttribute[0]));

            _owner.AddSpellChecker(this);
            view.Closed += (sender, ea) => {
                _owner.RemoveSpellChecker(this);

                if (_loader != null)
                    _loader.DocumentDataUpdated -= this.OnDocumentDataUpdated;
            };

            var textViewAdapter = view.GetVsTextView();
            if (!ErrorHandler.Failed(textViewAdapter.GetBuffer(out var textLines)))
            {
                IVsUserData userData = textLines as IVsUserData;
                if (userData != null)
                {
                    Guid id = typeof(XmlKeyRefCompletionCommandHandler).GUID;
                    userData.GetData(ref id, out var cmdHandler);

                    _loader = (cmdHandler as XmlKeyRefCompletionCommandHandler)?.DocumentDataLoader;
                    if (_loader != null)
                    {
                        _loader.DocumentDataUpdated += this.OnDocumentDataUpdated;
                    }
                }
            }
        }

        private void OnDocumentDataUpdated(object sender, EventArgs e)
        {
            if (_loader.DocumentData == null || _loader.DocumentData.InvalidKeyrefs.Count == 0)
            {
                this.HighlightedSpans = new NormalizedSnapshotSpanCollection();
            }
            else
            {
                var keyrefs = _loader.DocumentData.InvalidKeyrefs;

                var spans = keyrefs.Select(attr => {
                    var spanLine = _loader.CurrentSnapshot.GetLineFromLineNumber(attr.TextLocation.Line - 1);
                    var spanPosition = spanLine.Start.Position + attr.TextLocation.Column - 1;
                    int spanLength;

                    var text = attr.ChildNodes.OfType<MyXmlText>().FirstOrDefault();
                    if (text != null)
                    {
                        var textLine = _loader.CurrentSnapshot.GetLineFromLineNumber(text.TextLocation.Line - 1);
                        var textPosition = textLine.Start.Position + text.TextLocation.Column - 1;

                        //spanLength = attrPosition - spanPosition + text.Length + 1;

                        spanPosition = textPosition;
                        spanLength = text.Length;
                    }
                    else
                    {
                        spanLength = attr.OuterXml.Length;
                    }

                    return new Microsoft.VisualStudio.Text.Span(spanPosition, spanLength);
                }).ToList();

                this.HighlightedSpans = new NormalizedSnapshotSpanCollection(_loader.CurrentSnapshot, spans);
                this.ErrorsSnapshot = new SpellingErrorsSnapshot(this.FilePath, this.ErrorsSnapshot.VersionNumber + 1, this.HighlightedSpans, keyrefs);
                _owner.UpdateAllSinks();
            }

            this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        #region ITableEntriesSnapshotFactory members

        public override int CurrentVersionNumber
        {
            get
            {
                return this.ErrorsSnapshot.VersionNumber;
            }
        }

        public override void Dispose()
        {
        }

        public override ITableEntriesSnapshot GetCurrentSnapshot()
        {
            return this.ErrorsSnapshot;
        }

        public override ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            // In theory the snapshot could change in the middle of the return statement so snap the snapshot just to be safe.
            var snapshot = this.ErrorsSnapshot;
            return (versionNumber == snapshot.VersionNumber) ? snapshot : null;
        }

        #endregion

        #region ITagger<HighlightWordTag> Members

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<InvalidKeyrefTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            NormalizedSnapshotSpanCollection wordSpans = this.HighlightedSpans;

            if (spans.Count == 0 || wordSpans == null || wordSpans.Count == 0)
                yield break;

            // If the requested snapshot isn't the same as the one our words are on, translate our spans to the expected snapshot
            if (spans[0].Snapshot != _loader.CurrentSnapshot)
            {
                wordSpans = new NormalizedSnapshotSpanCollection(wordSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));
            }

            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
                yield return new TagSpan<InvalidKeyrefTag>(span, new InvalidKeyrefTag(PredefinedErrorTypeNames.Warning, string.Format("No reference target for keyref: {0}.", span.GetText())));
        }

        //private string GetTagTooltip(SnapshotSpan snapshotSpan)
        //{
        //    var errorsSnapshot = this.ErrorsSnapshot;

        //    if (errorsSnapshot.SpanToIndex.TryGetValue(snapshotSpan, out var index))
        //    {
        //        return errorsSnapshot.GetContentText(index);
        //    }
        //    else
        //    {
        //        return string.Format("No reference target for keyref: {0}.", snapshotSpan.GetText());
        //    }
        //}

        #endregion
    }
}
