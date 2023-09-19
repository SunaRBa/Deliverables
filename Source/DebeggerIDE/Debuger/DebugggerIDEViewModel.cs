using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Debuger
{
    public class DebugggerIDEViewModel : INotifyPropertyChanged, IDisposable
    {
        public ReactiveCommand<object> RunCommand { get; } = new ReactiveCommand<object>();
        public ReactiveCommand<object> StepCommand { get; } = new ReactiveCommand<object>();
        public ReactiveCommand<object> StopCommand { get; } = new ReactiveCommand<object>();

        public ReactiveProperty<bool> IsEnabledRun { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<bool> IsEnabledStep { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<bool> IsEnabledStop { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<string> SourcePath { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<int> Line { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<string> Output { get; } = new ReactiveProperty<string>();

        public ReactiveCollection<SourceItem> SourceCollection { get; } = new ReactiveCollection<SourceItem>();

        public ReactiveCollection<VariableItem> VariableCollection { get; } = new ReactiveCollection<VariableItem>();

        private readonly Dispatcher _dispacher;
        private readonly string _netCoreDbgPath;
        private readonly string _programPath;
        private readonly TextBox _outputTextBox;
        private DebugAdapterHost _debugAdapterHost;

        private readonly CompositeDisposable _compositeDisposableUI = new CompositeDisposable();
        private CompositeDisposable _compositeDisposableDebug;

        public event PropertyChangedEventHandler PropertyChanged;

        public DebugggerIDEViewModel(UIElement parent, TextBox outputTextBox)
        {
            RunCommand.Subscribe(_ => Run()).AddTo(_compositeDisposableUI);
            StepCommand.Subscribe(_ => Step()).AddTo(_compositeDisposableUI);
            StopCommand.Subscribe(_ => Stop()).AddTo(_compositeDisposableUI);
            _dispacher = parent.Dispatcher;

            var appFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var targetFolderProgram = Path.Combine(appFolder, "../../../../../", @"DebuggerTest");
            var targetFolderDll = Path.GetFullPath(Path.Combine(targetFolderProgram, @"bin\x64\Debug\net6.0"));
            _netCoreDbgPath = Path.Combine(appFolder, "netcoredbg", "netcoredbg.exe");
            _programPath = Path.Combine(targetFolderDll, "DebuggerTest.dll");
            _outputTextBox = outputTextBox;

            SourcePath.Value = Path.GetFullPath(Path.Combine(targetFolderProgram, "Program.cs"));

            var programs = File.ReadLines(SourcePath.Value);
            var count = 1;
            foreach (var program in programs)
            {
                var sourceItem = new SourceItem(count.ToString(), false, program);
                sourceItem.BreakPoint.Subscribe(x => UpdateBreakPoint()).AddTo(_compositeDisposableUI);
                SourceCollection.Add(sourceItem);
                count++;
            }

            UpdateEnabled();
        }

        private void UpdateBreakPoint()
        {
            if (_debugAdapterHost == null)
            {
                return;
            }
            var breakPointLines = CreateBreakPointLines();
            _debugAdapterHost.SetBreakPoint(SourcePath.Value, breakPointLines);
            _debugAdapterHost.UpdateBreakPoint();
        }

        private List<int> CreateBreakPointLines()
        {
            var count = 1;
            var breakPointLines = new List<int>();
            foreach (var sourceItem in SourceCollection)
            {
                if (sourceItem.BreakPoint.Value)
                {
                    breakPointLines.Add(count);
                }
                count++;
            }

            return breakPointLines;
        }

        private void InitializeDebugAdapterHost()
        {
            if (_compositeDisposableDebug != null)
            {
                _compositeDisposableDebug.Dispose();
                _compositeDisposableDebug = null;
            }
            _compositeDisposableDebug = new CompositeDisposable();
            _debugAdapterHost = new DebugAdapterHost(_netCoreDbgPath, _programPath);
            _debugAdapterHost.Protocol.LogMessage += Protocol_LogMessage;
            _debugAdapterHost.Stopped.Subscribe(Stopped).AddTo(_compositeDisposableDebug);
            _debugAdapterHost.Exited.Subscribe(Exited).AddTo(_compositeDisposableDebug);
        }

        public void Dispose()
        {
            RemoveDebug();
            _compositeDisposableUI.Dispose();
        }

        private void RemoveDebug()
        {
            if (Line.Value != 0)
            {
                SourceCollection[Line.Value - 1].IsSelect.Value = false;
            }
            Line.Value = 0;
            if (_debugAdapterHost == null)
            {
                return;
            }
            _debugAdapterHost.Protocol.LogMessage -= Protocol_LogMessage;
            _debugAdapterHost.Dispose();
            _debugAdapterHost = null;
            _compositeDisposableDebug.Dispose();
            _compositeDisposableDebug = null;
        }

        private void Run()
        {
            if (_debugAdapterHost == null)
            {
                InitializeDebugAdapterHost();
                var breakPointLines = CreateBreakPointLines();
                _debugAdapterHost.SetBreakPoint(SourcePath.Value, breakPointLines);
            }
            _debugAdapterHost.Run();
            UpdateEnabled();
        }

        private void Step()
        {
            if (_debugAdapterHost == null)
            {
                InitializeDebugAdapterHost();
                var breakPointLines = CreateBreakPointLines();
                _debugAdapterHost.SetBreakPoint(SourcePath.Value, breakPointLines);
            }
            _debugAdapterHost.Step();
            UpdateEnabled();
        }

        private void Stop()
        {
            RemoveDebug();
            UpdateEnabled();
        }

        private void UpdateEnabled()
        {
            if (_debugAdapterHost == null)
            {
                IsEnabledRun.Value = true;
                IsEnabledStep.Value = true;
                IsEnabledStop.Value = false;
                return;
            }
            IsEnabledRun.Value = _debugAdapterHost.CanRun;
            IsEnabledStep.Value = _debugAdapterHost.CanStep;
            IsEnabledStop.Value = _debugAdapterHost.CanStop;
        }

        private void Stopped(StopArugument stopArugument)
        {
            //別スレッドから帰ってくるので注意
            _dispacher.BeginInvoke(() =>
            {
                if (Line.Value != 0)
                {
                    SourceCollection[Line.Value - 1].IsSelect.Value = false;
                }
                Line.Value = stopArugument.StackFrame.Line;
                SourceCollection[Line.Value - 1].IsSelect.Value = true;

                VariableCollection.Clear();
                foreach (var variableItem in stopArugument.VariableItems)
                {
                    VariableCollection.Add(variableItem);
                }
                UpdateEnabled();
            });
        }

        private void Exited(object obj)
        {
            //別スレッドから帰ってくるので注意
            _dispacher.BeginInvoke(() =>
            {
                UpdateEnabled();
                RemoveDebug();
            });
        }

        private void Protocol_LogMessage(object sender, LogEventArgs e)
        {
            Trace.WriteLine(e.Message);
            //別スレッドから帰ってくるので注意
            _dispacher.BeginInvoke(() =>
            {
                Output.Value = Output.Value + Environment.NewLine + e.Message;
                _outputTextBox.ScrollToEnd();
            });
        }
    }
}