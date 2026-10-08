namespace QJellyTower.Core
{
    /// <summary>
    /// 弹动期间循环播放的 BGM。只在「开关打开 + 身处战斗房间」时出声，
    /// 其余情况立刻停掉，避免在地图/商店里一直响。
    /// </summary>
    public static class JellyMusic
    {
        public const string StreamPath = "res://Q弹尖塔/audio/jelly_music.ogg";

        private const float SilentDb = -80f;

        private static AudioStreamPlayer _player;
        private static bool _unavailable;

        public static void Tick(bool shouldPlay, float volume)
        {
            EnsurePlayer();
            if (_player == null || !GodotObject.IsInstanceValid(_player))
            {
                return;
            }

            _player.VolumeDb = volume <= 0.001f ? SilentDb : Mathf.LinearToDb(volume);

            if (shouldPlay)
            {
                if (!_player.Playing)
                {
                    _player.Play();
                }
            }
            else if (_player.Playing)
            {
                _player.Stop();
            }
        }

        private static void EnsurePlayer()
        {
            if (_unavailable)
            {
                return;
            }

            if (_player != null && GodotObject.IsInstanceValid(_player))
            {
                return;
            }

            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            {
                return;
            }

            AudioStream stream = GD.Load<AudioStream>(StreamPath);
            if (stream == null)
            {
                _unavailable = true;
                Log.Warn("[Q弹尖塔] music not found at " + StreamPath + ", jelly will run silently.", 2);
                return;
            }

            if (stream is AudioStreamOggVorbis ogg)
            {
                ogg.Loop = true;
            }

            _player = new AudioStreamPlayer
            {
                Name = "QJellyMusic",
                Stream = stream,
            };
            tree.Root.AddChild(_player);

            Log.Info("[Q弹尖塔] jelly music loaded from " + StreamPath, 2);
        }
    }
}
