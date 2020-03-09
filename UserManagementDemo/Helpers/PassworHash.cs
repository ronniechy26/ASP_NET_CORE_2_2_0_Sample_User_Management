using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace UserManagementDemo.Helpers
{
    public static class PasswordHash
    {
        private const int SALT_SIZE = 8;
        private const int NUM_ITERATIONS = 6000;

        private static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        public static string CreatePasswordSalt(string password)
        {
            byte[] buf = new byte[SALT_SIZE];
            rng.GetBytes(buf);
            string salt = Convert.ToBase64String(buf);

            Rfc2898DeriveBytes deriver2898 = new Rfc2898DeriveBytes(password.Trim(), buf,
                NUM_ITERATIONS, HashAlgorithmName.SHA256);
            string hash = Convert.ToBase64String(deriver2898.GetBytes(32));
            return hash + ':' + salt;
        }
        public static bool IsPasswordValid(string password, string saltHash)
        {
            string[] parts = saltHash.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                return false;

            byte[] buf = Convert.FromBase64String(parts[1]);
            Rfc2898DeriveBytes deriver2898 = new Rfc2898DeriveBytes(password.Trim(), buf,
                NUM_ITERATIONS, HashAlgorithmName.SHA256);
            string computedHash = Convert.ToBase64String(deriver2898.GetBytes(32));
            return parts[0].Equals(computedHash);
        }

        public static string GenerateRandomPassword()
        {
            System.Random rnd = new System.Random();
            const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890";
            string temp = "";

            for (int i = 0; i < 8; i++)
            {
                temp = temp + alpha[rnd.Next(61)];
            }

            return temp;

        }

    }
}

