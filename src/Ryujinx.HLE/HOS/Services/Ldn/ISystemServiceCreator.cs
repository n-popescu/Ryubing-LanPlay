using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator;

namespace Ryujinx.HLE.HOS.Services.Ldn
{
    [Service("ldn:s")]
    class ISystemServiceCreator : IpcService
    {
        public ISystemServiceCreator(ServiceCtx context) : base(context.Device.System.LdnServer) { }

        [CommandCmif(0)]
        // CreateSystemLocalCommunicationService() -> object<nn::ldn::detail::ISystemLocalCommunicationService>
        public ResultCode CreateSystemLocalCommunicationService(ServiceCtx context)
        {
            MakeObject(context, new ISystemLocalCommunicationService(context));

            return ResultCode.Success;
        }

        [CommandCmif(1)] // 18.0.0+
        // CreateClientProcessMonitor() -> object<nn::ldn::detail::IClientProcessMonitor>
        public ResultCode CreateClientProcessMonitor(ServiceCtx context)
        {
            MakeObject(context, new IClientProcessMonitor(context));

            return ResultCode.Success;
        }
    }
}
