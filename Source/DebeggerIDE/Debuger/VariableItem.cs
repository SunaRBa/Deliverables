using Reactive.Bindings;
using System.ComponentModel;

namespace Debuger
{
    public class VariableItem : INotifyPropertyChanged
    {
        public ReactiveProperty<string> Name { get; }
        public ReactiveProperty<string> Type { get; set; }
        public ReactiveProperty<string> Value { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public VariableItem(string name, string type, string value)
        {
            Name = new ReactiveProperty<string>(name);
            Type = new ReactiveProperty<string>(type);
            Value = new ReactiveProperty<string>(value);
        }
    }
}