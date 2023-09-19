using Reactive.Bindings;
using System.ComponentModel;

namespace Debuger
{
    public class SourceItem : INotifyPropertyChanged
    {
        public ReactiveProperty<string> Line { get; }
        public ReactiveProperty<bool> BreakPoint { get; }
        public ReactiveProperty<string> Source { get; }
        public ReactiveProperty<bool> IsSelect { get; } = new ReactiveProperty<bool>(false);

        public event PropertyChangedEventHandler PropertyChanged;

        public SourceItem(string line, bool breakPoint, string source)
        {
            Line = new ReactiveProperty<string>(line);
            BreakPoint = new ReactiveProperty<bool>(breakPoint);
            Source = new ReactiveProperty<string>(source);
        }
    }
}