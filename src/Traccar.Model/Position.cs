namespace Traccar.Model;

public class Position : Message
{
    public const string KeyOriginal = "raw";
    public const string KeyIndex = "index";
    public const string KeyHdop = "hdop";
    public const string KeyVdop = "vdop";
    public const string KeyPdop = "pdop";
    public const string KeySatellites = "sat";
    public const string KeySatellitesVisible = "satVisible";
    public const string KeyRssi = "rssi";
    public const string KeyGps = "gps";
    public const string KeyRoaming = "roaming";
    public const string KeyEvent = "event";
    public const string KeyAlarm = "alarm";
    public const string KeyStatus = "status";
    public const string KeyOdometer = "odometer";
    public const string KeyOdometerService = "serviceOdometer";
    public const string KeyOdometerTrip = "tripOdometer";
    public const string KeyHours = "hours";
    public const string KeySteps = "steps";
    public const string KeyHeartRate = "heartRate";
    public const string KeyInput = "input";
    public const string KeyOutput = "output";
    public const string KeyImage = "image";
    public const string KeyVideo = "video";
    public const string KeyAudio = "audio";

    public const string KeyPower = "power";
    public const string KeyBattery = "battery";
    public const string KeyBatteryLevel = "batteryLevel";
    public const string KeyFuel = "fuel";
    public const string KeyFuelUsed = "fuelUsed";
    public const string KeyFuelConsumption = "fuelConsumption";
    public const string KeyFuelLevel = "fuelLevel";

    public const string KeyVersionFw = "versionFw";
    public const string KeyVersionHw = "versionHw";
    public const string KeyType = "type";
    public const string KeyIgnition = "ignition";
    public const string KeyFlags = "flags";
    public const string KeyAntenna = "antenna";
    public const string KeyCharge = "charge";
    public const string KeyIp = "ip";
    public const string KeyArchive = "archive";
    public const string KeyDistance = "distance";
    public const string KeyTotalDistance = "totalDistance";
    public const string KeyRpm = "rpm";
    public const string KeyVin = "vin";
    public const string KeyApproximate = "approximate";
    public const string KeyThrottle = "throttle";
    public const string KeyMotion = "motion";
    public const string KeyArmed = "armed";
    public const string KeyGeofence = "geofence";
    public const string KeyAcceleration = "acceleration";
    public const string KeyHumidity = "humidity";
    public const string KeyDeviceTemp = "deviceTemp";
    public const string KeyCoolantTemp = "coolantTemp";
    public const string KeyEngineLoad = "engineLoad";
    public const string KeyEngineTemp = "engineTemp";
    public const string KeyOperator = "operator";
    public const string KeyCommand = "command";
    public const string KeyBlocked = "blocked";
    public const string KeyLock = "lock";
    public const string KeyDoor = "door";
    public const string KeyAxleWeight = "axleWeight";
    public const string KeyGSensor = "gSensor";
    public const string KeyIccid = "iccid";
    public const string KeyPhone = "phone";
    public const string KeySpeedLimit = "speedLimit";
    public const string KeyDrivingTime = "drivingTime";

    public const string KeyDtcs = "dtcs";
    public const string KeyObdSpeed = "obdSpeed";
    public const string KeyObdOdometer = "obdOdometer";

    public const string KeyResult = "result";

    public const string KeyDriverUniqueId = "driverUniqueId";
    public const string KeyCard = "card";

    public const string PrefixTemp = "temp";
    public const string PrefixAdc = "adc";
    public const string PrefixIo = "io";
    public const string PrefixCount = "count";
    public const string PrefixIn = "in";
    public const string PrefixOut = "out";

    public const string AlarmGeneral = "general";
    public const string AlarmSos = "sos";
    public const string AlarmVibration = "vibration";
    public const string AlarmMovement = "movement";
    public const string AlarmLowSpeed = "lowspeed";
    public const string AlarmOverspeed = "overspeed";
    public const string AlarmFallDown = "fallDown";
    public const string AlarmLowPower = "lowPower";
    public const string AlarmLowBattery = "lowBattery";
    public const string AlarmFault = "fault";
    public const string AlarmPowerOff = "powerOff";
    public const string AlarmPowerOn = "powerOn";
    public const string AlarmDoor = "door";
    public const string AlarmLock = "lock";
    public const string AlarmUnlock = "unlock";
    public const string AlarmGeofence = "geofence";
    public const string AlarmGeofenceEnter = "geofenceEnter";
    public const string AlarmGeofenceExit = "geofenceExit";
    public const string AlarmGpsAntennaCut = "gpsAntennaCut";
    public const string AlarmAccident = "accident";
    public const string AlarmTow = "tow";
    public const string AlarmIdle = "idle";
    public const string AlarmHighRpm = "highRpm";
    public const string AlarmAcceleration = "hardAcceleration";
    public const string AlarmBraking = "hardBraking";
    public const string AlarmCornering = "hardCornering";
    public const string AlarmLaneChange = "laneChange";
    public const string AlarmFatigueDriving = "fatigueDriving";
    public const string AlarmPowerCut = "powerCut";
    public const string AlarmPowerRestored = "powerRestored";
    public const string AlarmJamming = "jamming";
    public const string AlarmTemperature = "temperature";
    public const string AlarmParking = "parking";
    public const string AlarmBonnet = "bonnet";
    public const string AlarmFootBrake = "footBrake";
    public const string AlarmFuelLeak = "fuelLeak";
    public const string AlarmTampering = "tampering";
    public const string AlarmRemoving = "removing";

    public Position() { }

    public Position(string protocol)
    {
        Protocol = protocol;
    }

    public string? Protocol { get; set; }

    public DateTime ServerTime { get; set; } = DateTime.UtcNow;

    public DateTime? DeviceTime { get; set; }

    public DateTime? FixTime { get; set; }

    public void SetTime(DateTime time)
    {
        DeviceTime = time;
        FixTime = time;
    }

    public bool Outdated { get; set; }

    public bool Valid { get; set; }

    private double latitude;

    public double Latitude
    {
        get => latitude;
        set
        {
            if (value is < -90 or > 90)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Latitude out of range");
            }
            latitude = value;
        }
    }

    private double longitude;

    public double Longitude
    {
        get => longitude;
        set
        {
            if (value is < -180 or > 180)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Longitude out of range");
            }
            longitude = value;
        }
    }

    public double Altitude { get; set; }

    public double Speed { get; set; }

    public double Course { get; set; }

    public string? Address { get; set; }

    public double Accuracy { get; set; }

    public Network? Network { get; set; }

    public List<long>? GeofenceIds { get; set; }

    public void AddAlarm(string? alarm)
    {
        if (alarm is null)
        {
            return;
        }
        if (HasAttribute(KeyAlarm))
        {
            Set(KeyAlarm, Attributes[KeyAlarm] + "," + alarm);
        }
        else
        {
            Set(KeyAlarm, alarm);
        }
    }
}
