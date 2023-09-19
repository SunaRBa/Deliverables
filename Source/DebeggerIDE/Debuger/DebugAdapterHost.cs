using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Debuger
{
    public class DebugAdapterHost : DebugAdapterHostBase, IDisposable
    {
        private enum Status
        {
            Standby,
            Run,
            Pause,
        }

        public bool CanRun => _status is Status.Standby or Status.Pause;
        public bool CanStep => _status is Status.Standby or Status.Pause;
        public bool CanStop => _status is Status.Run or Status.Pause;
        public int? ThreadId { get; private set; }

        private Status _status = Status.Standby;
        private readonly string _programPath;
        private readonly Process _process;
        private readonly object _lock = new object();
        private readonly Subject<StopArugument> _stoppedSubject = new Subject<StopArugument>();
        private readonly Subject<object> _extedSubject = new Subject<object>();
        private string _sourcePath;
        private IEnumerable<int> _breakPointLines;

        public IObservable<StopArugument> Stopped => _stoppedSubject;
        public IObservable<object> Exited => _extedSubject;

        public DebugAdapterHost(string netCoreDbgPath, string programPath)
        {
            _programPath = programPath;

            _process = new Process();
            _process.StartInfo.FileName = netCoreDbgPath;
            _process.StartInfo.Arguments = @" --interpreter=vscode";
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.CreateNoWindow = true;
            _process.Start();

            InitializeProtocolHost(_process.StandardInput.BaseStream, _process.StandardOutput.BaseStream);

            Protocol.Run();
        }

        public void Run()
        {
            lock (_lock)
            {
                if (_status == Status.Run)
                {
                    return;
                }

                if (_status == Status.Standby)
                {
                    Initialize(false);
                    return;
                }
                _status = Status.Run;
                RequestContinue();
            }
        }

        public void Step()
        {
            lock (_lock)
            {
                if (_status == Status.Run)
                {
                    return;
                }

                if (_status == Status.Standby)
                {
                    Initialize(true);
                    return;
                }
                _status = Status.Run;
                RequestStep();
            }
        }

        public void SetBreakPoint(string sourcePath, IEnumerable<int> breakPointLines)
        {
            _sourcePath = sourcePath;
            _breakPointLines = breakPointLines;
        }

        public void UpdateBreakPoint()
        {
            var request = new SetBreakpointsRequest();
            var source = new Source
            {
                Name = Path.GetFileName(_sourcePath),
                Path = _sourcePath
            };
            request.Source = source;
            foreach (var line in _breakPointLines)
            {
                request.Breakpoints.Add(new SourceBreakpoint(line));
            }
            Protocol.SendRequestSync(request);
        }

        public void Dispose()
        {
            if (Protocol.IsRunning)
            {
                Protocol.Stop();
            }
            _process.Kill();
            _process.Dispose();
        }

        protected override void HandleStoppedEvent(StoppedEvent body)
        {
            //別スレッドから来るので注意
            //帰って来たスレッドだとリクエストを発信できないので別スレッド立ち上げる
            Task.Run(() =>
            {
                lock (_lock)
                {
                    ThreadId = body.ThreadId;
                    _status = Status.Pause;
                    switch (body.Reason)
                    {
                        case StoppedEvent.ReasonValue.Pause:
                        case StoppedEvent.ReasonValue.Step:
                        case StoppedEvent.ReasonValue.Breakpoint:
                        case StoppedEvent.ReasonValue.Entry:
                            OnNextStopped();
                            break;
                        case StoppedEvent.ReasonValue.Exception:
                            //例外発生
                            break;
                        case StoppedEvent.ReasonValue.Goto:
                        case StoppedEvent.ReasonValue.FunctionBreakpoint:
                        case StoppedEvent.ReasonValue.DataBreakpoint:
                        case StoppedEvent.ReasonValue.InstructionBreakpoint:
                        case StoppedEvent.ReasonValue.Restart:
                        case StoppedEvent.ReasonValue.Unknown:
                            throw new NotSupportedException();
                    }
                    base.HandleStoppedEvent(body);
                }
            });
        }

        private void OnNextStopped()
        {
            var stackFrames = RequestStackTrace();
            var scopeRequest = new ScopesRequest();
            var stakFrame = stackFrames.First();
            scopeRequest.FrameId = stakFrame.Id;
            var scopeResponse = Protocol.SendRequestSync(scopeRequest);

            var request = new VariablesRequest
            {
                VariablesReference = scopeResponse.Scopes.First().VariablesReference
            };
            var response = Protocol.SendRequestSync(request);

            var variableItemList = new List<VariableItem>();
            foreach (var variable in response.Variables)
            {
                variableItemList.Add(new VariableItem(variable.Name, variable.Type, variable.Value));
            }
            _stoppedSubject.OnNext(new StopArugument(stakFrame, variableItemList));
        }

        protected override void HandleExitedEvent(ExitedEvent body)
        {
            lock (_lock)
            {
                //別スレッドから来るので注意
                //帰って来たスレッドだとリクエストを発信できないので別スレッド立ち上げる
                Task.Run(() =>
                {
                    _status = Status.Standby;
                    base.HandleExitedEvent(body);
                    _extedSubject.OnNext(1);
                });
            }
        }

        private void RequestInitialize()
        {
            var request = new InitializeRequest();
            request.Args.ClientID = "vscode";
            request.Args.ClientName = "Visual Studio Code";
            request.Args.AdapterID = "coreclr";
            request.Args.LinesStartAt1 = true;
            request.Args.ColumnsStartAt1 = true;
            request.Args.SupportsVariableType = true;
            request.Args.SupportsVariablePaging = true;
            request.Args.SupportsRunInTerminalRequest = true;
            request.Args.Locale = "Jp-jp";
            Protocol.SendRequestSync(request);
        }

        private void RequestLunch(bool stopEntry)
        {
            var request = new LaunchRequest();
            request.Args.ConfigurationProperties.Add("name", ".NET Core Launch (console) with pipeline");
            request.Args.ConfigurationProperties.Add("type", "coreclr");
            request.Args.ConfigurationProperties.Add("preLaunchTask", "build");
            request.Args.ConfigurationProperties.Add("program", _programPath);// デバッグ対象のパス
            request.Args.ConfigurationProperties.Add("cwd", "");
            request.Args.ConfigurationProperties.Add("console", "internalConsole");
            request.Args.ConfigurationProperties.Add("stopAtEntry", stopEntry);
            request.Args.ConfigurationProperties.Add("internalConsoleOptions", "openOnSessionStart");
            request.Args.ConfigurationProperties.Add("__sessionId", Guid.NewGuid().ToString());
            Protocol.SendRequestSync(request);
        }

        private void RequstConfigurationDone()
        {
            var request = new ConfigurationDoneRequest();
            Protocol.SendRequestSync(request);
        }

        private void RequestContinue()
        {
            var request = new ContinueRequest();
            if (!ThreadId.HasValue) return;
            request.ThreadId = ThreadId.Value;
            Protocol.SendRequestSync(request);
        }

        private void RequestStep()
        {
            var request = new NextRequest();
            if (!ThreadId.HasValue) return;
            request.ThreadId = ThreadId.Value;
            Protocol.SendRequestSync(request);
        }

        private List<Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame> RequestStackTrace()
        {
            if (ThreadId == null)
            {
                return new List<Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame>();
            }
            var request = new StackTraceRequest
            {
                ThreadId = ThreadId.Value
            };
            var responce = Protocol.SendRequestSync(request);
            return responce.StackFrames;
        }

        private void Initialize(bool stopEntry)
        {
            RequestInitialize();
            RequestLunch(stopEntry);
            UpdateBreakPoint();
            RequstConfigurationDone();
            _status = Status.Run;
        }
    }
}
