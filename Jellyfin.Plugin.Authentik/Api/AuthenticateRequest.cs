using System;

namespace Jellyfin.Plugin.Authentik.Api
{
    /// <summary>
    /// Request body for completing authentication from the client.
    /// </summary>
    public class AuthenticateRequest
    {
        /// <summary>
        /// Gets or sets the completion state token.
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Gets or sets the device ID.
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        public string? DeviceName { get; set; }

        /// <summary>
        /// Gets or sets the app name.
        /// </summary>
        public string? AppName { get; set; }

        /// <summary>
        /// Gets or sets the app version.
        /// </summary>
        public string? AppVersion { get; set; }
    }
}
