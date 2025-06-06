using System.Security.Cryptography;
using System.Text;

namespace Assist_Service.Helpers
{
    public static class SecurityHelper
    {
        private const string SharedSecret = "MySuperSecretKey";

        public static string GenerateToken(string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return Convert.ToBase64String(hash);
            }
        }

        public static bool ValidateToken(string message, string receivedToken)
        {
            string expectedToken = GenerateToken(message);
            return receivedToken == expectedToken;
        }
    }
}