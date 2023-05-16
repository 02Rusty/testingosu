// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Online.Rooms;
using osu.Game.Online.Rooms.RoomStatuses;

namespace osu.Game.Screens.OnlinePlay.Lounge.Components
{
    /// <summary>
    /// A pill that displays the room's current status.
    /// </summary>
    public partial class RoomStatusPill : OnlinePlayPill
    {
        [Resolved]
        private OsuColour colours { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EndDate.BindValueChanged(_ => updateDisplay());
            Status.BindValueChanged(_ => updateDisplay(), true);

            FinishTransforms(true);

            TextFlow.Colour = Colour4.Black;
        }

        private void updateDisplay()
        {
            RoomStatus status = getDisplayStatus();

            Pill.Background.Alpha = 1;
            Pill.Background.FadeColour(status.GetAppropriateColour(colours), 100);

            TextFlow.Clear();
            TextFlow.AddText(status.Message);
        }

        private RoomStatus getDisplayStatus()
        {
            if (EndDate.Value < DateTimeOffset.Now)
                return new RoomStatusEnded();

            return Status.Value;
        }
    }
}
