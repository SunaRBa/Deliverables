using System.Collections.Generic;

namespace Debuger
{
    public class StopArugument
    {
        public Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame StackFrame { get; }
        public IEnumerable<VariableItem> VariableItems { get; }

        public StopArugument(Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.StackFrame stackFrame, IEnumerable<VariableItem> variableItems)
        {
            StackFrame = stackFrame;
            VariableItems = variableItems;
        }
    }
}