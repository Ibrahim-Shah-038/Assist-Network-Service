/******************************************************************************
* Module: Helpers/SecurityHelper.cs
* Description: Security-related helper functions
* Created: 2025-05-24
* Author: Your Name
******************************************************************************/

using System;
using System.Security.Cryptography;
using System.Text;

namespace Assist_Service.Helpers
{
    /// <summary>
    /// Provides security-related helper methods
    /// </summary>
    public static class SecurityHelper
    {
        private const string SharedSecret = "MySuperSecretKey";

        /// <summary>
        /// Generates a security token
        /// </summary>
        public static string GenerateToken(string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Validates a security token
        /// </summary>
        public static bool ValidateToken(string message, string receivedToken)
        {
            return receivedToken == GenerateToken(message);
        }
    }
}