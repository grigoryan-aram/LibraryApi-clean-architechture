namespace Infrastructure.Settings
{
    public class RabbitMqSettings
    {
        public string Host { get; set; } = string.Empty;

        public ushort Port { get; set; } = 5672;

        public string VirtualHost { get; set; } = "/";

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Whether a broker is configured at all. With no host the bus is
        /// never registered and publishing becomes a no-op, so a deployment
        /// that has no RabbitMQ keeps working exactly as before.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host)
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password);
    }
}
