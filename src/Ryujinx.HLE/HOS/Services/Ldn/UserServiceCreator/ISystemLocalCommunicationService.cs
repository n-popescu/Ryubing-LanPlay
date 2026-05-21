using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Services.Ldn.Types;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator
{
    class ISystemLocalCommunicationService : IUserLocalCommunicationService
    {
        private const int NifmRequestId = 90;

        private bool _actionFrameEnabled;

        protected override bool ValidateLocalCommunicationId => false;

        public ISystemLocalCommunicationService(ServiceCtx context) : base(context) { }

        // NOTE: This overrides the parent's Initialize method with the same command ID (402)
        // The CommandCmif attribute is inherited from the parent class
        public override ResultCode Initialize(ServiceCtx context)
        {
            uint operationMode = context.RequestData.ReadUInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new { operationMode });

            return ResultCode.Success;
        }

        [CommandCmif(500)] // 18.0.0+
        // EnableActionFrame(nn::ldn::ActionFrameSettings)
        public ResultCode EnableActionFrame(ServiceCtx context)
        {
            byte[] settings = context.RequestData.ReadBytes(0x80);

            _actionFrameEnabled = true;

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new
            {
                localCommunicationId = System.BitConverter.ToUInt64(settings, 0),
                securityMode = System.BitConverter.ToUInt16(settings, 0x3c),
                passphraseSize = System.BitConverter.ToUInt16(settings, 0x3e),
            });

            return ResultCode.Success;
        }

        [CommandCmif(501)] // 18.0.0+
        // DisableActionFrame()
        public ResultCode DisableActionFrame(ServiceCtx context)
        {
            _actionFrameEnabled = false;

            Logger.Stub?.PrintStub(LogClass.ServiceLdn);

            return ResultCode.Success;
        }

        [CommandCmif(502)] // 18.0.0+
        // SendActionFrame(buffer<unknown, 0x21>, nn::ldn::MacAddress, nn::ldn::MacAddress, s16 channel, u32 flags)
        public ResultCode SendActionFrame(ServiceCtx context)
        {
            Array6<byte> destination = context.RequestData.ReadStruct<Array6<byte>>();
            Array6<byte> bssid = context.RequestData.ReadStruct<Array6<byte>>();
            short channel = context.RequestData.ReadInt16();
            _ = context.RequestData.ReadInt16();
            uint flags = context.RequestData.ReadUInt32();

            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x21();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new
            {
                enabled = _actionFrameEnabled,
                bufferPosition,
                bufferSize,
                destination,
                bssid,
                channel,
                flags,
            });

            return _actionFrameEnabled && bufferPosition != 0 && bufferSize != 0
                ? ResultCode.Success
                : ResultCode.InvalidState;
        }

        [CommandCmif(503)] // 18.0.0+
        // RecvActionFrame(u32 flags, buffer<unknown, 0x22>) -> nn::ldn::MacAddress, nn::ldn::MacAddress, s16 channel, u32 size, s32 link_level
        public ResultCode RecvActionFrame(ServiceCtx context)
        {
            uint flags = context.RequestData.ReadUInt32();
            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x22();

            if (bufferPosition != 0 && bufferSize != 0)
            {
                MemoryHelper.FillWithZeros(context.Memory, bufferPosition, (int)bufferSize);
            }

            context.ResponseData.WriteStruct(new Array6<byte>());
            context.ResponseData.WriteStruct(new Array6<byte>());
            context.ResponseData.Write((short)0);
            context.ResponseData.Write((ushort)0);
            context.ResponseData.Write(0u);
            context.ResponseData.Write(0);

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new
            {
                enabled = _actionFrameEnabled,
                bufferSize,
                flags,
            });

            return _actionFrameEnabled ? ResultCode.Success : ResultCode.InvalidState;
        }

        [CommandCmif(505)] // 18.0.0+
        // SetHomeChannel(s16 channel)
        public ResultCode SetHomeChannel(ServiceCtx context)
        {
            short channel = context.RequestData.ReadInt16();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new { channel });

            return ResultCode.Success;
        }

        [CommandCmif(600)] // 18.0.0+
        // SetTxPower(s32 power)
        public ResultCode SetTxPower(ServiceCtx context)
        {
            int power = context.RequestData.ReadInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceLdn, new { power });

            return ResultCode.Success;
        }

        [CommandCmif(601)] // 18.0.0+
        // ResetTxPower()
        public ResultCode ResetTxPower(ServiceCtx context)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceLdn);

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
