namespace Giny.Zaap.Accounts
{
    /// <summary>
    /// Miroir client-side de Giny.Auth.Web.Models.RegisterResponse.
    /// Même shape JSON ; classes séparées pour ne pas créer de référence
    /// Giny.Auth → Giny.Zaap.
    /// </summary>
    public class RegisterResponse
    {
        public bool Success
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        } = string.Empty;

        public WebAccount? Account
        {
            get;
            set;
        }
    }
}
