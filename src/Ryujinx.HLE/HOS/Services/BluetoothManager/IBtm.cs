using Ryujinx.Common.Logging;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.BluetoothManager
{
    [Service("btm")]
    class IBtm : IpcService
    {
        public IBtm(ServiceCtx context) { }

        [CommandCmif(26)] // 5.1.0+
        // StartBleScanForGeneral(BtdrvBleAdvertisePacketParameter)
        public ResultCode StartBleScanForGeneral(ServiceCtx context)
        {
            // struct of BtdrvBleAdvertisePacketParameter
            uint companyId = context.RequestData.ReadUInt16();
            uint pattern_data = context.RequestData.ReadByte();
            
            Logger.Stub?.PrintStub(LogClass.ServiceBtm, new { companyId, pattern_data });
            return ResultCode.Success;
        }
        
        [CommandCmif(27)] // 5.1.0+
        // StopBleScanForGeneral(BtdrvBleAdvertisePacketParameter)
        public ResultCode StopBleScanForGeneral(ServiceCtx context)
        {
            // struct of BtdrvBleAdvertisePacketParameter
            uint companyId = context.RequestData.ReadUInt16();
            uint pattern_data = context.RequestData.ReadByte();
            
            Logger.Stub?.PrintStub(LogClass.ServiceBtm, new { companyId, pattern_data });
            return ResultCode.Success;
        }
        
        [CommandCmif(29)] // 5.1.0+
        // StartBleScanForPairedDevice(BtdrvBleAdvertisePacketParameter)
        public ResultCode StartBleScanForPairedDevice(ServiceCtx context)
        {
            // struct of BtdrvBleAdvertisePacketParameter
            uint companyId = context.RequestData.ReadUInt16();
            uint pattern_data = context.RequestData.ReadByte();
            
            Logger.Stub?.PrintStub(LogClass.ServiceBtm, new { companyId, pattern_data });
            return ResultCode.Success;
        }
        
        [CommandCmif(30)] // 5.1.0+
        // StopBleFoPairedDevice()
        public ResultCode StopBleForPairedDevice(ServiceCtx context)
        {
            Stream getBleInput = context.RequestData.BaseStream;
            Logger.Stub?.PrintStub(LogClass.ServiceBtm, new { getBleInput });
            return ResultCode.Success;
        }
    }
}
