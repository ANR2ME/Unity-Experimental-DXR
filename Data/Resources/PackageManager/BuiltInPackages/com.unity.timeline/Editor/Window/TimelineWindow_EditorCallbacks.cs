using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    partial class TimelineWindow
    {
        void OnSelectionChangedInactive()
        {
            // Case 946942 -- when selection changes and the window is open but hidden, timeline needs to update selection immediately
            // so preview mode is correctly released
            if (!hasFocus)
            {
                RefreshSelection(!locked);
            }
        }

        void InitializeEditorCallbacks()
        {
            Undo.postprocessModifications += PostprocessAnimationRecordingModifications;
            Undo.postprocessModifications += ProcessAssetModifications;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AnimationUtility.onCurveWasModified += OnCurveModified;
            EditorApplication.editorApplicationQuit += OnEditorQuit;
            Selection.selectionChanged += OnSelectionChangedInactive;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
        }

        void OnEditorQuit()
        {
            TimelineWindowViewPrefs.SaveAll();
        }

        void RemoveEditorCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.postprocessModifications -= PostprocessAnimationRecordingModifications;
            Undo.postprocessModifications -= ProcessAssetModifications;
            AnimationUtility.onCurveWasModified -= OnCurveModified;
            EditorApplication.editorApplicationQuit -= OnEditorQuit;
            Selection.selectionChanged -= OnSelectionChangedInactive;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            ObjectFactory.componentWasAdded -= OnComponentWasAdded;
        }

        // When the scene is save the director time will get reset.
        void OnSceneSaved(Scene scene)
        {
            if (state != null)
                state.OnSceneSaved();
        }

        void OnCurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType type)
        {
            InspectorWindow.RepaintAllInspectors();
            if (state == null)
                return;

            if (type == AnimationUtility.CurveModifiedType.CurveModified)
            {
                Playable playable;
                if (m_PlayableLookup.GetPlayableFromAnimClip(clip, out playable))
                {
                    playable.SetAnimatedProperties(clip);
                }

                // updates the duration of the graph without rebuilding
                AnimationUtility.SyncEditorCurves(clip); // deleted keys are not synced when this is sent out, so duration could be incorrect
                state.UpdateRootPlayableDuration(state.editSequence.duration);

                // don't evaluate if this is caused by recording on an animation track, the extra evaluation can cause hiccups
                if (!TimelineRecording.IsRecordingAnimationTrack)
                    state.Evaluate();
            }
            else // curve added/removed, or clip added/removed
            {
                state.rebuildGraph = true;
            }
        }

        void OnPlayModeStateChanged(PlayModeStateChange playModeState)
        {
            // case 923506 - make sure we save view data before switching modes
            if (playModeState == PlayModeStateChange.ExitingEditMode ||
                playModeState == PlayModeStateChange.ExitingPlayMode)
                TimelineWindowViewPrefs.SaveAll();

            bool isPlaymodeAboutToChange = playModeState == PlayModeStateChange.ExitingEditMode || playModeState == PlayModeStateChange.ExitingPlayMode;

            // Important to stop the graph on any director so temporary objects are properly cleaned up
            if (isPlaymodeAboutToChange && state != null)
                state.Stop();
        }

        UndoPropertyModification[] PostprocessAnimationRecordingModifications(UndoPropertyModification[] modifications)
        {
            if (!state.recording)
                return modifications;

            var remaining = TimelineRecording.ProcessUndoModification(modifications, state);
            // if we've changed, we need to repaint the sequence window to show clip length changes
            if (remaining != modifications)
            {
                // only update if us or the sequencer window has focus
                // Prevents color pickers and other dialogs from being wrongly dismissed
                bool repaint = (focusedWindow == null) ||
                    (focusedWindow is InspectorWindow) ||
                    (focusedWindow is TimelineWindow);

                if (repaint)
                    Repaint();
            }


            return remaining;
        }

        UndoPropertyModification[] ProcessAssetModifications(UndoPropertyModification[] modifications)
        {
            bool rebuildGraph = false;

            for (int i = 0; i < modifications.Length && !rebuildGraph; i++)
            {
                var mod = modifications[i];

                // check if an Avatar Mask has been modified
                if (mod.previousValue != null && mod.previousValue.target is AvatarMask)
                {
                    rebuildGraph = state.editSequence.asset != null &&
                        state.editSequence.asset.flattenedTracks
                            .OfType<UnityEngine.Timeline.AnimationTrack>()
                            .Any(x => mod.previousValue.target == x.avatarMask);
                }
            }

            if (rebuildGraph)
            {
                state.rebuildGraph = true;
                Repaint();
            }

            return modifications;
        }

        void OnUndoRedo()
        {
            var undos = new List<string>();
            var redos = new List<string>();
            Undo.GetRecords(undos, redos);

            var rebuildAll = redos.Any(x => x.StartsWith("Timeline ")) || undos.Any(x => x.StartsWith("Timeline"));
            var evalNow = redos.Any(x => x.Contains("Edit Curve")) || undos.Any(x => x.Contains("Edit Curve"));
            if (rebuildAll || evalNow)
            {
                ValidateSelection();
                if (state != null)
                {
                    if (evalNow) // when curves change, the new values need to be set in the transform before the inspector handles the undo
                        state.EvaluateImmediate();
                    if (rebuildAll)
                        state.Refresh();
                }
                Repaint();
            }
        }

        static void ValidateSelection()
        {
            //get all the clips in the selection
            var selectedClips = Selection.GetFiltered<EditorClip>(SelectionMode.Unfiltered).Select(x => x.clip);
            foreach (var selectedClip in selectedClips)
            {
                var parent = selectedClip.parentTrack;
                if (selectedClip.parentTrack != null)
                {
                    if (!parent.clips.Contains(selectedClip))
                    {
                        SelectionManager.Remove(selectedClip);
                    }
                }
            }
        }

        void OnComponentWasAdded(Component c)
        {
            var go = c.gameObject;
            foreach (var seq in state.GetAllSequences())
            {
                if (seq.director == null || seq.asset == null)
                {
                    return;
                }

                var rebind = seq.asset.GetOutputTracks().Any(track => seq.director.GetGenericBinding(track) == go);
                // Either the playable director has a binding for the GameObject or it is a sibling of the director.
                // The second case is needed since we have timeline top level markerTracks that do not have a binding, but
                // are still "targeting" the playable director
                if (rebind || seq.director.gameObject == go)
                {
                    seq.director.RebindPlayableGraphOutputs();
                }
            }
        }
    }
}
