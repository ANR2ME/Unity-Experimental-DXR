using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    interface IPropertyKeyDataSource
    {
        float[] GetKeys(); // Get the keys
        Dictionary<float, string> GetDescriptions(); // Caches for descriptions
    }

    class InfiniteTrackDrawer : TrackDrawer
    {
        readonly IPropertyKeyDataSource m_DataSource;
        Rect m_TrackRect;

        public InfiniteTrackDrawer(IPropertyKeyDataSource dataSource)
        {
            m_DataSource = dataSource;
        }

        public bool CanDraw(TrackAsset track, WindowState state)
        {
            var keys = m_DataSource.GetKeys();
            var isTrackEmpty = track.clips.Length == 0;

            return (keys != null || (state.IsArmedForRecord(track) && isTrackEmpty));
        }

        static void DrawRecordBackground(Rect trackRect)
        {
            var styles = DirectorStyles.Instance;

            EditorGUI.DrawRect(trackRect, styles.customSkin.colorInfiniteTrackBackgroundRecording);

            Graphics.ShadowLabel(trackRect,
                DirectorStyles.Elipsify(DirectorStyles.recordingLabel.text, trackRect, styles.fontClip),
                styles.fontClip, Color.white, Color.black);
        }

        public override bool DrawTrack(Rect trackRect, TrackAsset trackAsset, Vector2 visibleTime, WindowState state)
        {
            m_TrackRect = trackRect;

            var theState = (WindowState)state;

            if (!CanDraw(trackAsset, theState))
                return true;

            if (theState.recording && theState.IsArmedForRecord(trackAsset))
                DrawRecordBackground(trackRect);

            GUI.Box(trackRect, GUIContent.none, DirectorStyles.Instance.infiniteTrack);

            var shadowRect = trackRect;
            shadowRect.yMin = shadowRect.yMax;
            shadowRect.height = 15.0f;
            GUI.DrawTexture(shadowRect, DirectorStyles.Instance.bottomShadow.normal.background, ScaleMode.StretchToFill);

            var keys = m_DataSource.GetKeys();
            if (keys != null && keys.Length > 0)
            {
                foreach (var k in keys)
                    DrawKeyFrame(k, theState);
            }

            return true;
        }

        void DrawKeyFrame(float key, WindowState state)
        {
            var x = state.TimeToPixel(key);
            var bounds = new Rect(x, m_TrackRect.yMin + 3.0f, 1.0f, m_TrackRect.height - 6.0f);

            if (!m_TrackRect.Overlaps(bounds))
                return;

            var iconWidth = DirectorStyles.Instance.keyframe.fixedWidth;
            var iconHeight = DirectorStyles.Instance.keyframe.fixedHeight;

            var keyframeRect = bounds;
            keyframeRect.width = iconWidth;
            keyframeRect.height = iconHeight;
            keyframeRect.xMin -= iconWidth / 2.0f;
            keyframeRect.yMin = m_TrackRect.yMin + ((m_TrackRect.height - iconHeight) / 2.0f);

            // case 890650 : Make sure to use GUI.Label and not GUI.Box since the number of key frames can vary while dragging keys in the inline curves causing hotControls to be desynchronized
            GUI.Label(keyframeRect, GUIContent.none, DirectorStyles.Instance.keyframe);

            EditorGUI.DrawRect(bounds, DirectorStyles.Instance.customSkin.colorInfiniteClipLine);
        }
    }
}
