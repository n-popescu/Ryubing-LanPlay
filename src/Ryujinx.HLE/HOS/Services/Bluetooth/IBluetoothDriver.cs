using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Bluetooth.BluetoothDriver;
using Ryujinx.HLE.HOS.Services.Settings;
using Ryujinx.Horizon.Common;
using System;

namespace Ryujinx.HLE.HOS.Services.Bluetooth
{
    [Service("btdrv")]
    class IBluetoothDriver : IpcService
    {
#pragma warning disable CS0414, IDE0052 // Remove unread private member
        private string _unknownLowEnergy;
#pragma warning restore CS0414, IDE0052

        public IBluetoothDriver(ServiceCtx context) { }

        [CommandCmif(46)]
        // InitializeBluetoothLe() -> handle<copy>
        public ResultCode InitializeBluetoothLe(ServiceCtx context)
        {
            NxSettings.Settings.TryGetValue("bluetooth_debug!skip_boot", out object debugMode);

            int initializeEventHandle;

            if ((bool)debugMode)
            {
                if (BluetoothEventManager.InitializeBleDebugEventHandle == 0)
                {
                    BluetoothEventManager.InitializeBleDebugEvent = new KEvent(context.Device.System.KernelContext);

                    if (context.Process.HandleTable.GenerateHandle(BluetoothEventManager.InitializeBleDebugEvent.ReadableEvent, out BluetoothEventManager.InitializeBleDebugEventHandle) != Result.Success)
                    {
                        throw new InvalidOperationException("Out of handles!");
                    }
                }

                if (BluetoothEventManager.UnknownBleDebugEventHandle == 0)
                {
                    BluetoothEventManager.UnknownBleDebugEvent = new KEvent(context.Device.System.KernelContext);

                    if (context.Process.HandleTable.GenerateHandle(BluetoothEventManager.UnknownBleDebugEvent.ReadableEvent, out BluetoothEventManager.UnknownBleDebugEventHandle) != Result.Success)
                    {
                        throw new InvalidOperationException("Out of handles!");
                    }
                }

                if (BluetoothEventManager.RegisterBleDebugEventHandle == 0)
                {
                    BluetoothEventManager.RegisterBleDebugEvent = new KEvent(context.Device.System.KernelContext);

                    if (context.Process.HandleTable.GenerateHandle(BluetoothEventManager.RegisterBleDebugEvent.ReadableEvent, out BluetoothEventManager.RegisterBleDebugEventHandle) != Result.Success)
                    {
                        throw new InvalidOperationException("Out of handles!");
                    }
                }

                initializeEventHandle = BluetoothEventManager.InitializeBleDebugEventHandle;
            }
            else
            {
                _unknownLowEnergy = "low_energy";

                if (BluetoothEventManager.InitializeBleEventHandle == 0)
                {
                    BluetoothEventManager.InitializeBleEvent = new KEvent(context.Device.System.KernelContext);

                    if (context.Process.HandleTable.GenerateHandle(BluetoothEventManager.InitializeBleEvent.ReadableEvent, out BluetoothEventManager.InitializeBleEventHandle) != Result.Success)
                    {
                        throw new InvalidOperationException("Out of handles!");
                    }
                }

                if (BluetoothEventManager.UnknownBleEventHandle == 0)
                {
                    BluetoothEventManager.UnknownBleEvent = new KEvent(context.Device.System.KernelContext);

                    if (context.Process.HandleTable.GenerateHandle(BluetoothEventManager.UnknownBleEvent.ReadableEvent, out BluetoothEventManager.UnknownBleEventHandle) != Result.Success)
                    {
                        throw new InvalidOperationException("Out of handles!");
                    }
                }

                if (BluetoothEventManager.RegisterBleEventHandle == 0)
                {
                    BluetoothEventManager.RegisterBleEvent = new KEvent(context.Device.System.KernelContext);

                    if (context.Process.HandleTable.GenerateHandle(BluetoothEventManager.RegisterBleEvent.ReadableEvent, out BluetoothEventManager.RegisterBleEventHandle) != Result.Success)
                    {
                        throw new InvalidOperationException("Out of handles!");
                    }
                }

                initializeEventHandle = BluetoothEventManager.InitializeBleEventHandle;
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(initializeEventHandle);

            return ResultCode.Success;
        }

        [CommandCmif(57)]
        // Takes a type-0x19 input buffer containing a #BleAdvertiseFilter, no output. 
        public ResultCode AddBleScanFilterCondition(ServiceCtx context)
        {

            byte[] inputBuffer = context.RequestData.ReadBytes(25);
            
            // struct: BleAdvertiseFilter

            // ulong filterId = inputBuffer;                                                   // 0x0 	0x1 	FilterId
            // ulong condDataSize = (inputBuffer << 0) & 0xff;                                 // 0x1 	0x1 	CondDataSize. Only used with CondType Manu.
            // ulong condType = (inputBuffer << 8) & 0xff;                                     // 0x2 	0x1 	CondType
            // ulong condData = (inputBuffer << 16) & 0xff;                                    // 0x3 	0x1D 	CondData, content depends on CondType.
            // ulong mask = (inputBuffer << 20) & 0xff;                                        // 0x20 0x1D 	Mask. Only used with CondType Manu. +0 = u16 CompanyIdMask, then {pattern mask}.
            // ulong maskSize = (inputBuffer << 61) & 0xff;                                    // 0x3D 0x1 	MaskSize. Only used with CondType Manu.
            
            // CondType:
            // Value 	Name 	Description
            // 2-3 		ServiceUuid16. CondData = 16bit UUID which is byteswapped.
            // 4-5 		ServiceUuid32. CondData = 32bit UUID which is byteswapped.
            // 6-7 		ServiceUuid128. CondData = 128bit UUID which is copied raw into the param struct.
            // 255 		Manu. CondData: u16 CompanyId, then {pattern data}. 

            Logger.Stub?.PrintMsg(LogClass.ServiceBtm, $"inputBuffer: {string.Join(", ", inputBuffer)}");

            return ResultCode.Success;
        }
    }
}
