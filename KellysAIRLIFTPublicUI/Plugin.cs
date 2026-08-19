using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NuclearOption.Chat;
using NuclearOption.MissionEditorScripts.Buttons;
using NuclearOption.Networking;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KellysAirliftPublicUI
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "kelly.nuclearoption.airlift.publicui";
        public const string PluginName = "Kelly's AIRLIFT Support Purchase";
        public const string PluginVersion = "0.17.1";

        private static readonly string[] SupportNames = {
            "AA Battery Drop",
            "RAPID Runway Combat Drop",
            "Parachute Combat Drop (ammo-pallet prototype)",
            "Light Tank Drop (2 x FRCV-105 LT)",
            "Mortar Drop (1 x Recon, 3 x Mortar)",
            "AFV6 Drop (2 x AT, 2 x IFV)",
            "AA Gun Container Drop (4 containers)"
        };
        private static readonly string[] SupportTokens = {
            "aa", "rapid", "parachute",
            "lighttank", "mortar", "afv6", "aagun"
        };

        private static readonly FieldInfo ConvoyPrefabField =
            AccessTools.Field(typeof(ContributeToFaction), "convoySelectPrefab");
        private static readonly FieldInfo ConvoyBackgroundField =
            AccessTools.Field(typeof(ContributeToFaction), "convoySelectBackground");
        private static readonly FieldInfo HoverTextField =
            AccessTools.Field(typeof(ContributeToFaction), "hoverText");
        private static readonly FieldInfo LocalPlayerField =
            AccessTools.Field(typeof(ContributeToFaction), "localPlayer");
        private static readonly FieldInfo OptionButtonField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "button");
        private static readonly FieldInfo OptionHoverField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "buttonHoverText");
        private static readonly FieldInfo OptionTextField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "text");
        private static readonly FieldInfo OptionIconField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "icon");
        private static readonly FieldInfo OptionUnavailableField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "unavailable");
        private static readonly FieldInfo OptionLocalPlayerField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "localPlayer");
        private static readonly FieldInfo OptionCostField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "cost");
        private static readonly FieldInfo OptionCanSpawnField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "canSpawn");
        private static readonly FieldInfo OptionWasInteractableField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "wasInteractable");
        private static readonly FieldInfo OptionCompositionField =
            AccessTools.Field(typeof(ConvoyPurchaseOption), "composition");

        private static Plugin _instance;
        private Harmony _harmony;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _aaCost;
        private ConfigEntry<float> _rapidCost;
        private ConfigEntry<float> _parachuteCost;
        private ConfigEntry<float> _lightTankCost;
        private ConfigEntry<float> _mortarCost;
        private ConfigEntry<float> _afv6Cost;
        private ConfigEntry<float> _aaGunContainerCost;
        private Rect _selectorWindow = new Rect(35f, 70f, 500f, 520f);
        private ContributeToFaction _selectorMenu;
        private Player _selectorPlayer;
        private int _supportIndex;
        private bool _selectorVisible;
        private bool _supportDropdownOpen;
        private string _selectorStatus;
        private DynamicMap _map;
        private bool _openedMap;
        private bool _hasMarker;
        private GlobalPosition _markerPosition;
        private GameObject _marker;
        private GameObject _markerVector;
        private MapWaypoint _mapWaypoint;
        private readonly List<SuppressedUiState> _suppressedUi =
            new List<SuppressedUiState>();

        private sealed class SupportPurchaseMarker : MonoBehaviour { }

        private sealed class SuppressedUiState
        {
            internal CanvasGroup Group;
            internal float Alpha;
            internal bool Interactable;
            internal bool BlocksRaycasts;
        }

        private sealed class DonateLayoutState : MonoBehaviour
        {
            internal RectTransform Root;
            internal Vector2 RootSize;
            internal Vector3 RootPosition;
            internal bool Captured;
        }

        private void Awake()
        {
            _instance = this;
            _enabled = Config.Bind("Purchase", "ShowSupportPurchasePrototype", true,
                "Replace the old RAPID row with the local multi-support purchase prototype.");
            _aaCost = Config.Bind("Purchase", "DisplayedAntiAirDropCost", 400f,
                "Displayed AA-drop cost in Nuclear Option's native million-dollar units.");
            if (Mathf.Approximately(_aaCost.Value, 250f))
                _aaCost.Value = 400f;
            _rapidCost = Config.Bind("Purchase", "DisplayedRapidDropCost", 400f,
                "Displayed RAPID cost in Nuclear Option's native million-dollar units.");
            _parachuteCost = Config.Bind("Purchase", "DisplayedParachuteDropCost", 100f,
                "Displayed one-Chimera parachute prototype cost in Nuclear Option's native million-dollar units.");
            _lightTankCost = Config.Bind("Purchase", "DisplayedLightTankDropCost", 300f,
                "Displayed two-FRCV light tank drop cost in Nuclear Option's native million-dollar units.");
            _mortarCost = Config.Bind("Purchase", "DisplayedMortarDropCost", 200f,
                "Displayed Serval reconnaissance/mortar drop cost in Nuclear Option's native million-dollar units.");
            _afv6Cost = Config.Bind("Purchase", "DisplayedAfv6DropCost", 160f,
                "Displayed AFV6 AT/IFV drop cost in Nuclear Option's native million-dollar units.");
            _aaGunContainerCost = Config.Bind("Purchase", "DisplayedAaGunContainerDropCost", 200f,
                "Displayed AA Gun Container drop cost in Nuclear Option's native million-dollar units.");
            if (Application.isBatchMode) return;
            ValidateApi();
            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(AccessTools.Method(typeof(ContributeToFaction),
                    nameof(ContributeToFaction.RefreshVehicleList)),
                postfix: new HarmonyMethod(typeof(Plugin),
                    nameof(RefreshVehicleListPostfix)));
            Logger.LogInfo(PluginName + " " + PluginVersion
                + " loaded. Request Support will appear under Donate > Vehicles.");
        }

        private void OnDestroy()
        {
            CloseSelector();
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_instance == this) _instance = null;
        }

        private static void ValidateApi()
        {
            if (ConvoyPrefabField == null || ConvoyBackgroundField == null
                || HoverTextField == null || LocalPlayerField == null
                || OptionButtonField == null || OptionHoverField == null
                || OptionTextField == null || OptionIconField == null
                || OptionUnavailableField == null || OptionLocalPlayerField == null
                || OptionCostField == null || OptionCanSpawnField == null
                || OptionWasInteractableField == null
                || OptionCompositionField == null)
                throw new MissingFieldException(
                    "Nuclear Option 0.34 Donate/convoy UI fields changed.");
        }

        private static void RefreshVehicleListPostfix(ContributeToFaction __instance)
        {
            Plugin plugin = _instance;
            if (plugin == null || Application.isBatchMode || !plugin._enabled.Value
                || __instance == null)
                return;
            try { plugin.AddPurchaseButton(__instance); }
            catch (Exception exception)
            {
                plugin.Logger.LogWarning(
                    "AIRLIFT support-menu injection failed closed: " + exception);
            }
        }

        private void AddPurchaseButton(ContributeToFaction menu)
        {
            Player player = LocalPlayerField.GetValue(menu) as Player;
            GameObject prefab = ConvoyPrefabField.GetValue(menu) as GameObject;
            Transform background = ConvoyBackgroundField.GetValue(menu) as Transform;
            HoverText hoverText = HoverTextField.GetValue(menu) as HoverText;
            if (player == null || prefab == null || background == null) return;

            SupportPurchaseMarker[] old =
                background.GetComponentsInChildren<SupportPurchaseMarker>(true);
            for (int index = 0; index < old.Length; index++)
                if (old[index] != null) Destroy(old[index].gameObject);

            GameObject clone = Instantiate(prefab, background);
            clone.name = "KellysAIRLIFT_SupportPurchasePrototype";
            clone.AddComponent<SupportPurchaseMarker>();
            ConvoyPurchaseOption option = clone.GetComponent<ConvoyPurchaseOption>();
            if (option == null)
            {
                Destroy(clone);
                return;
            }
            if (hoverText != null) option.SetButtonHoverText(hoverText);
            float minimumCost = MinimumDisplayedCost();
            TMP_Text optionText = OptionTextField.GetValue(option) as TMP_Text;
            if (optionText != null)
                optionText.text = "Request Support (from "
                    + UnitConverter.ValueReading(minimumCost) + ")";
            Image optionIcon = OptionIconField.GetValue(option) as Image;
            if (optionIcon != null)
            {
                Image nativeIcon = background.GetComponentsInChildren<ConvoyPurchaseOption>(true)
                    .Where(candidate => candidate != null && candidate != option)
                    .Select(candidate => OptionIconField.GetValue(candidate) as Image)
                    .FirstOrDefault(candidate => candidate != null
                        && candidate.sprite != null);
                if (nativeIcon != null) optionIcon.sprite = nativeIcon.sprite;
            }
            GameObject unavailable = OptionUnavailableField.GetValue(option) as GameObject;
            if (unavailable != null) unavailable.SetActive(false);
            OptionLocalPlayerField.SetValue(option, player);
            OptionCostField.SetValue(option, minimumCost);
            OptionCanSpawnField.SetValue(option, true);
            OptionWasInteractableField.SetValue(option, true);
            OptionCompositionField.SetValue(option,
                "\nAA battery\nRAPID runway package\nParachute prototype"
                + "\nLight tanks\nMortars\nAFV6\nAA guns");
            ShowHoverText optionHover = OptionHoverField.GetValue(option) as ShowHoverText;
            if (optionHover != null)
                optionHover.SetText("Open the AIRLIFT local support prototype. "
                    + "Choose a support type, place a marker on the native map, "
                    + "and submit a server-authoritative purchase request.");
            Button button = OptionButtonField.GetValue(option) as Button;
            if (button == null)
            {
                Destroy(clone);
                return;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => OpenSupportSelector(menu, player));
            button.interactable = true;
            RepairDonateLayout(menu, background);
        }

        private float MinimumDisplayedCost()
        {
            return Mathf.Clamp(Mathf.Min(_aaCost.Value, _rapidCost.Value,
                _parachuteCost.Value, _lightTankCost.Value, _mortarCost.Value,
                _afv6Cost.Value, _aaGunContainerCost.Value), 1f, 1000f);
        }

        private static void RepairDonateLayout(ContributeToFaction menu,
            Transform optionContainer)
        {
            RectTransform root = menu.transform as RectTransform;
            if (root == null || optionContainer == null) return;
            DonateLayoutState state = menu.GetComponent<DonateLayoutState>();
            if (state == null) state = menu.gameObject.AddComponent<DonateLayoutState>();
            if (!state.Captured)
            {
                state.Root = root;
                state.RootSize = root.sizeDelta;
                state.RootPosition = root.position;
                state.Captured = true;
            }
            else if (state.Root == root)
            {
                root.sizeDelta = state.RootSize;
                root.position = state.RootPosition;
            }
            Canvas.ForceUpdateCanvases();
            RectTransform[] rows = optionContainer
                .GetComponentsInChildren<ConvoyPurchaseOption>(true)
                .Where(option => option != null && option.gameObject.activeSelf)
                .Select(option => option.transform as RectTransform)
                .Where(rect => rect != null).ToArray();
            if (rows.Length == 0) return;
            FieldInfo fundsField = AccessTools.Field(typeof(ContributeToFaction),
                "currentFunds");
            TMP_Text funds = fundsField == null
                ? null : fundsField.GetValue(menu) as TMP_Text;
            if (funds == null) return;
            float requiredGrowth = Mathf.Max(0f,
                WorldTop(funds.rectTransform) - rows.Min(WorldBottom) + 14f);
            float growth = Mathf.Min(requiredGrowth,
                Mathf.Max(0f, Screen.height - 32f - root.rect.height));
            if (growth < 1f) return;
            float oldTop = WorldTop(root);
            Vector2 size = root.sizeDelta;
            size.y += growth;
            root.sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            Vector3 position = root.position;
            position.y += oldTop - WorldTop(root);
            root.position = position;
        }

        private static float WorldTop(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners.Max(corner => corner.y);
        }

        private static float WorldBottom(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners.Min(corner => corner.y);
        }

        private void OpenSupportSelector(ContributeToFaction menu, Player player)
        {
            _selectorMenu = menu;
            _selectorPlayer = player;
            _selectorVisible = true;
            _supportDropdownOpen = false;
            _selectorStatus = "Select support, then click the native map to place the requested centre point.";
            _selectorWindow.x = Mathf.Clamp(_selectorWindow.x, 0f,
                Mathf.Max(0f, Screen.width - _selectorWindow.width));
            _selectorWindow.y = Mathf.Clamp(_selectorWindow.y, 0f,
                Mathf.Max(0f, Screen.height - _selectorWindow.height));
            OpenNativeMap();
        }

        private void OpenNativeMap()
        {
            _map = SceneSingleton<DynamicMap>.i;
            if (_map == null)
            {
                _selectorStatus = "The native map is not ready. Join a running mission and try again.";
                return;
            }
            if (!DynamicMap.mapMaximized)
            {
                // AircraftSelectionMenu deliberately sets AllowedToOpen=false.
                // AIRLIFT owns this explicit button press, so bypass that one
                // Maximize guard without leaving the game's global permission
                // enabled after the synchronous call returns.
                bool wasAllowedToOpen = DynamicMap.AllowedToOpen;
                try
                {
                    DynamicMap.AllowedToOpen = true;
                    _map.Maximize();
                }
                finally
                {
                    DynamicMap.AllowedToOpen = wasAllowedToOpen;
                }
                _openedMap = DynamicMap.mapMaximized;
            }
            if (!DynamicMap.mapMaximized)
                _selectorStatus = "The native map cannot be opened in the current game state.";
            else
                SuppressSelectionUi();
        }

        private void SuppressSelectionUi()
        {
            if (_suppressedUi.Count > 0) return;
            AircraftSelectionMenu selection = _selectorMenu == null
                ? null : _selectorMenu.GetComponentInParent<AircraftSelectionMenu>();
            if (selection == null)
                selection = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();

            var roots = new List<GameObject>();
            if (selection != null && selection.gameObject.activeInHierarchy)
                roots.Add(selection.gameObject);
            if (_selectorMenu != null && _selectorMenu.gameObject.activeInHierarchy
                && (selection == null
                    || !_selectorMenu.transform.IsChildOf(selection.transform)))
                roots.Add(_selectorMenu.gameObject);

            foreach (GameObject root in roots.Distinct())
            {
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                if (group == null) group = root.AddComponent<CanvasGroup>();
                _suppressedUi.Add(new SuppressedUiState {
                    Group = group,
                    Alpha = group.alpha,
                    Interactable = group.interactable,
                    BlocksRaycasts = group.blocksRaycasts
                });
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        private void RestoreSelectionUi()
        {
            foreach (SuppressedUiState state in _suppressedUi)
            {
                if (state == null || state.Group == null) continue;
                state.Group.alpha = state.Alpha;
                state.Group.interactable = state.Interactable;
                state.Group.blocksRaycasts = state.BlocksRaycasts;
            }
            _suppressedUi.Clear();
        }

        private void Update()
        {
            if (!_selectorVisible || Application.isBatchMode) return;
            if (_selectorPlayer == null || _selectorPlayer.HQ == null)
            {
                _selectorStatus = "Join a faction before purchasing support.";
                return;
            }
            if (_map == null || _map != SceneSingleton<DynamicMap>.i)
                _map = SceneSingleton<DynamicMap>.i;
            if (_map == null || !DynamicMap.mapMaximized)
            {
                RestoreSelectionUi();
                return;
            }
            if (!Input.GetMouseButtonUp(0) || !_map.IsCursorInMapRectangle())
                return;
            Vector2 guiPoint = new Vector2(Input.mousePosition.x,
                Screen.height - Input.mousePosition.y);
            if (_selectorWindow.Contains(guiPoint)) return;
            GlobalPosition candidate;
            if (!_map.TryGetCursorCoordinates(out candidate)) return;
            _markerPosition = candidate;
            _hasMarker = true;
            _selectorStatus = "Marker placed at global "
                + candidate.x.ToString("0", CultureInfo.InvariantCulture) + ", "
                + candidate.z.ToString("0", CultureInfo.InvariantCulture)
                + ". The server searches nearby and charges only an accepted request.";
            UpdateMarker();
        }

        private void UpdateMarker()
        {
            if (!_hasMarker || _map == null || !DynamicMap.mapMaximized)
            {
                DestroyMarker();
                return;
            }
            if (_marker == null)
            {
                _marker = Instantiate(_map.mapWaypoint, _map.iconLayer.transform);
                _marker.name = "KellysAIRLIFT_SupportMarker";
                _markerVector = Instantiate(_map.mapWaypointVector,
                    _map.iconLayer.transform);
                _markerVector.SetActive(false);
                foreach (Graphic graphic in _marker
                    .GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
            }
            Vector3 global = _markerPosition.AsVector3() * _map.mapDisplayFactor;
            _marker.transform.localPosition = new Vector3(global.x, global.z, 0f);
            Vector3 local = _marker.transform.localPosition;
            Vector3 world = _marker.transform.position;
            if (_mapWaypoint == null)
                _mapWaypoint = new MapWaypoint(world, local, _marker, _markerVector);
            else
            {
                _mapWaypoint.waypointPosition = world;
                _mapWaypoint.previousWaypoint = local;
                _mapWaypoint.PlaceMarker();
            }
            _markerVector.SetActive(false);
        }

        private void DestroyMarker()
        {
            if (_marker != null) Destroy(_marker);
            if (_markerVector != null) Destroy(_markerVector);
            _marker = null;
            _markerVector = null;
            _mapWaypoint = null;
        }

        private void OnGUI()
        {
            if (!_selectorVisible || Application.isBatchMode) return;
            _selectorWindow.width = Mathf.Min(500f, Screen.width);
            _selectorWindow.x = Mathf.Clamp(_selectorWindow.x, 0f,
                Mathf.Max(0f, Screen.width - _selectorWindow.width));
            _selectorWindow.y = Mathf.Clamp(_selectorWindow.y, 0f,
                Mathf.Max(0f, Screen.height - 40f));
            _selectorWindow = GUI.Window(0x4B4153, _selectorWindow,
                DrawSupportSelector, "AIRLIFT Support Purchase Prototype");
        }

        private void DrawSupportSelector(int windowId)
        {
            GUILayout.Space(5f);
            GUILayout.Label("SUPPORT TYPE");
            int previousSupport = _supportIndex;
            if (GUILayout.Button(SupportNames[_supportIndex], GUILayout.Height(32f)))
                _supportDropdownOpen = !_supportDropdownOpen;
            if (_supportDropdownOpen)
            {
                for (int index = 0; index < SupportNames.Length; index++)
                {
                    if (!GUILayout.Button(SupportNames[index])) continue;
                    _supportIndex = index;
                    _supportDropdownOpen = false;
                }
            }
            if (previousSupport != _supportIndex)
            {
                OpenNativeMap();
                _selectorStatus = _hasMarker
                    ? "Existing marker retained. Click the map to move it."
                    : "Click the native map outside this window to place a marker.";
            }

            GUILayout.Space(8f);
            GUILayout.Label("TARGET");
            GUILayout.Label(_hasMarker
                ? "Marker: " + _markerPosition.x.ToString("0", CultureInfo.InvariantCulture)
                    + ", " + _markerPosition.z.ToString("0", CultureInfo.InvariantCulture)
                : "No marker selected");
            if (!DynamicMap.mapMaximized
                && GUILayout.Button("Open Native Map", GUILayout.Height(28f)))
                OpenNativeMap();

            GUILayout.Space(8f);
            GUILayout.Label(_selectorStatus ?? string.Empty);
            float cost = DisplayedCost(_supportIndex);
            GUILayout.Label("Displayed cost: " + UnitConverter.ValueReading(cost)
                + "   Player allocation: "
                + (_selectorPlayer == null ? "unknown"
                    : UnitConverter.ValueReading(_selectorPlayer.Allocation)));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            bool enabledBefore = GUI.enabled;
            GUI.enabled = _selectorPlayer != null && _hasMarker;
            if (GUILayout.Button("Buy Support", GUILayout.Height(34f)))
                SubmitPurchase();
            GUI.enabled = enabledBefore;
            if (GUILayout.Button("Clear Marker", GUILayout.Height(34f)))
            {
                _hasMarker = false;
                DestroyMarker();
                _selectorStatus = "Marker cleared.";
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(34f)))
                CloseSelector();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, _selectorWindow.width, 28f));
        }

        private float DisplayedCost(int supportIndex)
        {
            float configured;
            switch (supportIndex)
            {
                case 0: configured = _aaCost.Value; break;
                case 1: configured = _rapidCost.Value; break;
                case 2: configured = _parachuteCost.Value; break;
                case 3: configured = _lightTankCost.Value; break;
                case 4: configured = _mortarCost.Value; break;
                case 5: configured = _afv6Cost.Value; break;
                case 6: configured = _aaGunContainerCost.Value; break;
                default: configured = _aaCost.Value; break;
            }
            return Mathf.Clamp(configured, 1f, 1000f);
        }

        private void SubmitPurchase()
        {
            try
            {
                if (ChatManager.i == null)
                {
                    _selectorStatus = "Chat networking is not ready.";
                    return;
                }
                string command = "/airlift purchase support "
                    + SupportTokens[_supportIndex] + " "
                    + _markerPosition.x.ToString("R", CultureInfo.InvariantCulture)
                    + " " + _markerPosition.y.ToString("R", CultureInfo.InvariantCulture)
                    + " " + _markerPosition.z.ToString("R", CultureInfo.InvariantCulture);
                ChatManager.SendChatMessage(command, false);
                CloseSelector();
            }
            catch (Exception exception)
            {
                _selectorStatus = "Support request failed: "
                    + exception.GetType().Name + ".";
                Logger.LogWarning("AIRLIFT support request failed: " + exception);
            }
        }

        private void CloseSelector()
        {
            RestoreSelectionUi();
            _selectorVisible = false;
            _supportDropdownOpen = false;
            _selectorMenu = null;
            _selectorPlayer = null;
            _hasMarker = false;
            DestroyMarker();
            if (_openedMap && _map != null && DynamicMap.mapMaximized)
                _map.Minimize();
            _openedMap = false;
            _map = null;
        }
    }
}

