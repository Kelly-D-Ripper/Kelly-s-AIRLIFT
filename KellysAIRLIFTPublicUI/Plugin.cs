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
        public const string PluginName = "Kelly's AIRLIFT Public Purchase UI";
        public const string PluginVersion = "0.14.1";

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
        private ConfigEntry<float> _displayedCost;
        private readonly List<Airbase> _enemyAirbases = new List<Airbase>();
        private readonly List<string> _enemyAirportNames = new List<string>();
        private Rect _selectorWindow = new Rect(40f, 80f, 460f, 290f);
        private Vector2 _airportScroll;
        private ContributeToFaction _selectorMenu;
        private Player _selectorPlayer;
        private int _selectedAirport;
        private bool _selectorVisible;
        private bool _airportDropdownOpen;
        private string _selectorStatus;

        private sealed class CombatPurchaseMarker : MonoBehaviour { }

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
            _enabled = Config.Bind("Purchase", "ShowCombatAirlift", true,
                "Show the RAPID Combat Drop entry under Donate > Vehicles.");
            _displayedCost = Config.Bind("Purchase", "DisplayedCombatDropCost",
                400f,
                "Displayed client price in Nuclear Option's native million-dollar units; 400 means $400m. The AIRLIFT server independently enforces its configured price.");
            if (Application.isBatchMode) return;
            ValidateApi();
            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(AccessTools.Method(typeof(ContributeToFaction),
                    nameof(ContributeToFaction.RefreshVehicleList)),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(RefreshVehicleListPostfix)));
            Logger.LogInfo(PluginName + " " + PluginVersion
                + " loaded. RAPID Combat Drop will appear under Donate > Vehicles.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
            _selectorVisible = false;
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
                    "AIRLIFT Donate-menu injection failed closed: " + exception);
            }
        }

        private void AddPurchaseButton(ContributeToFaction menu)
        {
            Player player = LocalPlayerField.GetValue(menu) as Player;
            GameObject prefab = ConvoyPrefabField.GetValue(menu) as GameObject;
            Transform background = ConvoyBackgroundField.GetValue(menu) as Transform;
            HoverText hoverText = HoverTextField.GetValue(menu) as HoverText;
            if (player == null || prefab == null || background == null) return;

            CombatPurchaseMarker[] old = background.GetComponentsInChildren<CombatPurchaseMarker>(true);
            for (int index = 0; index < old.Length; index++)
                if (old[index] != null) Destroy(old[index].gameObject);

            GameObject clone = Instantiate(prefab, background);
            clone.name = "KellysAIRLIFT_CombatPurchase";
            clone.AddComponent<CombatPurchaseMarker>();
            ConvoyPurchaseOption option = clone.GetComponent<ConvoyPurchaseOption>();
            if (option == null)
            {
                Destroy(clone);
                return;
            }
            if (hoverText != null) option.SetButtonHoverText(hoverText);
            float cost = Mathf.Clamp(_displayedCost.Value, 1f, 1000f);
            TMP_Text optionText = OptionTextField.GetValue(option) as TMP_Text;
            if (optionText != null)
                optionText.text = "RAPID Combat Drop (" + UnitConverter.ValueReading(cost) + ")";
            Image optionIcon = OptionIconField.GetValue(option) as Image;
            if (optionIcon != null)
            {
                Image nativeIcon = background.GetComponentsInChildren<ConvoyPurchaseOption>(true)
                    .Where(candidate => candidate != null && candidate != option)
                    .Select(candidate => OptionIconField.GetValue(candidate) as Image)
                    .FirstOrDefault(candidate => candidate != null && candidate.sprite != null);
                if (nativeIcon != null) optionIcon.sprite = nativeIcon.sprite;
            }
            GameObject unavailable = OptionUnavailableField.GetValue(option) as GameObject;
            if (unavailable != null) unavailable.SetActive(false);
            OptionLocalPlayerField.SetValue(option, player);
            OptionCostField.SetValue(option, cost);
            OptionCanSpawnField.SetValue(option, true);
            OptionWasInteractableField.SetValue(option, true);
            OptionCompositionField.SetValue(option,
                "\n5x MC-260 Chimera\n2x Type-12 MBT\n2x AFV6 IFV"
                + "\n2x AFV6 AA\n2x FRCV-105 light tank");
            ShowHoverText optionHover = OptionHoverField.GetValue(option) as ShowHoverText;
            if (optionHover != null)
                optionHover.SetText("Request five MC-260 Chimeras carrying eight combat vehicles. "
                    + "You select an enemy-held airport; the server validates its ownership and runway, "
                    + "AGM-68-armed transports suppress hostile runway ground units. The server charges only "
                    + "an accepted request and enforces a five-minute faction cooldown.");
            Button button = OptionButtonField.GetValue(option) as Button;
            if (button == null)
            {
                Destroy(clone);
                return;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => OpenPurchaseSelector(menu, player));
            button.interactable = player.Allocation >= cost;
            RepairDonateLayout(menu, background);
        }

        private static void RepairDonateLayout(ContributeToFaction menu, Transform optionContainer)
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
            RectTransform[] rows = optionContainer.GetComponentsInChildren<ConvoyPurchaseOption>(true)
                .Where(option => option != null && option.gameObject.activeSelf)
                .Select(option => option.transform as RectTransform)
                .Where(rect => rect != null).ToArray();
            if (rows.Length == 0) return;

            FieldInfo currentFundsField = AccessTools.Field(typeof(ContributeToFaction), "currentFunds");
            TMP_Text currentFunds = currentFundsField == null
                ? null : currentFundsField.GetValue(menu) as TMP_Text;
            if (currentFunds == null) return;

            float lastRowBottom = rows.Min(WorldBottom);
            float fundsTop = WorldTop(currentFunds.rectTransform);
            float requiredGrowth = Mathf.Max(0f, fundsTop - lastRowBottom + 14f);
            if (requiredGrowth < 1f) return;

            float availableGrowth = Mathf.Max(0f, Screen.height - 32f - root.rect.height);
            float growth = Mathf.Min(requiredGrowth, availableGrowth);
            if (growth < 1f) return;

            float oldTop = WorldTop(root);
            Vector2 size = root.sizeDelta;
            size.y += growth;
            root.sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            Vector3 position = root.position;
            position.y += oldTop - WorldTop(root);
            root.position = position;
            Canvas.ForceUpdateCanvases();
            if (_instance != null)
                _instance.Logger.LogInfo("AIRLIFT repaired Nuclear Option 0.34.2 Donate layout: "
                    + rows.Length + " vehicle rows; panel grew "
                    + growth.ToString("F0", CultureInfo.InvariantCulture) + " UI units.");
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

        private void OpenPurchaseSelector(ContributeToFaction menu, Player player)
        {
            _selectorMenu = menu;
            _selectorPlayer = player;
            _selectorVisible = true;
            _airportDropdownOpen = false;
            _selectorStatus = null;
            RefreshEnemyAirports();
            _selectorWindow.x = Mathf.Clamp(_selectorWindow.x, 0f,
                Mathf.Max(0f, Screen.width - _selectorWindow.width));
            _selectorWindow.y = Mathf.Clamp(_selectorWindow.y, 0f,
                Mathf.Max(0f, Screen.height - _selectorWindow.height));
        }

        private void RefreshEnemyAirports()
        {
            string selected = _enemyAirportNames.Count == 0
                || _selectedAirport >= _enemyAirportNames.Count
                ? null : _enemyAirportNames[_selectedAirport];
            _enemyAirbases.Clear();
            _enemyAirportNames.Clear();
            FactionHQ hq = _selectorPlayer == null ? null : _selectorPlayer.HQ;
            if (hq == null || hq.faction == null)
            {
                _selectorStatus = "Join a faction before purchasing a RAPID drop.";
                _selectedAirport = 0;
                return;
            }
            Airbase[] live = UnityEngine.Object.FindObjectsOfType<Airbase>()
                .Where(airbase => airbase != null && !airbase.disabled
                    && airbase.CurrentHQ != null && airbase.CurrentHQ.faction != null
                    && airbase.CurrentHQ.faction != hq.faction
                    && airbase.runways != null
                    && airbase.runways.Any(runway => runway != null
                        && runway.Start != null && runway.End != null))
                .GroupBy(AirbaseName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(AirbaseName, StringComparer.OrdinalIgnoreCase).ToArray();
            _enemyAirbases.AddRange(live);
            _enemyAirportNames.AddRange(live.Select(AirbaseName));
            int restored = selected == null ? -1 : _enemyAirportNames.FindIndex(name =>
                string.Equals(name, selected, StringComparison.OrdinalIgnoreCase));
            _selectedAirport = restored >= 0 ? restored
                : Mathf.Clamp(_selectedAirport, 0,
                    Mathf.Max(0, _enemyAirportNames.Count - 1));
            _selectorStatus = _enemyAirportNames.Count == 0
                ? "No enemy-held airport with runway data is available."
                : "The server will re-check enemy ownership and select an eligible runway before charging.";
        }

        private static string AirbaseName(Airbase airbase)
        {
            if (airbase == null) return "<unknown airport>";
            if (airbase.SavedAirbase != null
                && !string.IsNullOrWhiteSpace(airbase.SavedAirbase.DisplayName))
                return airbase.SavedAirbase.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(airbase.NetworknetworkUniqueName))
                return airbase.NetworknetworkUniqueName.Trim();
            return string.IsNullOrWhiteSpace(airbase.name)
                ? "<unnamed airport>" : airbase.name.Trim();
        }

        private void OnGUI()
        {
            if (!_selectorVisible || Application.isBatchMode) return;
            if (_selectorMenu == null || !_selectorMenu.gameObject.activeInHierarchy)
            {
                _selectorVisible = false;
                return;
            }
            _selectorWindow.width = Mathf.Min(460f, Screen.width);
            _selectorWindow.x = Mathf.Clamp(_selectorWindow.x, 0f,
                Mathf.Max(0f, Screen.width - _selectorWindow.width));
            _selectorWindow.y = Mathf.Clamp(_selectorWindow.y, 0f,
                Mathf.Max(0f, Screen.height - 40f));
            _selectorWindow = GUI.Window(0x4B414C, _selectorWindow,
                DrawPurchaseSelector, "RAPID Runway Assault Destination");
        }

        private void DrawPurchaseSelector(int windowId)
        {
            GUILayout.Space(5f);
            GUILayout.Label("ENEMY AIRPORT");
            string current = _enemyAirportNames.Count == 0
                ? "<no enemy airport available>"
                : _enemyAirportNames[Mathf.Clamp(_selectedAirport, 0,
                    _enemyAirportNames.Count - 1)];
            if (GUILayout.Button(current, GUILayout.Height(30f)))
                _airportDropdownOpen = !_airportDropdownOpen;
            if (_airportDropdownOpen)
            {
                _airportScroll = GUILayout.BeginScrollView(_airportScroll,
                    GUI.skin.box, GUILayout.Height(Mathf.Min(150f,
                        34f * Mathf.Max(1, _enemyAirportNames.Count))));
                for (int index = 0; index < _enemyAirportNames.Count; index++)
                {
                    Airbase airbase = _enemyAirbases[index];
                    string owner = airbase == null || airbase.CurrentHQ == null
                        || airbase.CurrentHQ.faction == null
                        || string.IsNullOrWhiteSpace(airbase.CurrentHQ.faction.factionName)
                        ? "Enemy" : airbase.CurrentHQ.faction.factionName;
                    if (!GUILayout.Button(_enemyAirportNames[index] + " — " + owner))
                        continue;
                    _selectedAirport = index;
                    _airportDropdownOpen = false;
                }
                GUILayout.EndScrollView();
            }
            GUILayout.Label(_selectorStatus ?? string.Empty);
            GUILayout.Label("Cost: " + UnitConverter.ValueReading(
                    Mathf.Clamp(_displayedCost.Value, 1f, 1000f))
                + "   Cooldown: 5 minutes per faction");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(30f)))
                RefreshEnemyAirports();
            bool previous = GUI.enabled;
            GUI.enabled = _enemyAirportNames.Count > 0;
            if (GUILayout.Button("Purchase RAPID Drop", GUILayout.Height(30f)))
            {
                string airport = _enemyAirportNames[Mathf.Clamp(_selectedAirport,
                    0, _enemyAirportNames.Count - 1)];
                SendPurchaseRequest(airport);
                _selectorVisible = false;
            }
            GUI.enabled = previous;
            if (GUILayout.Button("Cancel", GUILayout.Height(30f)))
                _selectorVisible = false;
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, _selectorWindow.width, 28f));
        }

        private void SendPurchaseRequest(string airport)
        {
            try
            {
                if (ChatManager.i == null)
                {
                    Logger.LogWarning("AIRLIFT purchase request ignored: chat networking is not ready.");
                    return;
                }
                ChatManager.SendChatMessage("/airlift purchase rapid " + airport, false);
            }
            catch (Exception exception)
            {
                Logger.LogWarning("AIRLIFT purchase request failed: " + exception);
            }
        }
    }
}
