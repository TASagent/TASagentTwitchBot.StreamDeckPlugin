namespace TASagentBotPlugin
{
    class BotConfiguration
    {
        public AuthConfiguration AuthConfiguration { get; set; } = new AuthConfiguration();
    }

    class AuthConfiguration
    {
        public CredentialSet Admin { get; set; } = new CredentialSet();
    }

    class CredentialSet
    {
        public string AuthString { get; set; } = "";
    }

}
