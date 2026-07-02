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

        /// <summary>Default topic/routing-key when <see cref="Topic"/> is not configured.</summary>
        public const string DefaultTopic = "positions";

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

    /// <summary>"Database:*" section — RDBMS provider selection and connection behaviour.</summary>
    public static class Database
    {
        /// <summary>
        /// Active database provider. Supported values: "sqlite" (default), "postgresql",
        /// "mysql", "mariadb", "sqlserver". Case-insensitive.
        /// </summary>
        public const string Provider = "Database:Provider";

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

    /// <summary>Authentication / authorisation constants shared between Program.cs and controllers.</summary>
    public static class Auth
    {
        /// <summary>HTTP cookie name for the session token.</summary>
        public const string CookieName = "traccar_session";

        /// <summary>Role name assigned to administrator accounts; used in [Authorize(Roles = ...)] attributes.</summary>
        public const string RoleAdministrator = "Administrator";
    }
}
