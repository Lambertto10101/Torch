﻿using System;

namespace Torch.Mod.Messages
{
    /// <summary>
    /// shim to store incoming message data
    /// </summary>
    internal class IncomingMessage : MessageBase
    {
        public override void ProcessClient()
        {
            throw new Exception();
        }

        public override void ProcessServer()
        {
            throw new Exception();
        }
    }
}
