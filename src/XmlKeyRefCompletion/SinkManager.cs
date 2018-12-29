using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

namespace XmlKeyRefCompletion
{
    /// <summary>
    /// Every consumer of data from an <see cref="ITableDataSource"/> provides an <see cref="ITableDataSink"/> to record the changes. We give the consumer
    /// an IDisposable (this object) that they hang on to as long as they are interested in our data (and they Dispose() of it when they are done).
    /// </summary>
    public class SinkManager : IDisposable
    {
        private readonly HighlightInvalidKeyrefTaggerProvider _taggetProvider;
        private readonly ITableDataSink _sink;

        internal SinkManager(HighlightInvalidKeyrefTaggerProvider taggerProvider, ITableDataSink sink)
        {
            _taggetProvider = taggerProvider;
            _sink = sink;

            taggerProvider.AddSinkManager(this);
        }

        public void Dispose()
        {
            // Called when the person who subscribed to the data source disposes of the cookie (== this object) they were given.
            _taggetProvider.RemoveSinkManager(this);
        }

        internal void AddSpellChecker(HighlightInvalidKeyrefTagger spellChecker)
        {
            _sink.AddFactory(spellChecker);
        }

        internal void RemoveSpellChecker(HighlightInvalidKeyrefTagger spellChecker)
        {
            _sink.RemoveFactory(spellChecker);
        }

        internal void UpdateSink()
        {
            _sink.FactorySnapshotChanged(null);
        }
    }
}
