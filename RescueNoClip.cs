using System;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace RescueNoClip
{
    [BepInPlugin("local.spt.rescuenoclip", "Rescue NoClip", "1.0.1")]
    [BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        private ConfigEntry<KeyboardShortcut> toggle;
        private ConfigEntry<float> speed;
        private Harmony harmony;
        private Player player;
        private SimpleCharacterController controller;
        private bool active, oldEnabled, oldMoveIgnored;
        private Vector3 position;

        private void Awake()
        {
            Instance = this;
            toggle = Config.Bind("Controls", "Toggle", new KeyboardShortcut(KeyCode.F8), "NoClip ein/aus (nur im Raid).");
            speed = Config.Bind("Flight", "Speed", 6f, new ConfigDescription("Fluggeschwindigkeit in Metern pro Sekunde.", new AcceptableValueRange<float>(0.5f, 30f)));
            harmony = new Harmony("local.spt.rescuenoclip");
            harmony.Patch(AccessTools.Method(typeof(MovementContext), "ApplyGravity", new[] { typeof(Vector3).MakeByRefType(), typeof(float), typeof(bool) }), prefix: new HarmonyMethod(typeof(Plugin), nameof(BeforeGravity)));
            Logger.LogInfo("Rescue NoClip loaded. F8 by default; WASD, Space/Ctrl, Shift.");
        }

        private static bool BeforeGravity(MovementContext __instance)
        {
            var p = Instance;
            return p == null || !p.active || p.player == null || !ReferenceEquals(__instance, p.player.MovementContext);
        }

        private void Update()
        {
            try
            {
                var world = Singleton<GameWorld>.Instance;
                var current = world == null ? null : world.MainPlayer;
                if (active && (current == null || current != player || !player.gameObject.activeInHierarchy || !player.HealthController.IsAlive)) Stop();
                if (!toggle.Value.IsDown()) return;
                if (active) { Stop(); return; }
                if (current == null || !current.IsYourPlayer || !current.gameObject.activeInHierarchy || !current.HealthController.IsAlive) return;
                var candidate = current.CharacterController as SimpleCharacterController;
                if (candidate == null)
                {
                    Logger.LogWarning("NoClip activation cancelled: unsupported controller " + current.CharacterController?.GetType().FullName);
                    return;
                }
                // Read all snapshots before assigning session state or mutating anything.
                var startPosition = current.Transform.position;
                var enabledSnapshot = candidate.isEnabled;
                var ignoredSnapshot = candidate.IsMoveIgnored;
                player = current;
                controller = candidate;
                oldEnabled = enabledSnapshot;
                oldMoveIgnored = ignoredSnapshot;
                position = startPosition;
                // Mark active before mutations so partial activation is rolled back.
                active = true;
                controller.IsMoveIgnored = true;
                controller.isEnabled = false;
                Logger.LogInfo("NoClip ON 1.0.1; controller=" + controller.GetType().Name + "; position=" + position);
            }
            catch (Exception e) { Logger.LogError(e); Stop(); }
        }

        private void LateUpdate()
        {
            if (!active || player == null) return;
            try
            {
                Vector3 direction = Vector3.zero;
                if (!Cursor.visible && Application.isFocused)
                {
                    var camera = Camera.main;
                    var forward = camera == null ? player.Transform.rotation * Vector3.forward : camera.transform.forward;
                    var right = camera == null ? player.Transform.rotation * Vector3.right : camera.transform.right;
                    if (Input.GetKey(KeyCode.W)) direction += forward;
                    if (Input.GetKey(KeyCode.S)) direction -= forward;
                    if (Input.GetKey(KeyCode.D)) direction += right;
                    if (Input.GetKey(KeyCode.A)) direction -= right;
                    if (Input.GetKey(KeyCode.Space)) direction += Vector3.up;
                    if (Input.GetKey(KeyCode.LeftControl)) direction -= Vector3.up;
                }
                float boost = Input.GetKey(KeyCode.LeftShift) ? 3f : 1f;
                position += Vector3.ClampMagnitude(direction, 1f) * speed.Value * boost * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                player.Teleport(position, false);
                player.MovementContext.FreefallTime = 0f;
            }
            catch (Exception e) { Logger.LogError(e); Stop(); }
        }

        private void Stop()
        {
            if (!active) return;
            active = false;
            bool restored = true;
            try
            {
                if (player != null) { player.Teleport(position, false); player.MovementContext.FreefallTime = 0f; }
            }
            catch (Exception e) { restored = false; Logger.LogWarning(e); }
            finally
            {
                if (controller != null)
                {
                    try { controller.IsMoveIgnored = oldMoveIgnored; }
                    catch (Exception e) { restored = false; Logger.LogWarning(e); }
                    try { controller.isEnabled = oldEnabled; }
                    catch (Exception e) { restored = false; Logger.LogWarning(e); }
                }
                controller = null;
                player = null;
            }
            if (restored) Logger.LogInfo("NoClip OFF; controller state restored.");
            else Logger.LogError("NoClip OFF; restoration encountered an error. See preceding exception.");
        }

        private void OnGUI()
        {
            if (active) GUI.Box(new Rect(20, 20, 490, 55), "NOCLIP AKTIV — " + toggle.Value + " zum Ausschalten\nWASD | Leertaste hoch | Strg runter | Shift schneller");
        }

        private void OnDisable() { if (active) Stop(); }
        private void OnDestroy()
        {
            if (active) Stop();
            harmony?.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }
}
