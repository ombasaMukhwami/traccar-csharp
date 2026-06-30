namespace Traccar.Model;

public class Command : Message
{
    public const string TypeCustom = "custom";
    public const string TypeIdentification = "deviceIdentification";
    public const string TypePositionSingle = "positionSingle";
    public const string TypePositionPeriodic = "positionPeriodic";
    public const string TypePositionStop = "positionStop";
    public const string TypeEngineStop = "engineStop";
    public const string TypeEngineResume = "engineResume";
    public const string TypeAlarmArm = "alarmArm";
    public const string TypeAlarmDisarm = "alarmDisarm";
    public const string TypeAlarmDismiss = "alarmDismiss";
    public const string TypeSetTimezone = "setTimezone";
    public const string TypeRequestPhoto = "requestPhoto";
    public const string TypePowerOff = "powerOff";
    public const string TypeRebootDevice = "rebootDevice";
    public const string TypeFactoryReset = "factoryReset";
    public const string TypeSendSms = "sendSms";
    public const string TypeSendUssd = "sendUssd";
    public const string TypeSosNumber = "sosNumber";
    public const string TypeSilenceTime = "silenceTime";
    public const string TypeSetPhonebook = "setPhonebook";
    public const string TypeMessage = "message";
    public const string TypeVoiceMessage = "voiceMessage";
    public const string TypeOutputControl = "outputControl";
    public const string TypeVoiceMonitoring = "voiceMonitoring";
    public const string TypeSetAgps = "setAgps";
    public const string TypeSetIndicator = "setIndicator";
    public const string TypeConfiguration = "configuration";
    public const string TypeGetVersion = "getVersion";
    public const string TypeFirmwareUpdate = "firmwareUpdate";
    public const string TypeSetConnection = "setConnection";
    public const string TypeSetOdometer = "setOdometer";
    public const string TypeGetModemStatus = "getModemStatus";
    public const string TypeGetDeviceStatus = "getDeviceStatus";
    public const string TypeSetSpeedLimit = "setSpeedLimit";
    public const string TypeModePowerSaving = "modePowerSaving";
    public const string TypeModeDeepSleep = "modeDeepSleep";
    public const string TypeVideoStart = "videoStart";
    public const string TypeVideoStop = "videoStop";

    public const string TypeAlarmGeofence = "alarmGeofence";
    public const string TypeAlarmBattery = "alarmBattery";
    public const string TypeAlarmSos = "alarmSos";
    public const string TypeAlarmRemove = "alarmRemove";
    public const string TypeAlarmClock = "alarmClock";
    public const string TypeAlarmSpeed = "alarmSpeed";
    public const string TypeAlarmFall = "alarmFall";
    public const string TypeAlarmVibration = "alarmVibration";

    public const string KeyUniqueId = "uniqueId";
    public const string KeyFrequency = "frequency";
    public const string KeyLanguage = "language";
    public const string KeyTimezone = "timezone";
    public const string KeyDevicePassword = "devicePassword";
    public const string KeyRadius = "radius";
    public const string KeyMessage = "message";
    public const string KeyEnable = "enable";
    public const string KeyData = "data";
    public const string KeyIndex = "index";
    public const string KeyPhone = "phone";
    public const string KeyServer = "server";
    public const string KeyPort = "port";
    public const string KeyNoQueue = "noQueue";

    public bool TextChannel { get; set; }

    public string? Description { get; set; }
}
