using System;
using Newtonsoft.Json;

namespace Fergun
{
    /// <summary>
    /// Represents a simple MongoDB auth config.
    /// </summary>
    public class MongoConfig
    {
        /// <summary>
        /// Gets the user.
        /// </summary>
        [JsonProperty]
        public string User { get; private set; }

        /// <summary>
        /// Gets the password.
        /// </summary>
        [JsonProperty]
        public string Password { get; private set; }

        /// <summary>
        /// Gets the host where the MongoDB server is running. The default is <c>localhost</c>.
        /// </summary>
        [JsonProperty]
        public string Host { get; private set; } = "localhost";

        /// <summary>
        /// Gets the port where the MongoDB server is running. The default is <c>27017</c>.
        /// </summary>
        [JsonProperty]
        public int Port { get; private set; } = 27017;

        /// <summary>
        /// Gets the authentication database to use if an user and password is passed. The default is <c>admin</c>.
        /// </summary>
        [JsonProperty]
        public string AuthDatabase { get; private set; } = "admin";

        /// <summary>
        /// Gets whether the hostname corresponds to a DNS SRV record (+srv).
        /// </summary>
        [JsonProperty]
        public bool IsSrv { get; private set; } = false;

        /// <summary>
        /// Gets a <see cref="MongoConfig"/> instance with the default values.
        /// </summary>
        public static MongoConfig Default => new MongoConfig();

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        [JsonIgnore]
        public string ConnectionString
        {
            get
            {
                string cs = $"mongodb{(IsSrv ? "+srv" : "")}://";
                if (!string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Password))
                {
                    cs += $"{Uri.EscapeDataString(User)}:{Uri.EscapeDataString(Password)}@";
                }
                cs += Host;
                if (!IsSrv)
                {
                    cs += $":{Port}";
                }
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