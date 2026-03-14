using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Notification
{
    [Service("notif:a")] // 9.0.0+
    class INotificationServicesForApplication : IpcService
    {
        public INotificationServicesForApplication(ServiceCtx context) { }
        
        // Leaving this here since I can never find it: https://switchbrew.org/wiki/Glue_services
        
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
        // Initialize(PID-descriptor, u64 pid_reserved)
        public ResultCode Intialize(ServiceCtx context)
        {
            ulong PID = context.Device.Processes.ActiveApplication.ProgramId;
            ulong pid_reserved = context.Device.Processes.ActiveApplication.ProcessId;
            
            Logger.Stub?.PrintStub(LogClass.ServiceNotification, new { PID, pid_reserved });
            return ResultCode.Success;
        }
    }
}
