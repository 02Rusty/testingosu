// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Toolkit.HighPerformance;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Skinning;

namespace osu.Game.Screens.Backgrounds
{
    public partial class BackgroundScreenDefault : BackgroundScreen
    {
        private Background background;

        private int currentDisplay;
        private const int background_count = 8;
        private IBindable<APIUser> user;
        private Bindable<bool> showTriangles;
        private Track currentTrack;

        private Bindable<Skin> skin;
        private Bindable<BackgroundSource> source;
        private Bindable<IntroSequence> introSequence;
        private readonly SeasonalBackgroundLoader seasonalBackgroundLoader = new SeasonalBackgroundLoader();

        protected TrianglesV2? Triangles;

        private TrianglesV2 triangles;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; }

        [Resolved]
        private GameHost gameHost { get; set; }

        protected virtual bool AllowStoryboardBackground => true;

        public Background Children { get; set; }

        [BackgroundDependencyLoader]
        private void load(IBindable<WorkingBeatmap> beatmap, IAPIProvider api, SkinManager skinManager, OsuConfigManager config)
        {
            currentTrack = beatmap?.Value?.Track;

            if (currentTrack == null)

                Logger.Log("Warning: No track found in current beatmap!", LoggingTarget.Runtime, LogLevel.Error);

            // LEAVE OTHER DEPENDECIES ALONE!

            user = api.LocalUser.GetBoundCopy();
            skin = skinManager.CurrentSkin.GetBoundCopy();
            source = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource);
            introSequence = config.GetBindable<IntroSequence>(OsuSetting.IntroSequence);

            AddInternal(seasonalBackgroundLoader);

            // Ensure showTriangles is assigned before using it
            showTriangles = config.GetBindable<bool>(OsuSetting.ShowTriangles);

            // Ensure the Bindable is not null before calling BindValueChanged
            if (showTriangles == null)
            {
                return;
            }
            showTriangles.BindValueChanged(enabled =>
            {
                if (Triangles != null)
                    Triangles.Alpha = enabled.NewValue ? 1 : 0; // Show or hide triangles
            }, true);
        }


        protected override void LoadComplete()
        {
            base.LoadComplete();


    // Initialize triangles only if it hasn't been done yet
    if (triangles == null)
    {
        triangles = new TrianglesV2
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            ScaleAdjust = 1.7f,
            SpawnRatio = 4.2f,
        };

               // Add it to the screen
             AddInternal(triangles);

            user.ValueChanged += _ => Scheduler.AddOnce(next);
            skin.ValueChanged += _ => Scheduler.AddOnce(next);
            source.ValueChanged += _ => Scheduler.AddOnce(next);
            beatmap.ValueChanged += _ => Scheduler.AddOnce(next);
            introSequence.ValueChanged += _ => Scheduler.AddOnce(next);
            seasonalBackgroundLoader.SeasonalBackgroundChanged += () => Scheduler.AddOnce(next);
            currentDisplay = RNG.Next(0, background_count);
            Next();

            // helper function required for AddOnce usage.
            void next() => Next();
        }
        }

protected override void Update()
{
    base.Update();

    if (Triangles == null || currentTrack == null || !currentTrack.IsRunning || !showTriangles.Value)
        return;

    // Get the beat amplitude
    float amplitude = currentTrack.CurrentAmplitudes.Average;

    // If amplitude is too small, skip updates
    if (amplitude < 0.01f)
        return;

    // Scale the triangles based on the beat
    Triangles.Scale = new osuTK.Vector2(amplitude * 4);

    // Move triangles upwards dynamically
    Triangles.Position = new osuTK.Vector2(amplitude * 150);

    // Change transparency dynamically
    Triangles.Alpha = 0.5f + amplitude * 0.5f;
}
        private ScheduledDelegate storyboardUnloadDelegate;

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            var backgroundScreenStack = Parent as BackgroundScreenStack;
            Debug.Assert(backgroundScreenStack != null);

            if (background is BeatmapBackgroundWithStoryboard storyboardBackground)
                storyboardUnloadDelegate = gameHost.UpdateThread.Scheduler.AddDelayed(storyboardBackground.UnloadStoryboard, TRANSITION_LENGTH);

            base.OnSuspending(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            if (background is BeatmapBackgroundWithStoryboard storyboardBackground)
            {
                if (storyboardUnloadDelegate?.Completed == false)
                    storyboardUnloadDelegate.Cancel();
                else
                    storyboardBackground.LoadStoryboard();

                storyboardUnloadDelegate = null;
            }

            base.OnResuming(e);
        }

        private ScheduledDelegate nextTask;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Request loading the next background.
        /// </summary>
        /// <returns>Whether a new background was queued for load. May return false if the current background is still valid.</returns>
        public virtual bool Next()
        {
            var nextBackground = createBackground();

            // in the case that the background hasn't changed, we want to avoid cancelling any tasks that could still be loading.
            if (nextBackground == background)
                return false;

            Logger.Log(@"🌅 Global background change queued");

            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            nextTask?.Cancel();
            nextTask = Scheduler.AddDelayed(() =>
            {
                Logger.Log(@"🌅 Global background loading");
                LoadComponentAsync(nextBackground, displayNext, cancellationTokenSource.Token);
            }, 500);

            return true;
        }

        private void displayNext(Background newBackground)
        {
            background?.FadeOut(800, Easing.OutQuint);
            background?.Expire();

            AddInternal(background = newBackground);
            currentDisplay++;

        }


        private Background createBackground()
        {
            // seasonal background loading gets highest priority.
            Background newBackground = seasonalBackgroundLoader.LoadNextBackground();

            if (newBackground == null)
            {

                switch (source.Value)
                {
                    case BackgroundSource.Beatmap:
                    case BackgroundSource.BeatmapWithStoryboard:
                    {
                        if (source.Value == BackgroundSource.BeatmapWithStoryboard && AllowStoryboardBackground)
                            newBackground = new BeatmapBackgroundWithStoryboard(beatmap.Value, getBackgroundTextureName());
                        newBackground ??= new BeatmapBackground(beatmap.Value, getBackgroundTextureName());

                        break;
                    }

                    case BackgroundSource.Skin:
                        switch (skin.Value)
                        {
                            case TrianglesSkin:
                            case ArgonSkin:
                            case DefaultLegacySkin:
                                // default skins should use the default background rotation, which won't be the case if a SkinBackground is created for them.
                                break;

                            default:
                                newBackground = new SkinBackground(skin.Value, getBackgroundTextureName());
                                break;
                        }

                        break;
                }
            }

            // this method is called in many cases where the background might not necessarily need to change.
            // if an equivalent background is currently being shown, we don't want to load it again.
            if (newBackground?.Equals(background) == true)
                return background;

            newBackground ??= new Background(getBackgroundTextureName());
            newBackground.Depth = currentDisplay;

            return newBackground;

        }

        private string getBackgroundTextureName()
        {
            switch (introSequence.Value)
            {
                case IntroSequence.Welcome:
                    return @"Intro/Welcome/menu-background";

                default:
                    return $@"Menu/menu-background-{currentDisplay % background_count + 1}";
            }
        }
    }
}
