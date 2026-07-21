namespace Traccar.Protocols;

/// <summary>
/// Central repository of configuration key strings and related constants, eliminating bare string
/// literals scattered across configuration reads. Nested classes mirror the appsettings.json
/// section hierarchy so usages are self-documenting (e.g. ConfigKeys.Forward.Retry.Delay).
/// </summary>
public static class ConfigKeys
{
    /// <summary>"Protocols:*" section — per-protocol settings.</summary>
    public static class Protocols
    {
        /// <summary>Comma- or space-separated list of protocol names to activate. Omit to activate all configured ones.</summary>
        public const string Enable = "Protocols:Enable";

        /// <summary>Top-level section name; combine with a protocol name to build "Protocols:{name}".</summary>
        public const string SectionPrefix = "Protocols";

        /// <summary>TCP/UDP port sub-key within a protocol section.</summary>
        public const string Port = "Port";

        /// <summary>Idle-disconnect timeout sub-key (seconds) within a protocol section.</summary>
        public const string Timeout = "Timeout";

        /// <summary>Enables Codec8-Extended mode for Teltonika decoders.</summary>
        public const string Extended = "Extended";

        /// <summary>Enables the alternative command encoding variant (Jt808, Meitrack, Meiligao).</summary>
        public const string Alternative = "Alternative";

        /// <summary>When true, uses the server-received time instead of the device-reported fix time (Gl200).</summary>
        public const string IgnoreFixTime = "IgnoreFixTime";

        /// <summary>When true, sends a +SACK acknowledgement for every decoded text sentence (Gl200).</summary>
        public const string Ack = "Ack";
    }

    /// <summary>"Forward:*" section — position forwarding to an external broker.</summary>
    public static class Forward
    {
        /// <summary>Forwarding back-end: "kafka" or "rabbitmq". Omit to disable forwarding.</summary>
        public const string Type = "Forward:Type";

        /// <summary>Broker URL: Kafka bootstrap servers or RabbitMQ AMQP URI.</summary>
        public const string Url = "Forward:Url";

        /// <summary>Kafka topic or RabbitMQ routing key for position messages.</summary>
        public const string Topic = "Forward:Topic";

        /// <summary>RabbitMQ exchange name (topic exchange, durable).</summary>
        public const string Exchange = "Forward:Exchange";

        /// <summary>Value for <see cref="Type"/> that selects the Kafka forwarder.</summary>
        public const string TypeKafka = "kafka";

        /// <summary>Value for <see cref="Type"/> that selects the RabbitMQ/AMQP forwarder.</summary>
        public const string TypeRabbitMq = "rabbitmq";

        /// <summary>Value for <see cref="Type"/> that selects the SignalR hub forwarder.</summary>
        public const string TypeSignalR = "signalr";

        /// <summary>Default topic/routing-key when <see cref="Topic"/> is not configured.</summary>
        public const string DefaultTopic = "positions";

        /// <summary>Default SignalR hub method name when <see cref="Topic"/> is not configured.</summary>
        public const string DefaultSignalRMethod = "PositionReceived";

        /// <summary>Default RabbitMQ exchange when <see cref="Exchange"/> is not configured.</summary>
        public const string DefaultExchange = "traccar";

        /// <summary>"Forward:Retry:*" sub-section — delivery retry / back-off settings.</summary>
        public static class Retry
        {
            public const string Enable = "Forward:Retry:Enable";
            public const string Delay = "Forward:Retry:Delay";
            public const string Count = "Forward:Retry:Count";
            public const string Limit = "Forward:Retry:Limit";
        }
    }

    /// <summary>"Server:*" section — global server settings.</summary>
    public static class Server
    {
        /// <summary>Public-facing web URL used when building absolute links (e.g. JT808 stream URIs).</summary>
        public const string WebUrl = "Server:WebUrl";
    }

    /// <summary>"Database:*" section — PostgreSQL connection retry behaviour.</summary>
    public static class Database
    {
        /// <summary>"Database:Retry:*" sub-section — transient-fault retry settings.</summary>
        public static class Retry
        {
            public const string Enable = "Database:Retry:Enable";
            public const string MaxRetryCount = "Database:Retry:MaxRetryCount";
            public const string MaxRetryDelaySeconds = "Database:Retry:MaxRetryDelaySeconds";
            public const string CommandTimeoutSeconds = "Database:Retry:CommandTimeoutSeconds";
        }
    }

    /// <summary>"Admin:*" section — bootstrap administrator credentials.</summary>
    public static class Admin
    {
        public const string Email = "Admin:Email";
        public const string Password = "Admin:Password";
    }

    /// <summary>"Report:*" section — report generation settings.</summary>
    public static class Report
    {
        /// <summary>Max allowed report period in seconds. 0 = no limit (default).</summary>
        public const string PeriodLimit = "Report:PeriodLimit";

        /// <summary>
        /// Periods longer than this (seconds) use the fast (event-based) algorithm instead of
        /// replaying every position. Default: 86400 (1 day).
        /// </summary>
        public const string FastThreshold = "Report:FastThreshold";

        /// <summary>Trip/stop detection thresholds.</summary>
        public static class Trip
        {
            /// <summary>Minimum distance (metres) for a movement to count as a trip. Default: 500.</summary>
            public const string MinimalDistance = "Report:Trip:MinimalDistance";

            /// <summary>Minimum duration (seconds) for a movement to count as a trip. Default: 300.</summary>
            public const string MinimalDuration = "Report:Trip:MinimalDuration";

            /// <summary>Minimum parking duration (seconds) before a stop is confirmed. Default: 300.</summary>
            public const string MinimalParking = "Report:Trip:MinimalParking";

            /// <summary>Gap longer than this (seconds) is treated as a stop regardless of motion flag. Default: 3600.</summary>
            public const string MinimalNoData = "Report:Trip:MinimalNoData";

            /// <summary>Use the ignition attribute to confirm trip start/end. Default: false.</summary>
            public const string UseIgnition = "Report:Trip:UseIgnition";

            /// <summary>Ignore device odometer; use server-calculated total distance instead. Default: false.</summary>
            public const string IgnoreOdometer = "Report:Trip:IgnoreOdometer";
        }
    }

    /// <summary>"Filter:*" section — position filtering settings.</summary>
    public static class Filter
    {
        public const string Invalid = "Filter:Invalid";
        public const string Zero = "Filter:Zero";
        public const string Duplicate = "Filter:Duplicate";
        public const string Outdated = "Filter:Outdated";
        /// <summary>Seconds ahead of now; positions further in the future are dropped.</summary>
        public const string Future = "Filter:Future";
        /// <summary>Seconds behind now; positions older than this are dropped.</summary>
        public const string Past = "Filter:Past";
        /// <summary>Accuracy threshold in metres; positions less accurate are dropped.</summary>
        public const string Accuracy = "Filter:Accuracy";
        public const string Approximate = "Filter:Approximate";
        /// <summary>Drop positions where speed == 0.</summary>
        public const string Static = "Filter:Static";
        /// <summary>Minimum moved distance in metres between consecutive positions.</summary>
        public const string Distance = "Filter:Distance";
        /// <summary>Maximum plausible speed in knots; positions implying higher speed are dropped.</summary>
        public const string MaxSpeed = "Filter:MaxSpeed";
        /// <summary>Minimum time in seconds between consecutive positions.</summary>
        public const string MinPeriod = "Filter:MinPeriod";
        /// <summary>Seconds of server silence after which excessive-data filters are bypassed.</summary>
        public const string SkipLimit = "Filter:SkipLimit";
        /// <summary>Comma/space-separated attribute names; filter is bypassed if any value changed.</summary>
        public const string SkipAttributes = "Filter:SkipAttributes";
    }

    /// <summary>"Events:*" section — event detection thresholds.</summary>
    public static class Events
    {
        /// <summary>
        /// When true, suppress duplicate alarm events if the previous position carried the same
        /// alarm value. Mirrors Java's Keys.EVENT_IGNORE_DUPLICATE_ALERTS. Default false.
        /// </summary>
        public const string IgnoreDuplicateAlerts = "Events:IgnoreDuplicateAlerts";

        /// <summary>"Events:Motion:*" — motion detection settings.</summary>
        public static class Motion
        {
            /// <summary>Speed threshold in knots below which the device is considered stationary. Default 0.01.</summary>
            public const string SpeedThreshold = "Events:Motion:SpeedThreshold";
        }
    }

    /// <summary>"Processing:*" section — position enrichment pipeline settings.</summary>
    public static class Processing
    {
        /// <summary>
        /// Comma- or space-separated attribute keys to propagate from the previous position when
        /// the current position does not include them. E.g. "odometer,hours".
        /// </summary>
        public const string CopyAttributes = "Processing:CopyAttributes";

        /// <summary>
        /// When true, per-device computed attributes also expose the device's own attribute
        /// dictionary as top-level variables in the NCalc context.
        /// </summary>
        public const string ComputedAttributesDeviceAttributes = "Processing:ComputedAttributes:DeviceAttributes";

        /// <summary>
        /// When true, the previous position's fields are exposed as "lastX" variables in the
        /// NCalc context (e.g. "lastSpeed", "lastOdometer").
        /// </summary>
        public const string ComputedAttributesLastAttributes = "Processing:ComputedAttributes:LastAttributes";
    }

    /// <summary>"Coordinates:*" section — GPS anti-jitter filter settings.</summary>
    public static class Coordinates
    {
        /// <summary>Enable the coordinate anti-jitter filter. Default false.</summary>
        public const string Filter = "Coordinates:Filter";

        /// <summary>
        /// Minimum plausible movement in metres. Positions that moved less than this from the
        /// previous fix are replaced with the previous fix's coordinates. 0 = disabled.
        /// </summary>
        public const string MinError = "Coordinates:MinError";

        /// <summary>
        /// Maximum plausible single-step distance in metres. Positions that jumped further than
        /// this are replaced with the previous fix. 0 = disabled.
        /// </summary>
        public const string MaxError = "Coordinates:MaxError";
    }

    /// <summary>"Geocoder:*" section — reverse geocoding settings.</summary>
    public static class Geocoder
    {
        /// <summary>Geocoder backend type. Currently only "nominatim" is supported.</summary>
        public const string Type = "Geocoder:Type";

        /// <summary>Override the geocoder API URL (defaults to the official Nominatim endpoint).</summary>
        public const string Url = "Geocoder:Url";

        /// <summary>API key for geocoders that require one (not needed for Nominatim).</summary>
        public const string Key = "Geocoder:Key";

        /// <summary>Preferred language for address results (BCP 47 tag, e.g. "en").</summary>
        public const string Language = "Geocoder:Language";

        /// <summary>Maximum number of geocoded addresses to cache in memory. Default 512.</summary>
        public const string CacheSize = "Geocoder:CacheSize";

        /// <summary>
        /// If the device has moved less than this many metres since the last geocoded position,
        /// reuse the cached address instead of making an HTTP call. Default 0 (always geocode).
        /// </summary>
        public const string ReuseDistance = "Geocoder:ReuseDistance";

        /// <summary>
        /// When true, skip geocoding for positions whose coordinates are not valid GPS fixes
        /// (e.g. positions filled in by OutdatedHandler from the last known position). Default false.
        /// </summary>
        public const string IgnorePositions = "Geocoder:IgnorePositions";

        /// <summary>Address format pattern (e.g. "%h %r, %t, %s, %c"). Default: "%h %r, %t, %s, %c".</summary>
        public const string Format = "Geocoder:Format";
    }

    /// <summary>"Logger:*" section — protocol data logging settings.</summary>
    public static class Logger
    {
        /// <summary>
        /// When false, raw protocol frames are always logged as hex. When true (default), printable
        /// ASCII payloads are decoded to text; binary payloads are hex-dumped.
        /// </summary>
        public const string TextProtocol = "Logger:TextProtocol";
    }

    /// <summary>Authentication / authorisation constants shared between Program.cs and controllers.</summary>
    public static class Auth
    {
        /// <summary>HTTP cookie name for the session token.</summary>
        public const string CookieName = "traccar_session";

        /// <summary>Role name assigned to administrator accounts; used in [Authorize(Roles = ...)] attributes.</summary>
        public const string RoleAdministrator = "Administrator";
    }
}
