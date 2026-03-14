using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Notification
{
    class INotificationSystemEventAccessor : IpcService
    {
        public INotificationSystemEventAccessor(ServiceCtx context) { }
        
        [CommandCmif(0)] // 9.0.0+
        // GetNotificationSendingNotifier() -> nn::notification::server::INotificationSystemEventAccessor
        public ResultCode GetSystemEvent(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.Service);
            return ResultCode.Success;
        }
    }
}
