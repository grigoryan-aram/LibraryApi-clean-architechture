namespace Infrastructure.Settings
{
    public class EmailSettings
    {
        public string From { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public SmtpSettings Smtp { get; set; } = new();
    }


}
