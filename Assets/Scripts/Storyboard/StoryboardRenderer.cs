using System;
using System.Collections.Generic;
using System.Linq;
using Cytoid.Storyboard.Controllers;
using Cytoid.Storyboard.Sprites;
using Cytoid.Storyboard.Texts;
using Cytoid.Storyboard.Videos;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using LineRenderer = Cytoid.Storyboard.Sprites.LineRenderer;
using SpriteRenderer = Cytoid.Storyboard.Sprites.SpriteRenderer;

namespace Cytoid.Storyboard
{
    public class StoryboardRenderer
    {
        public const int ReferenceWidth = 800;
        public const int ReferenceHeight = 600;

        public Storyboard Storyboard { get; }
        public Game Game => Storyboard.Game;
        public Camera Camera { get; private set; }
        public float Time => Game.Time;
        public StoryboardRendererProvider Provider => StoryboardRendererProvider.Instance;
        
        public readonly Dictionary<string, StoryboardComponentRenderer> ComponentRenderers = 
            new Dictionary<string, StoryboardComponentRenderer>(); // Object ID to renderer instance

        public readonly Dictionary<Type, List<StoryboardComponentRenderer>> TypedComponentRenderers =
            new Dictionary<Type, List<StoryboardComponentRenderer>>();
        
        public readonly Dictionary<string, int> SpritePathRefCount = new Dictionary<string, int>();
        
        public StoryboardConstants Constants { get; } = new StoryboardConstants();
        
        public class StoryboardConstants
        {
            public float CanvasToWorldXMultiplier;
            public float CanvasToWorldYMultiplier;
            public float WorldToCanvasXMultiplier;
            public float WorldToCanvasYMultiplier;
        }

        public StoryboardRenderer(Storyboard storyboard)
        {
            Storyboard = storyboard;
        }

        public void Clear()
        {
            ComponentRenderers.Values.ForEach(it => it.Clear());

            ResetCamera();
            ResetCameraFilters();
            
            var canvas = Provider.CanvasRect;
            Constants.CanvasToWorldXMultiplier = 1.0f / canvas.width * Camera.pixelWidth;
            Constants.CanvasToWorldYMultiplier = 1.0f / canvas.height * Camera.pixelHeight;
            Constants.WorldToCanvasXMultiplier = 1.0f / Camera.pixelWidth * canvas.width;
            Constants.WorldToCanvasYMultiplier = 1.0f / Camera.pixelHeight * canvas.height;
        }

        private void ResetCamera()
        {
            Camera = Provider.Camera;
            var cameraTransform = Camera.transform;
            cameraTransform.position = new Vector3(0, 0, -10);
            cameraTransform.eulerAngles = Vector3.zero;
            Camera.orthographic = true;
            Camera.fieldOfView = 53.2f;
        }

        private void ResetCameraFilters()
        {
            Provider.RadialBlur.Apply(it =>
            {
                it.enabled = false;
                it.Intensity = 0.025f;
            });
            Provider.ColorAdjustment.Apply(it =>
            {
                it.enabled = false;
                it.Brightness = 1;
                it.Saturation = 1;
                it.Contrast = 1;
            });
            Provider.GrayScale.Apply(it =>
            {
                it.enabled = false;
                it._Fade = 1;
            });
            Provider.Noise.Apply(it =>
            {
                it.enabled = false;
                it.Noise = 0.2f;
            });
            Provider.ColorFilter.Apply(it =>
            {
                it.enabled = false;
                it.ColorRGB = UnityEngine.Color.white;
            });
            Provider.Sepia.Apply(it =>
            {
                it.enabled = false;
                it._Fade = 1;
            });
            Provider.Dream.Apply(it =>
            {
                it.enabled = false;
                it.Distortion = 1;
            });
            Provider.Fisheye.Apply(it =>
            {
                it.enabled = false;
                it.Distortion = 0.5f;
            });
            Provider.Shockwave.Apply(it =>
            {
                it.enabled = false;
                it.TimeX = 1.0f;
                it.Speed = 1;
            });
            Provider.Focus.Apply(it =>
            {
                it.enabled = false;
                it.Size = 1;
                it.Color = UnityEngine.Color.white;
                it.Speed = 5;
                it.Intensity = 0.25f;
            });
            Provider.Glitch.Apply(it =>
            {
                it.enabled = false;
                it.Glitch = 1f;
            });
            Provider.Artifact.Apply(it =>
            {
                it.enabled = false;
                it.Fade = 1;
                it.Colorisation = 1;
                it.Parasite = 1;
                it.Noise = 1;
            });
            Provider.Arcade.Apply(it =>
            {
                it.enabled = false;
                it.Interferance_Size = 1;
                it.Interferance_Speed = 0.5f;
                it.Contrast = 1;
                it.Fade = 1;
            });
            Provider.Chromatical.Apply(it =>
            {
                it.enabled = false;
                it.Fade = 1;
                it.Intensity = 1;
                it.Speed = 1;
            });
            Provider.Tape.Apply(it =>
            {
                it.enabled = false;
            });
            Provider.SleekRender.Apply(it =>
            {
                it.enabled = false;
                it.settings.bloomEnabled = false;
                it.settings.bloomIntensity = 0;
            });
        }

        public void Dispose()
        {
            ComponentRenderers.Values.ForEach(it => it.Dispose());
            ComponentRenderers.Clear();
            TypedComponentRenderers.Clear();
            SpritePathRefCount.Clear();
            Context.AssetMemory.DisposeTaggedCacheAssets(AssetTag.Storyboard);
            Clear();
        }

        public async UniTask Initialize()
        {
            // Clear
            Clear();

            foreach (var type in new[]
                {typeof(Text), typeof(Sprite), typeof(Line), typeof(Video), typeof(Controller), typeof(NoteController)})
            {
                TypedComponentRenderers[type] = new List<StoryboardComponentRenderer>();
            }
            
            var timer = new BenchmarkTimer("StoryboardRenderer initialization");
            bool Predicate<TO>(TO obj) where TO : Object => !obj.IsManuallySpawned(); await SpawnObjects<NoteController, NoteControllerState, NoteControllerRenderer>(Storyboard.NoteControllers.Values.ToList(), noteController => new NoteControllerRenderer(this, noteController), Predicate);
            timer.Time("NoteController"); // Spawn note placeholder transforms
            await SpawnObjects<Text, TextState, TextRenderer>(Storyboard.Texts.Values.ToList(), text => new TextRenderer(this, text), Predicate);
            timer.Time("Text");
            await SpawnObjects<Sprite, SpriteState, SpriteRenderer>(Storyboard.Sprites.Values.ToList(), sprite => new SpriteRenderer(this, sprite), Predicate);
            timer.Time("Sprite");
            await SpawnObjects<Line, LineState, LineRenderer>(Storyboard.Lines.Values.ToList(), line => new LineRenderer(this, line), Predicate);
            timer.Time("Line");
            await SpawnObjects<Video, VideoState, VideoRenderer>(Storyboard.Videos.Values.ToList(), line => new VideoRenderer(this, line), Predicate);
            timer.Time("Video");
            await SpawnObjects<Controller, ControllerState, ControllerRenderer>(Storyboard.Controllers.Values.ToList(), controller => new ControllerRenderer(this, controller), Predicate);
            timer.Time("Controller");
            timer.Time();

            // Clear on abort/retry/complete
            Game.onGameDisposed.AddListener(_ =>
            {
                Dispose();
            });
            Game.onGamePaused.AddListener(_ =>
            {
                // TODO: Pause SB
            });
            Game.onGameWillUnpause.AddListener(_ =>
            {
                // TODO: Unpause SB
            });
        }

        private async UniTask<List<TR>> SpawnObjects<TO, TS, TR>(List<TO> objects, Func<TO, TR> rendererCreator, Predicate<TO> predicate = default, Func<TO, TO> transformer = default) 
            where TS : ObjectState
            where TO : Object<TS>
            where TR : StoryboardComponentRenderer<TO, TS>
        {
            if (predicate == default) predicate = _ => true;
            if (transformer == default) transformer = _ => _;
            var renderers = new List<TR>();
            var tasks = new List<UniTask>();
            foreach (var obj in objects)
            {
                if (!predicate(obj)) continue;
                var transformedObj = transformer(obj);
                
                var renderer = rendererCreator(transformedObj);
                if (ComponentRenderers.ContainsKey(transformedObj.Id))
                {
                    Debug.LogWarning($"Storyboard: Object {transformedObj.Id} is already spawned");
                    continue;
                }
                ComponentRenderers[transformedObj.Id] = renderer;
                TypedComponentRenderers[typeof(TO)].Add(renderer);
                // Debug.Log($"StoryboardRenderer: Spawned {typeof(TO).Name} with ID {obj.Id}");
                
                // Resolve parent
                StoryboardComponentRenderer parent = null;
                if (transformedObj.ParentId != null)
                {
                    if (!ComponentRenderers.ContainsKey(transformedObj.ParentId))
                    {
                        throw new InvalidOperationException($"Storyboard: parent_id \"{transformedObj.ParentId}\" does not exist");
                    }
                    parent = ComponentRenderers[transformedObj.ParentId];
                }
                else if (transformedObj.TargetId != null)
                {
                    if (!ComponentRenderers.ContainsKey(transformedObj.TargetId))
                    {
                        throw new InvalidOperationException($"Storyboard: target_id \"{transformedObj.TargetId}\" does not exist");
                    }
                    parent = ComponentRenderers[transformedObj.TargetId] as TR ?? throw new InvalidOperationException($"Storyboard: target_id \"{transformedObj.TargetId} does not have type {typeof(TR).Name}");
                }
                if (parent != null)
                {
                    parent.Children.Add(renderer);
                    renderer.Parent = parent;
                }
                
                tasks.Add(renderer.Initialize());
            }

            await UniTask.WhenAll(tasks);
            return renderers;
        }

        public void OnGameUpdate(Game _)
        {
            var time = Time;
            if (time < 0 || Game.State.IsReadyToExit) return;

            var updateOrder = new[]
                {typeof(NoteController), typeof(Text), typeof(Sprite), typeof(Line), typeof(Video), typeof(Controller)};

            var renderersToDestroy = new Dictionary<string, Type>();

            foreach (var type in updateOrder)
            {
                var renderers = TypedComponentRenderers[type];
                foreach (var renderer in renderers)
                {
                    renderer.Component.FindStates(time, out var fromState, out var toState);

                    if (fromState == null) continue;

                    // Destroy?
                    if (fromState.Destroy != null && fromState.Destroy.Value)
                    {
                        // Destroy the target as well
                        if (renderer.Parent != null && renderer.Component.TargetId != null)
                        {
                            renderersToDestroy[renderer.Parent.Component.Id] = type;
                            this.ListOf(renderer.Parent).Flatten(it => it.Children).ForEach(it =>
                            {
                                renderersToDestroy[it.Component.Id] = type;
                            });
                        }
                        else
                        {
                            renderer.Parent?.Children.Remove(renderer);
                            this.ListOf(renderer).Flatten(it => it.Children).ForEach(it =>
                            {
                                renderersToDestroy[it.Component.Id] = type;
                            });
                        }
                        continue;
                    }

                    renderer.Update(fromState, toState);
                }
            }

            renderersToDestroy.ForEach(it =>
            {
                var id = it.Key;
                var type = it.Value;
                var renderer = ComponentRenderers[id];
                
                if (Game is PlayerGame)
                {
                    renderer.Clear();
                }
                else
                {
                    renderer.Dispose();
                }
                ComponentRenderers.Remove(id);
                TypedComponentRenderers[type].Remove(renderer);
            });
        }

        public void OnTrigger(Trigger trigger)
        {
            // Spawn objects
            if (trigger.Spawn != null)
            {
                foreach (var id in trigger.Spawn)
                {
                    SpawnObjectById(id);
                }
            }

            // Destroy objects
            if (trigger.Destroy != null)
            {
                foreach (var id in trigger.Destroy)
                {
                    DestroyObjectsById(id);
                }
            }
        }

        public async void SpawnObjectById(string id)
        {
            bool Predicate<TO>(TO obj) where TO : Object => obj.Id == id;
            TO Transformer<TO, TS>(TO obj) where TO : Object<TS> where TS : ObjectState
            {
                var res = obj.JsonDeepCopy();
                RecalculateTime<TO, TS>(res);
                return res;
            }
            if (Storyboard.Texts.ContainsKey(id)) await SpawnObjects<Text, TextState, TextRenderer>(new List<Text> {Storyboard.Texts[id]}, text => new TextRenderer(this, text), Predicate, Transformer<Text, TextState>);
            if (Storyboard.Sprites.ContainsKey(id)) await SpawnObjects<Sprite, SpriteState, SpriteRenderer>(new List<Sprite> {Storyboard.Sprites[id]}, sprite => new SpriteRenderer(this, sprite), Predicate, Transformer<Sprite, SpriteState>);
            if (Storyboard.Lines.ContainsKey(id)) await SpawnObjects<Line, LineState, LineRenderer>(new List<Line> {Storyboard.Lines[id]}, line => new LineRenderer(this, line), Predicate, Transformer<Line, LineState>);
            if (Storyboard.Videos.ContainsKey(id)) await SpawnObjects<Video, VideoState, VideoRenderer>(new List<Video> {Storyboard.Videos[id]}, line => new VideoRenderer(this, line), Predicate, Transformer<Video, VideoState>);
            if (Storyboard.Controllers.ContainsKey(id)) await SpawnObjects<Controller, ControllerState, ControllerRenderer>(new List<Controller> {Storyboard.Controllers[id]}, controller => new ControllerRenderer(this, controller), Predicate, Transformer<Controller, ControllerState>);
            if (Storyboard.NoteControllers.ContainsKey(id)) await SpawnObjects<NoteController, NoteControllerState, NoteControllerRenderer>(new List<NoteController> {Storyboard.NoteControllers[id]}, noteController => new NoteControllerRenderer(this, noteController), Predicate, Transformer<NoteController, NoteControllerState>);
        }

        public void DestroyObjectsById(string id)
        {
            if (!ComponentRenderers.ContainsKey(id)) return;
            ComponentRenderers[id].Let(it =>
            {
                if (Game is PlayerGame) it.Clear();
                else it.Dispose();
                TypedComponentRenderers[it.GetType()].Remove(it);
            });
            ComponentRenderers.Remove(id);
        }

        public void RecalculateTime<TO, TS>(TO obj) where TO : Object<TS> where TS : ObjectState
        {
            var baseTime = Time;
            var states = obj.States;

            if (obj.IsManuallySpawned())
            {
                states[0].Time = baseTime;
            }
            else
            {
                baseTime = states[0].Time;
            }

            var lastTime = baseTime;
            foreach (var state in states)
            {
                if (state.RelativeTime != null)
                {
                    state.Time = baseTime + state.RelativeTime.Value;
                }

                if (state.AddTime != null)
                {
                    state.Time = lastTime + state.AddTime.Value;
                }

                lastTime = state.Time;
            }
        }

    }
}