using BCrypt.Net;
using System;

namespace Giny.Auth.Security
{
    /// <summary>
    /// Hashing + vérification des mots de passe via BCrypt. Conserve la
    /// compatibilité avec les comptes legacy stockés en clair : Verify() détecte
    /// le format et compare en clair si le stored n'est pas un hash BCrypt.
    ///
    /// La migration se fait au login : voir AccountController.Auth /
    /// ConnectionHandler.ProcessIdentification.
    /// </summary>
    public static class PasswordHasher
    {
        // Work factor 11 = ~150-300ms par hash sur un CPU desktop moderne.
        // Suffisamment lent pour décourager le brute-force, suffisamment rapide
        // pour ne pas bloquer le thread auth de manière visible.
        private const int WorkFactor = 11;

        public static string Hash(string plain)
        {
            if (plain == null)
                throw new ArgumentNullException(nameof(plain));

            return BCrypt.Net.BCrypt.HashPassword(plain, WorkFactor);
        }

        public static bool IsHashed(string stored)
        {
            if (string.IsNullOrEmpty(stored) || stored.Length < 4)
                return false;

            // Préfixes BCrypt standards : $2a$, $2b$, $2y$.
            return stored.StartsWith("$2a$", StringComparison.Ordinal)
                || stored.StartsWith("$2b$", StringComparison.Ordinal)
                || stored.StartsWith("$2y$", StringComparison.Ordinal);
        }

        public static bool Verify(string plain, string stored)
        {
            if (plain == null || stored == null)
                return false;

            if (IsHashed(stored))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(plain, stored);
                }
                catch (SaltParseException)
                {
                    // Hash corrompu / malformé → on refuse l'auth.
                    return false;
                }
            }

            // Compte legacy : comparaison brute en clair.
            return stored == plain;
        }
    }
}
