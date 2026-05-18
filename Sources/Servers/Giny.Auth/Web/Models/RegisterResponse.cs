using Giny.Protocol.IPC.Types;

namespace Giny.Auth.Web.Models
{
    /// <summary>
    /// Réponse uniforme de POST /account/register. La même shape JSON est
    /// dupliquée côté launcher (Giny.Zaap.Accounts.RegisterResponse) pour éviter
    /// une dépendance Giny.Auth → Giny.Zaap.
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

        public Account? Account
        {
            get;
            set;
        }

        public static RegisterResponse Fail(string message)
            => new RegisterResponse { Success = false, Message = message };

        public static RegisterResponse Ok(Account account)
            => new RegisterResponse { Success = true, Message = string.Empty, Account = account };
    }
}
