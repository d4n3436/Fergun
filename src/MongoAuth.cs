using System;
using Newtonsoft.Json;

namespace Fergun
{
    /// <summary>
    /// A simple MongoDB auth class.
    /// </summary>
    public class MongoAuth
    {
        /// <summary>
        /// The user.
        /// </summary>
        [JsonProperty("user")]
        public string User { get; set; } = null;

        /// <summary>
        /// The password.
        /// </summary>
        [JsonProperty("password")]
        public string Password { get; set; } = null;

        /// <summary>
        /// The host where the mongod instance is running. Defaults to <c>localhost</c>.
        /// </summary>
        [JsonProperty("host")]
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// The port where the mongod instance is running. Defaults to <c>27017</c>.
        /// </summary>
        [JsonProperty("port")]
        public int Port { get; set; } = 27017;

        /// <summary>
        /// The authentication database to use if an user and password is passed. Defaults to <c>admin</c>.
        /// </summary>
        [JsonProperty("authDatabase")]
        public string AuthDatabase { get; set; } = "admin";

        /// <summary>
        /// Gets a <see cref="MongoAuth"/> instance with the default values.
        /// </summary>
        public static MongoAuth Default { get; } = new MongoAuth();

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        [JsonIgnore]
        public string ConnectionString
        {
            get
            {
                string cs = "mongodb://";
                if (!string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Password))
                {
                    cs += $"{Uri.EscapeDataString(User)}:{Uri.EscapeDataString(Password)}@";
                }
                cs += $"{Host}:{Port}";
                if (!string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Password))
                {
                    cs += $"/{AuthDatabase}";
                }
                return cs;
            }
        }

        public override string ToString() => ConnectionString;
    }
}