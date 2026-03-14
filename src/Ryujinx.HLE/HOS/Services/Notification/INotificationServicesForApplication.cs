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
    }
}
