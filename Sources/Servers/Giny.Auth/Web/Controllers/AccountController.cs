using Giny.Auth.Records;
using Giny.Auth.Security;
using Giny.Auth.Web.Models;
using Giny.Core;
using Giny.Core.Misc;
using Giny.ORM;
using Giny.Protocol.Custom.Enums;
using Giny.Protocol.IPC.Types;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Giny.World.Web.Controllers
{
    public class CredentialsRequest
    {
        public string Username
        {
            get;
            set;
        }
        public string Password
        {
            get;
            set;
        }
    }
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        [HttpPost]
        [Route("auth")]
        public Account? Auth(CredentialsRequest request)
        {
            System.Threading.Thread.Sleep(500);
            var record = AccountRecord.ReadAccount(request.Username);

            if (record == null || !PasswordHasher.Verify(request.Password, record.Password))
            {
                return null;
            }

            // Migration transparente : si le stored n'est pas encore un hash
            // BCrypt mais que la vérif a passé (== comparaison en clair OK),
            // on hash et on persiste immédiatement.
            if (!PasswordHasher.IsHashed(record.Password))
            {
                record.Password = PasswordHasher.Hash(request.Password);
                record.UpdateNow();
                Logger.Write($"(Auth) Password migrated to BCrypt for account #{record.Id} ({record.Username}).", Channels.Info);
            }

            return record.ToAccount();
        }

        private const int MinUsernameLength = 3;
        private const int MaxUsernameLength = 20;
        private const int MinPasswordLength = 4;
        private static readonly Regex UsernameRegex = new Regex(
            @"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

        [HttpPost]
        [Route("register")]
        public RegisterResponse Register(CredentialsRequest request)
        {
            var username = request?.Username?.Trim() ?? string.Empty;
            var password = request?.Password ?? string.Empty;

            if (username.Length < MinUsernameLength)
                return RegisterResponse.Fail($"Nom de compte trop court ({MinUsernameLength} caractères minimum).");

            if (username.Length > MaxUsernameLength)
                return RegisterResponse.Fail($"Nom de compte trop long ({MaxUsernameLength} caractères maximum).");

            if (!UsernameRegex.IsMatch(username))
                return RegisterResponse.Fail("Nom de compte invalide (lettres, chiffres et _ uniquement).");

            if (password.Length < MinPasswordLength)
                return RegisterResponse.Fail($"Mot de passe trop court ({MinPasswordLength} caractères minimum).");

            // UsernameExist passe par DatabaseReader.ReadFirst → la collation
            // SQL (latin1_swedish_ci) rend la comparaison case-insensitive.
            if (AccountRecord.UsernameExist(username))
                return RegisterResponse.Fail("Nom de compte déjà utilisé.");

            // Hash dès la création — pas de stockage en clair même temporaire.
            var hashed = PasswordHasher.Hash(password);

            try
            {
                var record = AccountRecord.CreateAccount(username, hashed, ServerRoleEnum.Player);
                record.AddNow();
                Logger.Write($"(Auth) New account registered : #{record.Id} ({record.Username}).", Channels.Info);
                return RegisterResponse.Ok(record.ToAccount());
            }
            catch (System.Exception ex)
            {
                Logger.Write($"(Auth) Register failed for '{username}' : {ex}", Channels.Critical);
                return RegisterResponse.Fail("Erreur serveur durant la création du compte.");
            }
        }
    }
}