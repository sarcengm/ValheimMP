using UnityEngine;
using ValheimMP.Framework.Extensions;

namespace ValheimMP.Framework.Events
{
    public class OnChatMessageArgs
    {
        /// <summary>
        /// Originating peer
        /// </summary>
        public ZNetPeer Peer { get; internal set; }

        /// <summary>
        /// Originating player
        /// </summary>
        public Player Player { get; internal set; }

        /// <summary>
        /// Name of the player displayed in the chat message
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// Location the messag is displayed
        /// </summary>
        public Vector3 MessageLocation { get; set; }

        /// <summary>
        /// Maximum distance players can be from the message location in order to receive it
        /// </summary>
        public float MessageDistance { get; set; }

        /// <summary>
        /// Message text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Message type
        /// </summary>
        public ChatMessageType MessageType { get; set; }

        /// <summary>
        /// Set true if you want to suppress the message from going out.
        /// </summary>
        public bool SuppressMessage { get; set; }
    }
}
