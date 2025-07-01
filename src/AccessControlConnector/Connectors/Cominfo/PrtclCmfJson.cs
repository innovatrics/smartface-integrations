using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public class PrtclCmfJson
    {
        public const string name = "cmf_private";

        #region PacketHeader
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Command
        {
            no,
            login,
            login_resp,
            device_list,
            device_list_resp,
            action,
            action_resp,
            dev_event,
            ping,
            ping_resp,
        }

        public class Header
        {
            [JsonProperty(Order = -2)]
            public string name { get; set; }

            [JsonProperty(Order = -2)]
            public Command cmd { get; set; }

            [JsonProperty(Order = -2)]
            public Device dev { get; set; }

            public Header() { }

            public Header(Command command) : this(command, null) { }

            public Header(Command command, Device device)
            {
                name = PrtclCmfJson.name;
                cmd = command;
                dev = device;
            }
        }

        public class Device
        {
            public string line { get; set; }
            public ushort addr { get; set; }

            public Device(string line_name, ushort address)
            {
                line = line_name;
                addr = address;
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum Result
        {
            ongoing,
            ok,
            disconnect,
            error,
            timeout,
            busy,
            badCmd,
            notExist,
            notExists,
            badCrc,
            badInp,
            saveError
        }
        #endregion

        #region Login
        public class MsgLogin : Header
        {
            public string pcName;
            public string appName;

            public MsgLogin(string pc_name, string application_name) : base(Command.login)
            {
                pcName = pc_name;
                appName = application_name;
            }
        }

        public class MsgLoginResp : Header
        {
            public float version;
        }
        #endregion

        #region DeviceList
        [JsonConverter(typeof(StringEnumConverter))]
        public enum ConnectionState
        {
            na,
            disconnect,
            connecting,
            connect
        }

        public struct LineInfo
        {
            public string name;
            public string protocol;
            public ConnectionState state;
            public List<DeviceInfo> devices;
        }

        public class DeviceInfo
        {
            public ushort addr { get; set; }
            public string mac { get; set; }
            public bool online { get; set; }
            public string sch_name { get; set; }
            public bool sch_on { get; set; }
            public bool sch_has_plan { get; set; }
            public uint dev_id { get; set; }
            public uint[] type { get; set; }
            public string type_str { get; set; }
            public string name { get; set; }
        }

        public class MsgDevlist : Header
        {
            public MsgDevlist() : base(Command.device_list) { }
        }

        public class MsgDevlistResp : Header
        {
            public LineInfo[] lines;
        }
        #endregion

        #region Action
        [JsonConverter(typeof(StringEnumConverter))]
        public enum PassageAction
        {
            passL,
            passR,
            partpassL,
            partpassR,
            passL_verify,
            passR_verify
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum BarrierAction
        {
            close,
            openL,
            openR
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ModeOffOn
        {
            off,
            on
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TurnstileMode
        {
            all_modes_off,
            free_off,
            free_on,
            lockdown_off,
            lockdown_on,
            optical_off,
            optical_on,
            group_off,
            group_on,
            cflow_off,
            cflow_on
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum SideMode
        {
            normal,
            free,
            lockdown
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum SecuritySafetyMode
        {
            maxsec,
            lowsaf,
            midsaf,
            highsaf,
            maxsaf
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ElectronicAction
        {
            bridge_off,
            bridge_on,
            bootloader,
            reset
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum Verification
        {
            ok,
            failed
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ExtSignal
        {
            off,
            in1,
            in2,
            in3
        }

        public class MsgAction : Header
        {
            public PassageAction? passage; // passL, passR, partpassL, partpassR
            public BarrierAction? barrier1; // close, openL, openR
            public BarrierAction? barrier2; // close, openL, openR
            public ModeOffOn? emerg; // off, on
            public ModeOffOn? state; // off, on
            public TurnstileMode? mode; // optical_off, optical_on, cflow_off, cflow_on
            public SideMode? modeL; // normal, free, lockdown
            public SideMode? modeR; // normal, free, lockdown
            public SecuritySafetyMode? safety; // maxsec, lowsaf, midsaf, highsaf, maxsaf
            public Verification? verification; // ok, failed
            public ElectronicAction? electronic; // reset, bootloader
            public ExtSignal? ext_signal; // off, in1, in2, in3

            public MsgAction() : base(Command.action) { }

            public MsgAction(Device device) : base(Command.action, device) { }
        }

        public class MsgActionResp : Header
        {
            public Result result;
            public string[] events;
        }
        #endregion

        #region DeviceEvent
        [JsonConverter(typeof(StringEnumConverter))]
        public enum DevEvent
        {
            reset,
            errorM,
            errorS,
            emerg,
            deviceOn,
            modeFree,
            modeLockdown,
            modeOpt,
            modeGroup,
            modeCf,
            modeBridge,
            passL,
            passR,
            partOpen,
            freeL,
            freeR,
            lockL,
            lockR,
            passTO,
            realL,
            realR,
            bikeIn,
            personInL,
            personInR,
            validation,
            alarmBreach,
            alarmVandalism,
            alarmTailgating,
            alarmCrossover,
            vandalismEntryDoor,
            vandalismExitDoor,
            alarmJumpIn,
            alarmJumpOut,
            alarmStandard,
            alarmWarning,
            passing,
            turnback,
            barrierOpened,
            openingEntryDoorL,
            openingEntryDoorR,
            openingExitDoorL,
            openingExitDoorR,
            openedEntryDoorL,
            openedEntryDoorR,
            openedExitDoorL,
            openedExitDoorR,
            closingEntryDoor,
            closingExitDoor,
            closedEntryDoor,
            closedExitDoor,
            entryDoorOpened,
            exitDoorOpened,
            blockedEntryDoor,
            blockedExitDoor,
            leftItem,
            input1,
            input2,
            input3,
            input4
        }

        public class MsgDevEvent : Header
        {
            public string[] events;
        }
        #endregion
    }
}
