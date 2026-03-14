namespace Ryujinx.HLE.HOS.Services.Notification
{
    [Service("notif:a")] // 9.0.0+
    class INotificationServicesForApplication : IpcService
    {
        public INotificationServicesForApplication(ServiceCtx context) { }
        
        [CommandCmif(520)] // 9.0.0+
        // ListAlarmSettings(nn::arp::ApplicationCertificate) -> s32 AlarmSettingsCount
        public ResultCode ListAlarmSettings(ServiceCtx context)
        {
            // TO-DO: Currently just returns 0. Should read in an ApplicationCertificate.
            int alarmSettingsCount = 0;
            context.ResponseData.Write(alarmSettingsCount);
            return ResultCode.Success;
        }

        [CommandCmif(1000)] // 9.0.0+
        // GetNotificationCount() -> nn::notification::server::INotificationSystemEventAccessor
        public ResultCode GetNotificationCount(ServiceCtx context)
        {
            MakeObject(context, new INotificationSystemEventAccessor(context));
            return ResultCode.Success;
        }
        
        [CommandCmif(1040)] // 9.0.0+
        // GetNotificationSendingNotifier() -> nn::notification::server::INotificationSystemEventAccessor
        public ResultCode GetNotificationSendingNotifier(ServiceCtx context)
        {
            MakeObject(context, new INotificationSystemEventAccessor(context));
            return ResultCode.Success;
        }
    }
}
