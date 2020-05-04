using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace libTWM
{
    public interface IRandomProvider
    {
        double NextDouble();
    }

    public class SystemRandomProvider : IRandomProvider
    {
        private static readonly Random RNG = new Random();

        public double NextDouble()
        {
            return RNG.NextDouble();
        }
    }

    public class CryptoRandomProvider : IRandomProvider, IDisposable
    {
        private static readonly RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider();

        public double NextDouble()
        {
            var bytes = new byte[8];
            RNG.GetBytes(bytes);
            var ul = BitConverter.ToUInt64(bytes, 0) / (1 << 11);
            return ul / (double)(1UL << 53);
        }

        public void Dispose()
        {
            RNG.Dispose();
        }
    }
}
