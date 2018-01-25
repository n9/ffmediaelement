﻿namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;

    /// <summary>
    /// Implements the logic to start or resume media playback
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class PlayCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlayCommand" /> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        public PlayCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Play)
        {
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;
            if (m.Media.IsOpen == false) return;
            if (m.Media.HasMediaEnded
                || (m.Media.NaturalDuration.HasValue
                && m.Media.NaturalDuration != TimeSpan.MinValue
                && m.Clock.Position >= m.Media.NaturalDuration.Value))
                return;

            foreach (var renderer in m.Renderers.Values)
                renderer.Play();

            m.Clock.Play();
            m.Media.MediaState = MediaEngineState.Play;
        }
    }
}
