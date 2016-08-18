namespace MsTestRunner
{
    using System;
    using System.Security.Cryptography;

    public sealed class TestResult
    {
        private static readonly HashAlgorithm Provider = new SHA1CryptoServiceProvider();
            
        public TestResult(string name, string output, bool success, TimeSpan duration)
        {
            this.Name = name;
            this.Output = output;
            this.Success = success;
            this.Id = GuidFromString(name);
            this.Duration = duration;
        }

        public readonly Guid Id;

        public readonly Guid ExecutionId = Guid.NewGuid();

        public readonly string Name;

        public readonly string Output;

        public readonly bool Success;

        public readonly TimeSpan Duration;

        /// Consistent Id based on string as done by MsTest
        private static Guid GuidFromString(string data)
        {
            byte[] hash;
            lock (Provider)
            {
                hash = Provider.ComputeHash(System.Text.Encoding.Unicode.GetBytes(data));
            }

            byte[] toGuid = new byte[16];
            Array.Copy(hash, toGuid, 16);

            return new Guid(toGuid);
        }
    }
}