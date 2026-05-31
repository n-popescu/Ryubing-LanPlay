using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator
{
    class ISystemLocalCommunicationService : IUserLocalCommunicationService
    {
        private const int NifmRequestId = 90;

        public ISystemLocalCommunicationService(ServiceCtx context) : base(context) { }

        protected override bool ValidateLocalCommunicationId => false;

        // NOTE: This overrides the parent's Initialize method with the same command ID (402)
        // The CommandCmif attribute is inherited from the parent class
        public override ResultCode Initialize(ServiceCtx context)
        {
            uint operationMode = context.RequestData.ReadUInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new { operationMode });

            return ResultCode.Success;
        }

        [CommandCmif(403)]
        // InitializeWithVersion(s32 version, pid)
        public ResultCode InitializeWithVersion(ServiceCtx context)
        {
            int version = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new { version });

            return InitializeImpl(context, context.Process.Pid, NifmRequestId);
        }

        [CommandCmif(404)] // 11.0.0+
        // InitializeWithPriority(s32 version, s32 priority, pid)
        public ResultCode InitializeWithPriority(ServiceCtx context)
        {
            int version = context.RequestData.ReadInt32();
            int priority = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new { version, priority });

            return InitializeImpl(context, context.Process.Pid, NifmRequestId);
        }
    }
}
